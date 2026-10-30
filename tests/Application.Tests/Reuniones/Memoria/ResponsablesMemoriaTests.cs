using Diger.TramitesEstado.Application.Reuniones.Memoria;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Memoria;

/// <summary>
/// Quién puede ser responsable de un acuerdo de la memoria: las personas que están en la lista de
/// asistencia y las instituciones con que se registraron (más la contraparte de la reunión). Un
/// invitado de Outlook que no llegó no cuenta. Lo que la IA escribe se ajusta a esa lista.
/// </summary>
public class ResponsablesMemoriaTests
{
    private static readonly IReadOnlyList<ResponsableValido> Validos = ResponsablesMemoria.De(
    [
        new("Wendy Elizabeth Oviedo Martinez", "wendy.oviedo@sgjd.gob.hn", "Secretaría de Gobernación", EnLaLista: true),
        new("Elias Rosales", "elias.rosales@sgjd.gob.hn", "Secretaría de Gobernación", EnLaLista: true),
        new("Henry Cardona", "hcardona@diger.gob.hn", "DIGER", EnLaLista: true),
        new("tomas.fantl@undp.org", "tomas.fantl@undp.org", null, EnLaLista: false),
    ], institucionReunion: "PNUD");

    [Fact]
    public void Solo_la_lista_de_asistencia_y_sus_instituciones()
    {
        Validos.Select(v => v.Nombre).Should().BeEquivalentTo(
            ["Wendy Elizabeth Oviedo Martinez", "Elias Rosales", "Henry Cardona", "Secretaría de Gobernación", "DIGER", "PNUD"]);
        Validos.Where(v => v.EsInstitucion).Select(v => v.Nombre)
            .Should().BeEquivalentTo(["Secretaría de Gobernación", "DIGER", "PNUD"]);
    }

    [Theory]
    [InlineData("Wendy Elizabeth Oviedo Martinez", "Wendy Elizabeth Oviedo Martinez")]
    [InlineData("Wendy Oviedo (Gobernación)", "Wendy Elizabeth Oviedo Martinez")]
    [InlineData("wendy oviedo", "Wendy Elizabeth Oviedo Martinez")]
    [InlineData("Elías Rosales", "Elias Rosales")]
    [InlineData("elias.rosales@sgjd.gob.hn", "Elias Rosales")]
    [InlineData("secretaria de gobernacion", "Secretaría de Gobernación")]
    [InlineData("PNUD", "PNUD")]
    public void Ajusta_lo_que_escribe_la_IA_al_nombre_de_la_lista(string escrito, string esperado)
    {
        ResponsablesMemoria.Resolver(escrito, Validos).Should().Be(esperado);
    }

    [Theory]
    [InlineData("tomas.fantl@undp.org")]   // invitado que no se registró
    [InlineData("Jorge Godoy")]            // habló en la reunión, pero no llenó la lista
    [InlineData("Henry")]                  // ambiguo o insuficiente: un solo nombre de pila no alcanza
    [InlineData("")]
    [InlineData(null)]
    public void Lo_que_no_esta_en_la_lista_queda_sin_responsable(string? escrito)
    {
        ResponsablesMemoria.Resolver(escrito, Validos).Should().BeNull();
    }
}
