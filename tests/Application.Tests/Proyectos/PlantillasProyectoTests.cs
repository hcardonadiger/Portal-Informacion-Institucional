using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Plantillas;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// Plantillas de estructura de proyecto: las que resuelven el proyecto cíclico.
/// </summary>
/// <remarks>
/// Lo que se mide acá no es tanto que la copia funcione —eso es un doble bucle— sino las tres
/// reglas que la hacen segura: <b>solo sobre estructura vacía</b>, <b>solo lo que alcanza a quien
/// la pide</b> y <b>solo el esqueleto</b>. Las tres se pueden romper sin que la pantalla lo note,
/// porque las tres se ven igual de bien en el navegador el día que se rompen.
/// </remarks>
public class PlantillasProyectoTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    public PlantillasProyectoTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

        _usuario.Nombre.Returns("Henry Cardona");
        _usuario.ActiveInstitucionId.Returns("DIGER");
        _usuario.ActiveAreaId.Returns("TIC");
        _usuario.ActiveUnidadId.Returns("DESARROLLO");
    }

    // ── Andamio ───────────────────────────────────────────────────

    private GuardarPlantillaProyectoCommandHandler Guardar => new(_ctx, _usuario);
    private TraerPlantillaAProyectoCommandHandler  Traer   => new(_ctx, _usuario);

    private static EntregablePlantillaInput Ent(string nombre, params string[] actividades) =>
        new(nombre, $"Descripción de {nombre}",
            actividades.Select(a => new ActividadPlantillaInput(a, $"Cómo se hace {a}")).ToList());

    private Task<int> CrearPlantillaAsync(
        string nombre,
        NivelAlcance nivel = NivelAlcance.Institucion,
        string? areaId = null, string? unidadId = null,
        bool activa = true,
        params EntregablePlantillaInput[] entregables) =>
        Guardar.Handle(new GuardarPlantillaProyectoCommand(
            null, nombre, null, activa, nivel, areaId, unidadId,
            entregables.Length > 0 ? entregables : [Ent("Entregable único", "Actividad única")]),
            CancellationToken.None);

    /// <summary>
    /// Un proyecto directo en el contexto, sin pasar por su comando de alta: lo que se prueba acá
    /// es la copia de estructura, y el alta de proyecto tiene su propia suite.
    /// </summary>
    private async Task<int> CrearProyectoAsync(bool conEstructura = false)
    {
        var p = Proyecto.Crear("PRY-2026-001", "Informe trimestral 2026");
        if (conEstructura)
        {
            var e = EntregableProyecto.Crear("Entregable que ya estaba", 1);
            e.Agregar(ActividadProyecto.Crear("Actividad que ya estaba", 1));
            p.Agregar(e);
        }
        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync(CancellationToken.None);
        return p.Id;
    }

    private Task<Proyecto> LeerProyectoAsync(int id) =>
        _ctx.Proyectos
            .Include(p => p.Entregables).ThenInclude(e => e.Actividades)
            .FirstAsync(p => p.Id == id, CancellationToken.None);

    // ── La plantilla en sí ────────────────────────────────────────

    [Fact]
    public async Task Guardar_NumeraElArbolPorPosicionYNoPorLoQueMandeElFormulario()
    {
        var id = await CrearPlantillaAsync("Informe trimestral",
            entregables: [Ent("Levantamiento", "Entrevistas", "Consolidado"), Ent("Informe", "Redacción")]);

        var plantilla = await _ctx.PlantillasProyecto
            .Include(p => p.Entregables).ThenInclude(e => e.Actividades)
            .FirstAsync(p => p.Id == id);

        plantilla.Entregables.OrderBy(e => e.Orden).Select(e => e.Orden).Should().Equal(1, 2);
        plantilla.Entregables.Single(e => e.Nombre == "Levantamiento")
            .Actividades.OrderBy(a => a.Orden).Select(a => a.Orden).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Guardar_UnaPlantillaSinEntregablesSeRechaza()
    {
        var acto = async () => await Guardar.Handle(new GuardarPlantillaProyectoCommand(
            null, "Vacía", null, true, NivelAlcance.Institucion, null, null, []),
            CancellationToken.None);

        // No es un capricho de validación: una plantilla sin entregables no tiene
        // nada que traer, así que existir le sirve solo para aparecer en el selector.
        await acto.Should().ThrowAsync<DomainException>()
            .WithMessage("*nada que traer*");
    }

    [Fact]
    public async Task Guardar_LasFilasEnBlancoDelFormularioNoLleganAlArbol()
    {
        // El JS de la pantalla agrega filas vacías: quien agrega tres y llena dos
        // no debería acabar con un entregable sin nombre en la plantilla.
        var id = await CrearPlantillaAsync("Con huecos",
            entregables:
            [
                Ent("Real", "Actividad real"),
                new EntregablePlantillaInput("   ", null, [new ActividadPlantillaInput(" ", null)])
            ]);

        var plantilla = await _ctx.PlantillasProyecto
            .Include(p => p.Entregables).FirstAsync(p => p.Id == id);

        plantilla.Entregables.Should().HaveCount(1);
    }

    [Fact]
    public async Task Listar_NoOfreceLasDesactivadas()
    {
        await CrearPlantillaAsync("Vigente");
        await CrearPlantillaAsync("Retirada", activa: false);

        var ofrecidas = await new GetPlantillasProyectoQueryHandler(_ctx, _usuario)
            .Handle(new GetPlantillasProyectoQuery(SoloActivas: true), CancellationToken.None);
        var administradas = await new GetPlantillasProyectoQueryHandler(_ctx, _usuario)
            .Handle(new GetPlantillasProyectoQuery(SoloActivas: false), CancellationToken.None);

        ofrecidas.Select(p => p.Nombre).Should().Equal("Vigente");
        // La pantalla que las administra sí tiene que verla, o no habría forma de reactivarla.
        administradas.Select(p => p.Nombre).Should().BeEquivalentTo(["Retirada", "Vigente"]);
    }

    [Fact]
    public async Task Listar_NoOfreceLaDeOtraArea()
    {
        await CrearPlantillaAsync("De mi área",  NivelAlcance.Area, areaId: "TIC");
        await CrearPlantillaAsync("De otra área", NivelAlcance.Area, areaId: "LEGAL");

        var ofrecidas = await new GetPlantillasProyectoQueryHandler(_ctx, _usuario)
            .Handle(new GetPlantillasProyectoQuery(), CancellationToken.None);

        ofrecidas.Select(p => p.Nombre).Should().Equal("De mi área");
    }

    // ── Traerla al proyecto ───────────────────────────────────────

    [Fact]
    public async Task Traer_CopiaElArbolCompletoRespetandoElOrden()
    {
        var plantillaId = await CrearPlantillaAsync("Informe trimestral",
            entregables: [Ent("Levantamiento", "Entrevistas", "Consolidado"), Ent("Informe", "Redacción")]);
        var proyectoId = await CrearProyectoAsync();

        var cuantos = await Traer.Handle(
            new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        cuantos.Should().Be(2);
        var proyecto = await LeerProyectoAsync(proyectoId);
        proyecto.Entregables.OrderBy(e => e.Orden).Select(e => e.Nombre)
            .Should().Equal("Levantamiento", "Informe");
        proyecto.Entregables.Single(e => e.Nombre == "Levantamiento")
            .Actividades.OrderBy(a => a.Orden).Select(a => a.Nombre)
            .Should().Equal("Entrevistas", "Consolidado");
    }

    [Fact]
    public async Task Traer_CopiaLasDescripcionesPorqueSonLaMitadDeLaPlantilla()
    {
        var plantillaId = await CrearPlantillaAsync("Con instrucciones",
            entregables: [Ent("Levantamiento", "Entrevistas")]);
        var proyectoId = await CrearProyectoAsync();

        await Traer.Handle(new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        var e = (await LeerProyectoAsync(proyectoId)).Entregables.Single();
        e.Descripcion.Should().Be("Descripción de Levantamiento");
        e.Actividades.Single().Descripcion.Should().Be("Cómo se hace Entrevistas");
    }

    [Fact]
    public async Task Traer_NoCopiaFechasEstadosNiAvances()
    {
        // Es la razón de ser de la plantilla: el esqueleto se repite cada ciclo, el
        // calendario no. Traerlo con las fechas del ciclo pasado sería peor que no traerlo.
        var plantillaId = await CrearPlantillaAsync("Cíclica", entregables: [Ent("Entregable", "Actividad")]);
        var proyectoId  = await CrearProyectoAsync();

        await Traer.Handle(new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        var proyecto = await LeerProyectoAsync(proyectoId);
        var e = proyecto.Entregables.Single();
        var a = e.Actividades.Single();

        e.FechaPlan.Should().BeNull();
        e.FechaReal.Should().BeNull();
        e.Estado.Should().Be(EstadoEntregable.Pendiente);
        e.ResponsableId.Should().BeNull();
        a.AvancePct.Should().Be(0);
        a.FechaInicioPlan.Should().BeNull();
        a.FechaFinPlan.Should().BeNull();
        a.ResponsableId.Should().BeNull();
        proyecto.AvancePct.Should().Be(0);
    }

    [Fact]
    public async Task Traer_SobreUnProyectoQueYaTieneEstructuraSeRechaza()
    {
        var plantillaId = await CrearPlantillaAsync("Cíclica");
        var proyectoId  = await CrearProyectoAsync(conEstructura: true);

        var acto = async () => await Traer.Handle(
            new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*ya tiene estructura*");

        // Y no a medias: lo que ya estaba sigue intacto.
        var proyecto = await LeerProyectoAsync(proyectoId);
        proyecto.Entregables.Should().HaveCount(1);
        proyecto.Entregables.Single().Nombre.Should().Be("Entregable que ya estaba");
    }

    [Fact]
    public async Task Traer_UnaPlantillaDesactivadaSeRechaza()
    {
        var plantillaId = await CrearPlantillaAsync("Retirada", activa: false);
        var proyectoId  = await CrearProyectoAsync();

        var acto = async () => await Traer.Handle(
            new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*desactivada*");
    }

    [Fact]
    public async Task Traer_UnaPlantillaDeOtraAreaSeRechazaAunqueSePidaPorId()
    {
        // La pantalla no la ofrece, pero el id viaja en un campo del formulario:
        // la regla tiene que estar también del lado del servidor.
        var plantillaId = await CrearPlantillaAsync("De otra área", NivelAlcance.Area, areaId: "LEGAL");
        var proyectoId  = await CrearProyectoAsync();

        var acto = async () => await Traer.Handle(
            new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*no está disponible*");
        (await LeerProyectoAsync(proyectoId)).Entregables.Should().BeEmpty();
    }

    [Fact]
    public async Task Traer_UnaPlantillaDeMiUnidadSiSeTrae()
    {
        var plantillaId = await CrearPlantillaAsync(
            "De mi unidad", NivelAlcance.Unidad, unidadId: "DESARROLLO");
        var proyectoId = await CrearProyectoAsync();

        await Traer.Handle(new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        (await LeerProyectoAsync(proyectoId)).Entregables.Should().HaveCount(1);
    }

    [Fact]
    public async Task Traer_DejaConstanciaEnLaBitacoraDeQueLaEstructuraNoSeTecleo()
    {
        var plantillaId = await CrearPlantillaAsync("Informe trimestral",
            entregables: [Ent("Levantamiento", "Entrevistas", "Consolidado"), Ent("Informe")]);
        var proyectoId = await CrearProyectoAsync();

        await Traer.Handle(new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        var evento = await _ctx.BitacorasProyecto.SingleAsync(b => b.ProyectoId == proyectoId);
        evento.Tipo.Should().Be(TipoEventoProyecto.ModificacionEstructura);
        evento.Detalle.Should().Contain("Informe trimestral")
            .And.Contain("2 entregables").And.Contain("2 actividades");
    }

    [Fact]
    public async Task Traer_EsIndependienteDeLaPlantillaDespuesDeCopiada()
    {
        // Es copia y no vínculo: corregir el molde no debería reescribir el ciclo
        // que ya cerró con él.
        var plantillaId = await CrearPlantillaAsync("Cíclica", entregables: [Ent("Original")]);
        var proyectoId  = await CrearProyectoAsync();
        await Traer.Handle(new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        await Guardar.Handle(new GuardarPlantillaProyectoCommand(
            plantillaId, "Cíclica", null, true, NivelAlcance.Institucion, null, null,
            [Ent("Renombrado en la plantilla")]), CancellationToken.None);

        (await LeerProyectoAsync(proyectoId)).Entregables.Single().Nombre.Should().Be("Original");
    }

    [Fact]
    public async Task Eliminar_NoSeLlevaPorDelanteElProyectoQueYaLaUso()
    {
        var plantillaId = await CrearPlantillaAsync("Cíclica", entregables: [Ent("Entregable")]);
        var proyectoId  = await CrearProyectoAsync();
        await Traer.Handle(new TraerPlantillaAProyectoCommand(proyectoId, plantillaId), CancellationToken.None);

        await new EliminarPlantillaProyectoCommandHandler(_ctx)
            .Handle(new EliminarPlantillaProyectoCommand(plantillaId), CancellationToken.None);

        (await LeerProyectoAsync(proyectoId)).Entregables.Should().HaveCount(1);
    }

    // ── Clasificación: categoría y acción ─────────────────────────
    // Es lo que deja al alta ofrecer solo las plantillas que encajan con el
    // proyecto que se crea. El filtro vive en la pantalla; acá se mide que el
    // dato se guarde, se limpie y llegue a quien lo filtra.

    private async Task<int> CrearCategoriaAsync(string nombre)
    {
        var c = CategoriaProyecto.Crear(nombre, 1, ColorEtiqueta.Azul);
        _ctx.CategoriasProyecto.Add(c);
        await _ctx.SaveChangesAsync(CancellationToken.None);
        return c.Id;
    }

    private Task<int> GuardarClasificadaAsync(int? id, int? categoriaId, AccionProyecto? accion) =>
        Guardar.Handle(new GuardarPlantillaProyectoCommand(
            id, "POA anual", null, true, NivelAlcance.Institucion, null, null,
            [Ent("Entregable")], categoriaId, accion), CancellationToken.None);

    [Fact]
    public async Task Clasificar_ElListadoYElDetalleDevuelvenCategoriaYAccion()
    {
        var categoriaId = await CrearCategoriaAsync("Planificación");
        var id = await GuardarClasificadaAsync(null, categoriaId, AccionProyecto.Acompanamiento);

        var resumen = (await new GetPlantillasProyectoQueryHandler(_ctx, _usuario)
            .Handle(new GetPlantillasProyectoQuery(), CancellationToken.None)).Single();
        resumen.CategoriaId.Should().Be(categoriaId);
        resumen.Categoria.Should().Be("Planificación");
        resumen.Accion.Should().Be(AccionProyecto.Acompanamiento);

        var detalle = await new GetPlantillaProyectoQueryHandler(_ctx)
            .Handle(new GetPlantillaProyectoQuery(id), CancellationToken.None);
        detalle!.CategoriaId.Should().Be(categoriaId);
        detalle.Accion.Should().Be(AccionProyecto.Acompanamiento);
    }

    [Fact]
    public async Task Clasificar_SinCategoriaNiAccionLaPlantillaSirveParaCualquiera()
    {
        await CrearPlantillaAsync("Genérica");

        var resumen = (await new GetPlantillasProyectoQueryHandler(_ctx, _usuario)
            .Handle(new GetPlantillasProyectoQuery(), CancellationToken.None)).Single();

        // El null es el dato: «para cualquier proyecto». Inventarle una
        // clasificación la escondería del alta en cuanto se eligiera otra.
        resumen.CategoriaId.Should().BeNull();
        resumen.Accion.Should().BeNull();
    }

    [Fact]
    public async Task Clasificar_EditarSinClasificacionLaLimpia()
    {
        var categoriaId = await CrearCategoriaAsync("Planificación");
        var id = await GuardarClasificadaAsync(null, categoriaId, AccionProyecto.Desarrollo);

        await GuardarClasificadaAsync(id, null, null);

        var plantilla = await _ctx.PlantillasProyecto.AsNoTracking().FirstAsync(p => p.Id == id);
        plantilla.CategoriaId.Should().BeNull();
        plantilla.Accion.Should().BeNull();
    }

    [Fact]
    public async Task Clasificar_UnaCategoriaQueNoExisteSeRechaza()
    {
        var acto = () => GuardarClasificadaAsync(null, 9999, null);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*categoría*no existe*");
    }

    [Fact]
    public async Task Clasificar_NoSePuedeEliminarUnaCategoriaQueUsaUnaPlantilla()
    {
        // La llave es Restrict: sin este aviso, borrar la categoría acabaría en
        // un error de base en vez de un mensaje que se pueda leer.
        var categoriaId = await CrearCategoriaAsync("Planificación");
        await GuardarClasificadaAsync(null, categoriaId, null);

        var acto = () => new Diger.TramitesEstado.Application.Proyectos.Categorias.EliminarCategoriaProyectoCommandHandler(_ctx)
            .Handle(new Diger.TramitesEstado.Application.Proyectos.Categorias.EliminarCategoriaProyectoCommand(categoriaId),
                    CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*plantilla*");
    }

    public void Dispose() => _ctx.Dispose();
}
