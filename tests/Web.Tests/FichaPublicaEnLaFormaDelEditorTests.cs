using Diger.TramitesEstado.Application.Expedientes.Common;
using Diger.TramitesEstado.Web.Pages.Expedientes;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El viaje de los campos de la ficha pública por la forma JSON del editor (Fase 8).
///
/// Entre el formulario y la base hay dos traducciones: <c>OriginalShapeMapper</c> convierte la
/// forma que maneja el JavaScript en el DTO de aplicación, y <c>ExpedienteMapper</c> convierte
/// ese DTO en entidades. Un campo que se pierda en cualquiera de las dos <b>no da error</b>: el
/// formulario lo acepta, el guardado responde bien y el dato simplemente no está.
///
/// Y desde D-17 eso importa el doble: cuando la ficha SIGER queda enlazada a un expediente, sus
/// campos se vuelven de solo lectura y este es el único camino que les queda.
/// </summary>
public sealed class FichaPublicaEnLaFormaDelEditorTests
{
    [Fact]
    public void Los_campos_de_la_ficha_sobreviven_el_viaje_por_la_forma_del_editor()
    {
        var ida = ConUnTramite(new Dictionary<string, string?>
        {
            ["nombre_tramite"]      = "Licencia de operación",
            ["modalidad"]           = "Hibrido",
            ["modalidad_detalle"]   = "En línea salvo la firma",
            ["categoria_id"]        = "7",
            ["es_gratuito"]         = "false",
            ["vigencia_documento"]  = "2 años",
            ["temporalidad"]        = "Permanente",
            ["observaciones_diger"] = "Pendiente confirmar el monto",
            ["esta_en_sol"]         = "true",
            ["sol_tramo"]           = "licencia-de-operacion"
        });

        var dto = OriginalShapeMapper.ToInput(ida, "IDP");
        var t = dto.Tramites.Single();

        t.Modalidad.Should().Be("Hibrido");
        t.ModalidadDetalle.Should().Be("En línea salvo la firma");
        t.CategoriaId.Should().Be(7);
        t.EsGratuito.Should().BeFalse();
        t.VigenciaDocumento.Should().Be("2 años");
        t.Temporalidad.Should().Be("Permanente");
        t.ObservacionesDiger.Should().Be("Pendiente confirmar el monto");
        t.EstaEnSol.Should().BeTrue();
        t.SolTramo.Should().Be("licencia-de-operacion");

        // Y de vuelta, que es como el editor se repuebla al abrir un expediente guardado.
        var vuelta = OriginalShapeMapper.FromInput(dto).Tramites.Single();

        vuelta["categoria_id"].Should().Be("7");
        vuelta["es_gratuito"].Should().Be("false");
        vuelta["esta_en_sol"].Should().Be("true");
        vuelta["sol_tramo"].Should().Be("licencia-de-operacion");
        vuelta["modalidad_detalle"].Should().Be("En línea salvo la firma");
    }

    /// <summary>
    /// El costo tiene tres estados. Un desplegable en blanco significa «no se ha capturado», que
    /// no es «no». Si el viaje lo convirtiera en <c>false</c>, la ficha diría que tiene costo sin
    /// que nadie lo haya dicho.
    /// </summary>
    [Fact]
    public void Un_costo_sin_capturar_no_se_vuelve_ni_si_ni_no()
    {
        var dto = OriginalShapeMapper.ToInput(
            ConUnTramite(new Dictionary<string, string?> { ["nombre_tramite"] = "X", ["es_gratuito"] = "" }), "IDP");

        dto.Tramites.Single().EsGratuito.Should().BeNull();
    }

    [Fact]
    public void Los_entregables_y_lugares_sobreviven_el_viaje_por_la_forma_del_editor()
    {
        var ida = ConUnTramite(new Dictionary<string, string?> { ["nombre_tramite"] = "Licencia" });
        ida.EntregablesTram = [[new() { Entregable = "Constancia", Formato = "Digital", Presentacion = "Descarga" }]];
        ida.LugaresTram     = [[new() { Lugar = "Ventanilla", Ciudad = "Tegucigalpa", Direccion = "Bulevar", Telefonos = "2222-2222" }]];

        var dto = OriginalShapeMapper.ToInput(ida, "IDP");

        dto.Entregables!.Single().Entregable.Should().Be("Constancia");
        dto.Lugares!.Single().Telefonos.Should().Be("2222-2222");

        var vuelta = OriginalShapeMapper.FromInput(dto);

        vuelta.EntregablesTram.Single().Single().Entregable.Should().Be("Constancia");
        vuelta.LugaresTram.Single().Single().Ciudad.Should().Be("Tegucigalpa");
    }

    /// <summary>
    /// Un expediente guardado antes de la Fase 8 no trae ninguna de las claves nuevas. Abrirlo no
    /// puede reventar: tiene que quedar simplemente sin capturar.
    /// </summary>
    [Fact]
    public void Un_expediente_viejo_sin_los_campos_nuevos_se_abre_sin_romperse()
    {
        var dto = OriginalShapeMapper.ToInput(
            ConUnTramite(new Dictionary<string, string?> { ["nombre_tramite"] = "Trámite viejo" }), "IDP");

        var t = dto.Tramites.Single();
        t.CategoriaId.Should().BeNull();
        t.EsGratuito.Should().BeNull();
        t.EstaEnSol.Should().BeFalse();
        t.SolTramo.Should().BeNull();
        dto.Entregables.Should().BeEmpty();
        dto.Lugares.Should().BeEmpty();
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private static OriginalExpedienteDto ConUnTramite(Dictionary<string, string?> campos) => new()
    {
        Inst = "Instituto de Prueba",
        Analista = "Analista",
        NumTramites = 1,
        Tramites = [campos]
    };
}
