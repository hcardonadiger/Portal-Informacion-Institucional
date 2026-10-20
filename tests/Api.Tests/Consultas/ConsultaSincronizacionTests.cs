using Diger.TramitesEstado.Api.Consultas;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Api.Tests.Consultas;

public sealed class ConsultaSincronizacionTests : IDisposable
{
    private readonly BaseDePruebas _b = new();

    public void Dispose() => _b.Dispose();

    private ConsultaSincronizacion Consulta => new(_b.Ctx);

    [Fact]
    public async Task Codigos_publicados_solo_trae_los_publicados()
    {
        _b.Ctx.Fichas.AddRange(
            BaseDePruebas.Ficha("PUB-1", "A"),
            BaseDePruebas.Ficha("NOPUB-1", "B", publicado: false));
        await _b.SembrarAsync();

        var r = await Consulta.CodigosPublicadosAsync(CancellationToken.None);

        r.Should().BeEquivalentTo(["PUB-1"]);
    }

    [Fact]
    public async Task Cambios_devuelve_solo_publicados_modificados_desde_la_fecha()
    {
        var corte = new DateTime(2026, 6, 1);

        var reciente = BaseDePruebas.Ficha("RECIENTE", "A");
        reciente.CreatedAt = new DateTime(2026, 1, 1); reciente.UpdatedAt = new DateTime(2026, 7, 1);

        var viejo = BaseDePruebas.Ficha("VIEJO", "B");
        viejo.CreatedAt = new DateTime(2025, 1, 1); viejo.UpdatedAt = new DateTime(2025, 2, 1);

        var sinPublicar = BaseDePruebas.Ficha("NO-PUBLICADO", "C", publicado: false);
        sinPublicar.CreatedAt = new DateTime(2026, 1, 1); sinPublicar.UpdatedAt = new DateTime(2026, 7, 1);

        _b.Ctx.Fichas.AddRange(reciente, viejo, sinPublicar);
        await _b.SembrarAsync();

        var r = await Consulta.CambiosAsync(corte, CancellationToken.None);

        r.Codigos.Should().BeEquivalentTo(["RECIENTE"]);
    }

    [Fact]
    public async Task Sin_updated_at_usa_created_at_como_respaldo()
    {
        var corte = new DateTime(2026, 6, 1);
        var nuevo = BaseDePruebas.Ficha("NUEVO-SIN-TOCAR", "A");
        nuevo.CreatedAt = new DateTime(2026, 7, 1); nuevo.UpdatedAt = null;
        _b.Ctx.Fichas.Add(nuevo);
        await _b.SembrarAsync();

        var r = await Consulta.CambiosAsync(corte, CancellationToken.None);

        r.Codigos.Should().Contain("NUEVO-SIN-TOCAR");
    }

    /// <summary>
    /// La hora que viaja en la respuesta es la del servidor y se toma ANTES de consultar. El
    /// consumidor la guarda y la manda como «desde» la próxima vez: si se tomara después, un
    /// cambio ocurrido durante la consulta quedaría por debajo del sello y no se pediría nunca.
    /// </summary>
    [Fact]
    public async Task La_hora_generada_es_del_servidor_y_no_va_al_futuro()
    {
        var antes = DateTime.UtcNow;
        var r = await Consulta.CambiosAsync(new DateTime(2020, 1, 1), CancellationToken.None);
        var despues = DateTime.UtcNow;

        r.GeneradoEl.Should().BeOnOrAfter(antes).And.BeOnOrBefore(despues);
    }
}
