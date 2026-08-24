using Diger.TramitesEstado.Application.Expedientes.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

/// <summary>
/// La identidad estable de un trámite dentro de su expediente.
/// </summary>
/// <remarks>
/// Estas pruebas cubren el defecto que motivó la Fase 3: nada de lo que había servía para
/// recordar algo de un trámite entre un guardado y el siguiente. El <c>Id</c> cambia porque
/// guardar borra y reinserta todos los hijos, y el <c>TramiteIndex</c> se asigna por posición
/// en el arreglo del formulario, así que quitar uno del medio renumera los de atrás.
/// </remarks>
public class ClaveEstableTramiteTests
{
    /// <summary>
    /// El caso que rompía la conciliación. Al quitar el trámite del medio, los índices se
    /// recorren —el tercero pasa a ser el segundo— pero cada clave se queda con su trámite.
    /// Es lo que permite que una decisión guardada siga apuntando a lo que se decidió.
    /// </summary>
    [Fact]
    public void QuitarElTramiteDelMedio_RenumeraLosIndicesPeroNoLasClaves()
    {
        var (e, claves) = ExpedienteCon("Alfa", "Beta", "Gamma");

        var dto = ExpedienteMapper.ToInputDto(e);
        var sinElDelMedio = dto with
        {
            Tramites = [.. dto.Tramites.Where(t => t.NombreTramite != "Beta")
                                       .Select((t, i) => t with { TramiteIndex = i })]
        };

        ExpedienteMapper.Aplicar(e, sinElDelMedio);

        var quedan = e.Tramites.OrderBy(t => t.TramiteIndex).ToList();
        quedan.Select(t => t.NombreTramite).Should().Equal("Alfa", "Gamma");
        quedan.Select(t => t.TramiteIndex).Should().Equal(0, 1);

        // Gamma cambió de índice 2 a 1 y conservó su identidad. Sin esto, una decisión guardada
        // sobre Gamma pasaría a describir a otro trámite.
        quedan[0].ClaveEstable.Should().Be(claves["Alfa"]);
        quedan[1].ClaveEstable.Should().Be(claves["Gamma"]);
    }

    /// <summary>Reordenar por arrastre tampoco debe mover las identidades.</summary>
    [Fact]
    public void ReordenarLosTramites_NoIntercambiaLasClaves()
    {
        var (e, claves) = ExpedienteCon("Alfa", "Beta");

        var dto = ExpedienteMapper.ToInputDto(e);
        var alReves = dto with
        {
            Tramites = [.. dto.Tramites.OrderByDescending(t => t.TramiteIndex)
                                       .Select((t, i) => t with { TramiteIndex = i })]
        };

        ExpedienteMapper.Aplicar(e, alReves);

        var quedan = e.Tramites.OrderBy(t => t.TramiteIndex).ToList();
        quedan[0].NombreTramite.Should().Be("Beta");
        quedan[0].ClaveEstable.Should().Be(claves["Beta"]);
        quedan[1].ClaveEstable.Should().Be(claves["Alfa"]);
    }

    /// <summary>
    /// Guardar sin tocar nada no debe reescribir las claves. Si <c>Aplicar</c> generara una nueva
    /// cada vez, la identidad duraría hasta el siguiente guardado y no serviría para nada.
    /// </summary>
    [Fact]
    public void GuardarSinCambios_ConservaLasClaves()
    {
        var (e, claves) = ExpedienteCon("Alfa", "Beta");

        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e));
        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e));

        foreach (var t in e.Tramites)
            t.ClaveEstable.Should().Be(claves[t.NombreTramite]);
    }

    [Fact]
    public void UnTramiteNuevo_RecibeUnaClavePropia()
    {
        var (e, claves) = ExpedienteCon("Alfa");

        var dto = ExpedienteMapper.ToInputDto(e);
        var conUnoNuevo = dto with
        {
            Tramites = [.. dto.Tramites, dto.Tramites[0] with
            {
                TramiteIndex = 1, NombreTramite = "Recien llegado", ClaveEstable = null
            }]
        };

        ExpedienteMapper.Aplicar(e, conUnoNuevo);

        var nuevo = e.Tramites.Single(t => t.NombreTramite == "Recien llegado");
        nuevo.ClaveEstable.Should().NotBeEmpty();
        nuevo.ClaveEstable.Should().NotBe(claves["Alfa"]);
    }

    /// <summary>Dos trámites nunca comparten identidad, ni recién creados.</summary>
    [Fact]
    public void VariosTramitesNuevos_NoCompartenClave()
    {
        var (e, _) = ExpedienteCon("Alfa", "Beta", "Gamma");

        e.Tramites.Select(t => t.ClaveEstable).Distinct().Should().HaveCount(3);
        e.Tramites.Should().OnlyContain(t => t.ClaveEstable != Guid.Empty);
    }

    // ── Armado ────────────────────────────────────────────────────────────────

    /// <summary>Crea el expediente pasando por <c>Aplicar</c>, que es quien reparte las claves.</summary>
    private static (Expediente, Dictionary<string, Guid>) ExpedienteCon(params string[] nombres)
    {
        var e = Expediente.Crear("EXP-001", "SALUD", null, null, "SECRETARIA DE SALUD", "Analista");

        var dto = ExpedienteMapper.ToInputDto(e) with
        {
            Tramites = [.. nombres.Select((n, i) => Tramite(i, n))]
        };
        ExpedienteMapper.Aplicar(e, dto);

        return (e, e.Tramites.ToDictionary(t => t.NombreTramite, t => t.ClaveEstable));
    }

    private static TramiteInput Tramite(int indice, string nombre) => new(
        indice, nombre, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null, null);
}
