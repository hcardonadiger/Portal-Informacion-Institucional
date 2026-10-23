using Diger.TramitesEstado.Application.Reuniones.Outlook;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Outlook;

/// <summary>La cita se arma en hora local de Honduras (UTC−6, sin horario de verano): «ahora» tiene
/// que estar en la misma hora para compararlos, sea cual sea la zona del servidor.</summary>
public class HoraHondurasTests
{
    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    [Fact]
    public void Ahora_es_la_hora_de_Honduras_aunque_en_UTC_ya_sea_otro_dia()
    {
        var ahora = HoraHonduras.Ahora(new RelojFijo(new DateTimeOffset(2026, 10, 24, 3, 0, 0, TimeSpan.Zero)));

        ahora.Should().Be(new DateTime(2026, 10, 23, 21, 0, 0));
        ahora.Kind.Should().Be(DateTimeKind.Unspecified, "se compara con el Inicio/Fin de la cita, que no llevan zona");
    }

    [Fact]
    public void En_julio_sigue_siendo_UTC_menos_6()
    {
        HoraHonduras.Ahora(new RelojFijo(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)))
            .Should().Be(new DateTime(2026, 7, 1, 6, 0, 0));
    }
}
