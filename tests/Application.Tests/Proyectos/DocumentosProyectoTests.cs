using Diger.TramitesEstado.Application.Common.Interfaces;
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
/// El repositorio documental del proyecto: versionado y alcance.
///
/// <para>Lo que se vigila acá son las dos promesas del diseño. Una: <b>ninguna versión se pisa</b>
/// —subir de nuevo agrega, nunca reemplaza—. Dos, y es la que importa: <b>el documento no
/// reimplementa quién lo ve, lo hereda del proyecto</b>. Si alguna vez alguien copia las ramas del
/// filtro en la entidad de documentos, estas pruebas seguirán pasando pero las dos copias
/// empezarán a separarse; por eso hay un caso por cada rama —institución, responsable e
/// interesado—, que es donde se notaría.</para>
/// </summary>
public class DocumentosProyectoTests : IDisposable
{
    private readonly List<AppDbContext> _contextos = [];
    private readonly string _bd = Guid.NewGuid().ToString();

    private static readonly Guid Duenio      = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Interesado  = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Desconocido = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private AppDbContext ContextoDe(
        string? institucion, Guid? usuario = null, bool global = false,
        NivelAlcance nivel = NivelAlcance.Institucion)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.EsGlobal.Returns(global);
        u.NivelAlcance.Returns(nivel);
        u.ActiveInstitucionId.Returns(institucion);
        u.ActiveAreaId.Returns((string?)null);
        u.ActiveUnidadId.Returns((string?)null);
        u.UserId.Returns(usuario);
        u.Nombre.Returns("Quien sube");

        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_bd).Options,
            u, Substitute.For<MediatR.IPublisher>());
        _contextos.Add(ctx);
        return ctx;
    }

    private AppDbContext Global() => ContextoDe(null, global: true, nivel: NivelAlcance.Global);

    /// <summary>Un proyecto de DIGER con un documento, sembrado sin filtro.</summary>
    private (int ProyectoId, int DocumentoId) SembrarAsync(
        string institucion = "DIGER", Guid? responsable = null, Guid? interesado = null)
    {
        var ctx = Global();

        var categoria = CategoriaDocumento.Crear("Convenio", 1);
        ctx.CategoriasDocumento.Add(categoria);
        ctx.SaveChanges();

        var proyecto = Proyecto.Crear("PRY-2026-01", "Frente de prueba");
        proyecto.InstitucionId = institucion;
        proyecto.ResponsableId = responsable;
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        if (interesado is { } uid)
        {
            ctx.ProyectoInteresados.Add(InteresadoProyecto.Crear(
                proyecto.Id, uid, "Interesado", RolInteresado.ContraparteTecnica, "seed",
                NivelCualitativo.Alta));
            ctx.SaveChanges();
        }

        var doc = DocumentoProyecto.Crear(proyecto.Id, categoria.Id, "Convenio marco");
        doc.AgregarVersion("convenio.pdf", "/uploads/proyectos/a.pdf", 1024, new string('a', 64), "Quien sube");
        ctx.ProyectoDocumentos.Add(doc);
        ctx.SaveChanges();

        return (proyecto.Id, doc.Id);
    }

    private static Task<int> CuantosVeAsync(AppDbContext ctx) =>
        ctx.ProyectoDocumentos.AsNoTracking().CountAsync();

    // ── Versionado ────────────────────────────────────────────────
    [Fact]
    public void Las_versiones_se_numeran_solas_y_la_vigente_es_la_ultima()
    {
        var doc = DocumentoProyecto.Crear(1, 1, "Acta de entrega");

        doc.AgregarVersion("v1.pdf", "/uploads/proyectos/1.pdf", 10, new string('a', 64), "Ana");
        doc.AgregarVersion("v2.pdf", "/uploads/proyectos/2.pdf", 20, new string('b', 64), "Ana", "se corrigió la fecha");
        doc.AgregarVersion("v3.pdf", "/uploads/proyectos/3.pdf", 30, new string('c', 64), "Beto");

        doc.Versiones.Select(v => v.Numero).Should().Equal(1, 2, 3);
        doc.TotalVersiones.Should().Be(3);
        doc.Vigente!.ArchivoNombre.Should().Be("v3.pdf");
        doc.Vigente.Numero.Should().Be(3);
    }

    [Fact]
    public void Subir_una_version_nueva_no_borra_la_anterior()
    {
        var doc = DocumentoProyecto.Crear(1, 1, "Acta");
        doc.AgregarVersion("original.pdf", "/uploads/proyectos/1.pdf", 10, new string('a', 64), "Ana");

        doc.AgregarVersion("corregida.pdf", "/uploads/proyectos/2.pdf", 20, new string('b', 64), "Ana");

        doc.Versiones.Should().HaveCount(2, "reemplazar un acta firmada sin dejar rastro es justo lo que no debe poder hacerse");
        doc.Versiones.Should().Contain(v => v.ArchivoNombre == "original.pdf");
    }

    [Fact]
    public void Un_documento_recien_creado_no_tiene_version_vigente()
    {
        DocumentoProyecto.Crear(1, 1, "Sin archivo todavía").Vigente.Should().BeNull();
    }

    [Fact]
    public void Rechaza_un_archivo_vacio()
    {
        var doc = DocumentoProyecto.Crear(1, 1, "Acta");

        var act = () => doc.AgregarVersion("vacio.pdf", "/uploads/proyectos/x.pdf", 0, new string('a', 64), "Ana");

        act.Should().Throw<DomainException>().WithMessage("*vacío*");
    }

    [Fact]
    public void El_documento_exige_titulo_proyecto_y_categoria()
    {
        var sinTitulo   = () => DocumentoProyecto.Crear(1, 1, "   ");
        var sinProyecto = () => DocumentoProyecto.Crear(0, 1, "Acta");
        var sinCategoria = () => DocumentoProyecto.Crear(1, 0, "Acta");

        sinTitulo.Should().Throw<DomainException>();
        sinProyecto.Should().Throw<DomainException>();
        sinCategoria.Should().Throw<DomainException>();
    }

    // ── Alcance heredado ──────────────────────────────────────────
    [Fact]
    public async Task Quien_ve_el_proyecto_ve_su_documentacion()
    {
        SembrarAsync();

        (await CuantosVeAsync(ContextoDe("DIGER"))).Should().Be(1);
    }

    [Fact]
    public async Task Quien_no_ve_el_proyecto_no_ve_ni_uno_de_sus_documentos()
    {
        SembrarAsync();

        (await CuantosVeAsync(ContextoDe("CONSUCOOP"))).Should().Be(0,
            "el documento hereda el filtro del proyecto; si esto devuelve 1, el filtro se está aplicando al proyecto pero no a su documentación");
    }

    [Fact]
    public async Task El_responsable_ve_la_documentacion_aunque_el_proyecto_caiga_fuera_de_su_alcance()
    {
        SembrarAsync(institucion: "DIGER", responsable: Duenio);

        // Es una de las dos excepciones del filtro de Proyecto. Si el documento reimplementara el
        // alcance en vez de heredarlo, esta rama sería la primera en perderse.
        (await CuantosVeAsync(ContextoDe("CONSUCOOP", usuario: Duenio))).Should().Be(1);
    }

    [Fact]
    public async Task Un_interesado_ve_la_documentacion_aunque_el_proyecto_caiga_fuera_de_su_alcance()
    {
        SembrarAsync(institucion: "DIGER", interesado: Interesado);

        (await CuantosVeAsync(ContextoDe("CONSUCOOP", usuario: Interesado))).Should().Be(1);
    }

    [Fact]
    public async Task Un_usuario_ajeno_sin_vinculo_no_ve_nada()
    {
        SembrarAsync(institucion: "DIGER", responsable: Duenio, interesado: Interesado);

        (await CuantosVeAsync(ContextoDe("CONSUCOOP", usuario: Desconocido))).Should().Be(0);
    }

    [Fact]
    public async Task Las_versiones_tambien_quedan_fuera_del_alcance_ajeno()
    {
        SembrarAsync();

        // Consultar la tabla de versiones directamente se saltaría el documento: por eso lleva su
        // propio ancla. Es el descuido que en otro portal dejó escrituras sin alcance.
        var ajeno = ContextoDe("CONSUCOOP");

        (await ajeno.ProyectoDocumentoVersiones.AsNoTracking().CountAsync()).Should().Be(0);
        (await Global().ProyectoDocumentoVersiones.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_borrado_logico_saca_el_documento_de_toda_consulta()
    {
        var (_, documentoId) = SembrarAsync();

        var ctx = Global();
        var doc = await ctx.ProyectoDocumentos.FirstAsync(d => d.Id == documentoId);
        doc.IsDeleted = true;
        await ctx.SaveChangesAsync();

        (await CuantosVeAsync(ContextoDe("DIGER"))).Should().Be(0);
    }

    // ── Categorías ────────────────────────────────────────────────
    [Fact]
    public void La_categoria_se_desactiva_en_vez_de_borrarse()
    {
        var categoria = CategoriaDocumento.Crear("Acta", 1);

        categoria.Activa.Should().BeTrue();
        categoria.CambiarActiva(false).Should().BeTrue();
        categoria.Activa.Should().BeFalse();
        categoria.CambiarActiva(false).Should().BeFalse("no cambió nada, y la bitácora no debería registrar un no-cambio");
    }

    [Fact]
    public void La_categoria_exige_nombre()
    {
        var act = () => CategoriaDocumento.Crear("  ", 1);

        act.Should().Throw<DomainException>();
    }

    public void Dispose()
    {
        foreach (var ctx in _contextos) ctx.Dispose();
    }
}
