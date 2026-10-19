using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Commands.ActualizarReunion;
using Diger.TramitesEstado.Application.Reuniones.Commands.CrearReunion;
using Diger.TramitesEstado.Application.Reuniones.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>
/// Convocar no es ver: a una reunión se invita a cualquier institución, no solo a la propia. Es
/// el caso de uso normal del portal —DIGER convoca a la institución cuyos trámites está
/// racionalizando— y el recorte por alcance no tiene nada que hacer acá.
///
/// <para>El arreglo del lado de lectura ya se hizo (la pantalla pide
/// <c>GetActivasParaConvocarAsync</c>, que se salta el filtro de EF). Lo que quedaba vivo era el
/// lado de escritura, en dos capas: el rechazo explícito del comando de crear, y —debajo— la
/// búsqueda de la institución principal por un repositorio con alcance, que devolvía null y
/// guardaba la contraparte en blanco sin avisar.</para>
/// </summary>
public class ConvocarInstitucionesTests
{
    private sealed class Usuario(Guid? id, bool global, string? inst) : ICurrentUserService
    {
        public Guid?        UserId                 => id;
        public string?      Nombre                 => "u" + id;
        public string?      Correo                 => $"u{id}@x.com";
        public string?      Rol                    => global ? "Administrador" : "Empleado";
        public bool         IsAuthenticated        => true;
        public bool         EsGlobal               => global;
        public NivelAlcance NivelAlcance           => global ? NivelAlcance.Global : NivelAlcance.Unidad;
        public bool         EsSoloLectura          => false;
        public bool         EsSupervisor           => false;
        public bool         EsTecnicoSoporte       => false;
        public bool         EsJefeDeArea           => false;
        public bool         EsPmo                  => false;
        public string?      ActiveInstitucionId    => inst;
        public string?      ActiveAreaId           => null;
        public string?      ActiveUnidadId         => null;
        public IReadOnlyCollection<string> InstitucionesAsignadas => inst is null ? [] : [inst];
        public bool PuedeAccederInstitucion(string? institucionId) =>
            global || (institucionId is not null && institucionId == inst);
    }

    private static DbContextOptions<AppDbContext> Opts(string db) =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(db).Options;

    private static AppDbContext Ctx(string db, ICurrentUserService u) =>
        new(Opts(db), u, NSubstitute.Substitute.For<MediatR.IPublisher>());

    /// <summary>Catálogo mínimo: la casa y una institución a la que el usuario NO tiene alcance.</summary>
    private static async Task SembrarCatalogoAsync(string db)
    {
        await using var ctx = Ctx(db, new Usuario(null, true, null));
        await ctx.Instituciones.AddRangeAsync(
            Institucion.Crear("DIGER", "DIRECCION PRESIDENCIAL DE GESTION POR RESULTADOS"),
            Institucion.Crear("SRECI", "SECRETARIA DE RELACIONES EXTERIORES"));
        await ctx.SaveChangesAsync();
    }

    private static ReunionFormDto Form(params string[] instituciones) => new()
    {
        Titulo           = "Reunión técnica SRECI-DIGER",
        InstitucionesIds = [.. instituciones],
        Visibilidad      = VisibilidadReunion.Publica
    };

    private static CrearReunionCommandHandler Crear(AppDbContext ctx, ICurrentUserService u) =>
        new(new ReunionRepository(ctx), new InstitucionRepository(ctx), new ContactoRepository(ctx), u, ctx, ctx);

    private static ActualizarReunionCommandHandler Actualizar(AppDbContext ctx, ICurrentUserService u) =>
        new(new ReunionRepository(ctx), new InstitucionRepository(ctx), new ContactoRepository(ctx), u, ctx, ctx);

    [Fact]
    public async Task Un_usuario_no_global_puede_convocar_a_una_institucion_fuera_de_su_alcance()
    {
        var db = Guid.NewGuid().ToString();
        await SembrarCatalogoAsync(db);
        var yo = new Usuario(Guid.NewGuid(), false, "DIGER");

        await using var ctx = Ctx(db, yo);
        var crear = () => Crear(ctx, yo).Handle(new CrearReunionCommand(Form("SRECI"), [], []), default);

        await crear.Should().NotThrowAsync(
            "DIGER convoca a la institución que atiende; exigirle alcance sobre ella es confundir convocar con ver");
    }

    [Fact]
    public async Task La_contraparte_fuera_de_alcance_se_guarda_y_no_queda_en_blanco()
    {
        var db = Guid.NewGuid().ToString();
        await SembrarCatalogoAsync(db);
        var yo = new Usuario(Guid.NewGuid(), false, "DIGER");

        int id;
        await using (var ctx = Ctx(db, yo))
            id = await Crear(ctx, yo).Handle(new CrearReunionCommand(Form("SRECI"), [], []), default);

        await using (var ctx = Ctx(db, new Usuario(null, true, null)))
        {
            var r = await ctx.Reuniones.IgnoreQueryFilters().FirstAsync(x => x.Id == id);
            r.InstitucionId.Should().Be("SRECI",
                "el repositorio que resuelve la institución principal no puede ir filtrado por alcance");
            r.Institucion.Should().NotBeNullOrWhiteSpace("el acta y los listados muestran este nombre");
        }
    }

    [Fact]
    public async Task Editar_una_reunion_no_borra_la_contraparte_fuera_de_alcance()
    {
        // El mismo recorte, en el comando de actualizar, no deja la contraparte en blanco al
        // crearla: la BORRA de una reunión que ya la tenía, en cuanto alguien guarda el formulario.
        var db = Guid.NewGuid().ToString();
        await SembrarCatalogoAsync(db);
        var yo = new Usuario(Guid.NewGuid(), false, "DIGER");

        int id;
        await using (var ctx = Ctx(db, new Usuario(null, true, null)))
        {
            var r = Reunion.Crear("Reunión técnica SRECI-DIGER");
            r.InstitucionId = "SRECI"; r.Institucion = "SECRETARIA DE RELACIONES EXTERIORES";
            r.AgregarInstitucion("SRECI");
            r.CreadoPorId = yo.UserId;   // para que el editor pueda abrirla (ver VisibilidadReunionTests)
            await ctx.Reuniones.AddAsync(r);
            await ctx.SaveChangesAsync();
            id = r.Id;
        }

        await using (var ctx = Ctx(db, yo))
            await Actualizar(ctx, yo).Handle(new ActualizarReunionCommand(id, Form("SRECI"), [], []), default);

        await using (var ctx = Ctx(db, new Usuario(null, true, null)))
            (await ctx.Reuniones.IgnoreQueryFilters().FirstAsync(x => x.Id == id))
                .InstitucionId.Should().Be("SRECI", "guardar sin tocar la institución no puede perderla");
    }

    /// <summary>
    /// Guardar una reunión SIN contraparte no debe inventarle una. <c>CrearReunionCommand</c> deja
    /// <c>InstitucionId</c> en null a propósito, pero el bloque de «inyección automática de
    /// jerarquía institucional» de <c>SaveChangesAsync</c> rellena toda columna
    /// <c>InstitucionId</c> vacía con la institución activa. En el resto del portal eso es correcto
    /// —ahí la columna es el DUEÑO—; en Reunion la columna es la CONTRAPARTE, así que el relleno
    /// fabrica una convocatoria a la propia institución que nadie pidió.
    /// </summary>
    [Fact]
    public async Task Una_reunion_sin_contraparte_no_se_queda_con_la_institucion_del_creador()
    {
        var db = Guid.NewGuid().ToString();
        await SembrarCatalogoAsync(db);
        var yo = new Usuario(Guid.NewGuid(), false, "DIGER");

        int id;
        await using (var ctx = Ctx(db, yo))
            id = await Crear(ctx, yo).Handle(
                new CrearReunionCommand(new ReunionFormDto { Titulo = "Reunión interna sin convocados" }, [], []), default);

        await using (var ctx = Ctx(db, new Usuario(null, true, null)))
        {
            var r = await ctx.Reuniones.IgnoreQueryFilters().FirstAsync(x => x.Id == id);
            r.InstitucionId.Should().BeNull(
                "no se eligió contraparte; estampar la propia institución muestra una convocatoria que no existe");
        }
    }

    /// <summary>
    /// Al guardar, <c>ContactoFeeder</c> da de alta en el catálogo la institución que trae escrita
    /// cada asistente. Busca si ya existe con <c>GetByNombreAsync</c>, que va filtrado por alcance:
    /// para un usuario no global una institución ajena «no existe», así que la crea de nuevo con un
    /// Id derivado del nombre. <c>IX_Instituciones_Nombre</c> es ÚNICO, así que en SQL Server el
    /// guardado de la reunión revienta con violación de índice; acá —donde el proveedor en memoria
    /// no lo exige— se ve la otra cara: el catálogo gana una institución duplicada.
    /// </summary>
    [Fact]
    public async Task Un_asistente_de_otra_institucion_no_duplica_el_catalogo()
    {
        var db = Guid.NewGuid().ToString();
        await SembrarCatalogoAsync(db);
        var yo = new Usuario(Guid.NewGuid(), false, "DIGER");

        var asistente = new AsistenteInput
        {
            Nombre      = "Ana Méndez",
            Correo      = "ana.mendez@sreci.gob.hn",
            Institucion = "SECRETARIA DE RELACIONES EXTERIORES"   // ya está en el catálogo, como SRECI
        };

        await using (var ctx = Ctx(db, yo))
            await Crear(ctx, yo).Handle(new CrearReunionCommand(Form("SRECI"), [asistente], []), default);

        await using (var ctx = Ctx(db, new Usuario(null, true, null)))
        {
            (await ctx.Instituciones.IgnoreQueryFilters().CountAsync()).Should().Be(2,
                "la institución del asistente ya existía; en SQL Server esto es una violación de IX_Instituciones_Nombre");
            (await ctx.Contactos.IgnoreQueryFilters().Select(c => c.InstitucionId).ToListAsync())
                .Should().AllBe("SRECI", "el contacto debe colgar de la institución que ya existía");
        }
    }

    /// <summary>
    /// La institución de cada asistente es la SUYA, no la de quien convoca. El mismo bloque de
    /// inyección automática de <c>SaveChangesAsync</c> rellena <c>Asistente.InstitucionId</c> —que
    /// <c>ReunionMapper</c> deja en null a propósito cuando el asistente se capturó como texto
    /// libre— con la institución activa del que guarda.
    ///
    /// <para>En la base real son 182 asistentes cuyo Id contradice el texto escrito junto a su
    /// nombre: 312 filas con Id y solo 2 Ids distintos. Se nota al reabrir la reunión, porque el
    /// <c>select</c> de institución de cada asistente se pinta desde ese Id.</para>
    /// </summary>
    [Fact]
    public async Task El_asistente_conserva_su_institucion_y_no_hereda_la_del_convocante()
    {
        var db = Guid.NewGuid().ToString();
        await SembrarCatalogoAsync(db);
        var yo = new Usuario(Guid.NewGuid(), false, "DIGER");

        var asistente = new AsistenteInput
        {
            Nombre        = "Ana Méndez",
            Institucion   = "SECRETARIA DE RELACIONES EXTERIORES",  // texto libre, como en el legado
            InstitucionId = null
        };

        int id;
        await using (var ctx = Ctx(db, yo))
            id = await Crear(ctx, yo).Handle(new CrearReunionCommand(Form("SRECI"), [asistente], []), default);

        await using (var ctx = Ctx(db, new Usuario(null, true, null)))
            (await ctx.Asistentes.IgnoreQueryFilters().Where(a => a.ReunionId == id).Select(a => a.InstitucionId).ToListAsync())
                .Should().AllSatisfy(x => x.Should().NotBe("DIGER"),
                    "Ana es de SRECI; etiquetarla con la institución del convocante falsea la lista de asistencia");
    }
}
