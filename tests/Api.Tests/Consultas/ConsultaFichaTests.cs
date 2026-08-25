using Diger.TramitesEstado.Api.Consultas;
using Diger.TramitesEstado.Api.Contrato;
using Diger.TramitesEstado.Api.Lectura;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diger.TramitesEstado.Api.Tests.Consultas;

public sealed class ConsultaFichaTests : IDisposable
{
    private readonly BaseDePruebas _b = new();

    /// <summary>Host fijo y conocido para que las direcciones compuestas se puedan afirmar
    /// letra por letra. En producción sale de la sección «Sol» de appsettings.</summary>
    private static readonly IOptions<SolOptions> Sol =
        Options.Create(new SolOptions { UrlBase = "https://sol.pdihonduras.gob.hn" });

    public void Dispose() => _b.Dispose();

    private ConsultaFicha Consulta => new(_b.Ctx, Sol);

    [Fact]
    public async Task Un_codigo_inexistente_devuelve_null()
    {
        var r = await Consulta.EjecutarAsync("NO-EXISTE", CancellationToken.None);

        r.Should().BeNull();
    }

    [Fact]
    public async Task Un_tramite_sin_publicar_devuelve_null()
    {
        _b.Ctx.Fichas.Add(BaseDePruebas.Ficha("100-001", "Sin publicar", publicado: false));
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("100-001", CancellationToken.None);

        r.Should().BeNull("una API pública no distingue «no existe» de «no publicado»");
    }

    [Fact]
    public async Task Un_tramite_publicado_trae_sus_colecciones_hijas()
    {
        var t = BaseDePruebas.Ficha("100-001", "Trámite completo");
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        _b.Ctx.Pasos.AddRange(
            new PasoSiger { TramiteSigerId = t.Id, NumeroPaso = 2, Descripcion = "Segundo paso" },
            new PasoSiger { TramiteSigerId = t.Id, NumeroPaso = 1, Descripcion = "Primer paso" });
        _b.Ctx.Requisitos.Add(new RequisitoSiger { TramiteSigerId = t.Id, Numero = 1, Requisito = "DNI" });
        _b.Ctx.LugaresAtencion.Add(new LugarAtencionSiger { TramiteSigerId = t.Id, Numero = 1, Lugar = "Oficina central" });
        _b.Ctx.Enlaces.Add(new EnlaceSiger { TramiteSigerId = t.Id, Numero = 1, Url = "https://ejemplo.gob.hn" });
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("100-001", CancellationToken.None);

        r.Should().NotBeNull();
        r!.Pasos.Should().HaveCount(2);
        r.Pasos[0].Numero.Should().Be(1, "los pasos van ordenados por número");
        r.Requisitos.Should().ContainSingle(x => x.Requisito == "DNI");
        r.LugaresAtencion.Should().ContainSingle(x => x.Lugar == "Oficina central");
        r.Enlaces.Should().ContainSingle(x => x.Url == "https://ejemplo.gob.hn");
    }

    /// <summary>Las colecciones hijas de OTRA ficha no se cuelan. Con navegaciones esto lo
    /// garantizaba EF; acá los hijos se traen por TramiteSigerId, así que conviene comprobarlo.</summary>
    [Fact]
    public async Task No_se_cuelan_los_hijos_de_otra_ficha()
    {
        var mia  = BaseDePruebas.Ficha("100-001", "La mía");
        var otra = BaseDePruebas.Ficha("100-002", "La otra");
        _b.Ctx.Fichas.AddRange(mia, otra);
        await _b.SembrarAsync();

        _b.Ctx.Pasos.AddRange(
            new PasoSiger { TramiteSigerId = mia.Id,  NumeroPaso = 1, Descripcion = "Mío" },
            new PasoSiger { TramiteSigerId = otra.Id, NumeroPaso = 1, Descripcion = "Ajeno" });
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("100-001", CancellationToken.None);

        r!.Pasos.Should().ContainSingle(p => p.Descripcion == "Mío");
    }

    [Fact]
    public async Task La_ultima_revision_usa_updated_at_cuando_existe()
    {
        var actualizado = new DateTime(2026, 6, 1);
        var t = BaseDePruebas.Ficha("100-001", "Con revisión");
        t.CreatedAt = new DateTime(2025, 1, 1);
        t.UltimaModificacion = new DateTime(2025, 6, 1);
        t.UpdatedAt = actualizado;
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("100-001", CancellationToken.None);

        r!.UltimaRevision.Should().Be(actualizado,
            "la fecha que se publica es la que sella el sistema, no el campo editable del formulario");
    }

    [Fact]
    public async Task Sin_updated_at_cae_en_la_ultima_modificacion_heredada()
    {
        var legado = new DateTime(2022, 9, 1);
        var t = BaseDePruebas.Ficha("100-001", "Legado");
        t.CreatedAt = new DateTime(2020, 1, 1);
        t.UltimaModificacion = legado;
        t.UpdatedAt = null;
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("100-001", CancellationToken.None);

        r!.UltimaRevision.Should().Be(legado);
    }

    // ── La dirección en SOL ───────────────────────────────────────────────────

    /// <summary>
    /// El campo <c>solUrl</c> <b>sigue siendo una URL absoluta</b> aunque la ficha ya solo guarde
    /// el tramo. Si esta API pasara a emitir el tramo suelto, HondurasÁgil pintaría enlaces
    /// relativos contra su propio dominio y los botones de «hacer el trámite en línea» llevarían
    /// a ninguna parte, sin error en ningún registro.
    /// </summary>
    [Fact]
    public async Task Con_tramo_compone_la_direccion_absoluta_con_la_ruta_de_la_institucion()
    {
        _b.Ctx.Instituciones.Add(BaseDePruebas.Inst("CONSUCOOP", "Consejo Supervisor de Cooperativas"));
        var t = BaseDePruebas.Ficha("506-010", "Licencia", institucionId: "CONSUCOOP", institucion: "CONSUCOOP");
        t.EstaEnSol = true; t.SolTramo = "licencia-de-operacion";
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("506-010", CancellationToken.None);

        r!.SolUrl.Should().Be("https://sol.pdihonduras.gob.hn/CONSUCOOP/licencia-de-operacion");
    }

    /// <summary>Corregir la ruta de la institución cambia la dirección de todos sus trámites a la
    /// vez, que es justo para lo que existe esa columna.</summary>
    [Fact]
    public async Task Con_ruta_corregida_la_usa_en_lugar_de_la_sigla()
    {
        _b.Ctx.Instituciones.Add(BaseDePruebas.Inst("CANATURHIHT", "CANATURH / IHT", rutaSol: "canaturh"));
        var t = BaseDePruebas.Ficha("700-001", "Registro", institucionId: "CANATURHIHT", institucion: "CANATURH / IHT");
        t.EstaEnSol = true; t.SolTramo = "registro";
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("700-001", CancellationToken.None);

        r!.SolUrl.Should().Be("https://sol.pdihonduras.gob.hn/canaturh/registro",
            "la sigla dice CANATURHIHT, pero la ruta real de SOL es otra");
    }

    /// <summary>Sin tramo se emite la dirección heredada tal cual.</summary>
    [Fact]
    public async Task Sin_tramo_emite_la_direccion_heredada_sin_tocarla()
    {
        var t = BaseDePruebas.Ficha("400-002", "Viejo", institucionId: "ADUANAS", institucion: "ADUANAS");
        t.EstaEnSol = true; t.SolUrl = "https://otro.sitio.hn/x?y=1";
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("400-002", CancellationToken.None);

        r!.SolUrl.Should().Be("https://otro.sitio.hn/x?y=1");
    }

    /// <summary>El detalle sirve la columna de PortalDigital, igual que el resumen.</summary>
    [Fact]
    public async Task El_detalle_sirve_la_ficha_completa_que_dice_portaldigital()
    {
        var t = BaseDePruebas.Ficha("100-001", "Completa");
        t.FichaCompleta = true;
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync("100-001", CancellationToken.None);

        r!.FichaCompleta.Should().BeTrue();
    }
}
