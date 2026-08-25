using Diger.TramitesEstado.Api.Consultas;
using Diger.TramitesEstado.Api.Lectura;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Api.Tests.Consultas;

public sealed class ConsultaCatalogosAuxiliaresTests : IDisposable
{
    private readonly BaseDePruebas _b = new();

    public void Dispose() => _b.Dispose();

    private ConsultaCatalogosAuxiliares Consulta => new(_b.Ctx);

    // ── Instituciones ─────────────────────────────────────────────────────────

    /// <summary>
    /// En PortalDigital esta tabla lleva un filtro global por alcance institucional, y su consulta
    /// pública tenía que llamar a <c>IgnoreQueryFilters()</c> para que un usuario con alcance
    /// restringido no viera solo la suya. Acá el problema no existe: el modelo de lectura de esta
    /// API no tiene filtros globales, y esta prueba lo deja escrito para que nadie los agregue
    /// pensando que ayudan.
    /// </summary>
    [Fact]
    public void El_modelo_de_lectura_no_tiene_ningun_filtro_global()
    {
        var conFiltro = _b.Ctx.Model.GetEntityTypes()
            .Where(e => e.GetQueryFilter() is not null)
            .Select(e => e.ClrType.Name)
            .ToList();

        conFiltro.Should().BeEmpty(
            "una API pública no debe heredar los mecanismos de permisos de un portal interno");
    }

    [Fact]
    public async Task Devuelve_todas_las_instituciones_activas()
    {
        _b.Ctx.Instituciones.AddRange(
            BaseDePruebas.Inst("INPREMA", "Instituto Nacional de Previsión del Magisterio"),
            BaseDePruebas.Inst("IHTT", "Instituto Hondureño del Transporte Terrestre"));
        await _b.SembrarAsync();

        var r = await Consulta.InstitucionesAsync(CancellationToken.None);

        r.Should().HaveCount(2);
    }

    [Fact]
    public async Task Calcula_el_conteo_de_tramites_publicados_por_institucion()
    {
        _b.Ctx.Instituciones.Add(BaseDePruebas.Inst("INPREMA", "Instituto Nacional de Previsión del Magisterio"));
        _b.Ctx.Fichas.AddRange(
            BaseDePruebas.Ficha("1", "A"),
            BaseDePruebas.Ficha("2", "B"),
            BaseDePruebas.Ficha("3", "C", publicado: false));
        await _b.SembrarAsync();

        var r = await Consulta.InstitucionesAsync(CancellationToken.None);

        r.Single(i => i.Id == "INPREMA").ConteoTramitesPublicados.Should().Be(2);
    }

    [Fact]
    public async Task Excluye_las_instituciones_inactivas()
    {
        var activa = BaseDePruebas.Inst("INPREMA", "Instituto Nacional de Previsión del Magisterio");
        var inactiva = BaseDePruebas.Inst("IHTT", "Instituto Hondureño del Transporte Terrestre");
        inactiva.Activo = false;
        _b.Ctx.Instituciones.AddRange(activa, inactiva);
        await _b.SembrarAsync();

        var r = await Consulta.InstitucionesAsync(CancellationToken.None);

        r.Should().ContainSingle(i => i.Id == "INPREMA");
    }

    // ── Categorías ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Devuelve_las_categorias_activas_ordenadas_con_su_conteo()
    {
        var salud = new CategoriaTramite { Nombre = "Salud", Orden = 20, Activo = true };
        _b.Ctx.Categorias.AddRange(
            salud,
            new CategoriaTramite { Nombre = "Educación", Orden = 10, Activo = true },
            new CategoriaTramite { Nombre = "Inactiva", Orden = 5, Activo = false });
        await _b.SembrarAsync();

        var t = BaseDePruebas.Ficha("1", "A");
        t.CategoriaId = salud.Id;
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        var r = await Consulta.CategoriasAsync(CancellationToken.None);

        r.Should().HaveCount(2);
        r[0].Nombre.Should().Be("Educación", "Orden=10 va antes que Orden=20");
        r.Single(c => c.Id == salud.Id).ConteoTramitesPublicados.Should().Be(1);
    }
}
