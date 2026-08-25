using Diger.TramitesEstado.Application.Expedientes.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

/// <summary>
/// Los campos de la ficha pública dentro del trámite del expediente (Fase 8).
///
/// Por qué importan tanto: <b>D-17 invierte quién manda.</b> En cuanto una ficha SIGER queda
/// enlazada a un expediente, sus campos de contenido pasan a ser de solo lectura en la ficha y
/// solo se editan desde acá. Un campo que el expediente no sepa guardar es un campo que, a partir
/// de ese momento, nadie puede editar en ninguna parte — y nada avisaría, porque el formulario
/// seguiría aceptándolo y descartándolo en silencio.
///
/// Por eso lo que se prueba es el <b>viaje completo de ida y vuelta</b> por el mapeador, y no que
/// la entidad tenga las propiedades: tenerlas no sirve de nada si el mapeador las olvida.
/// </summary>
public class CamposFichaTramiteTests
{
    [Fact]
    public void Los_campos_de_la_ficha_sobreviven_el_viaje_de_ida_y_vuelta()
    {
        var e = Vacio();

        Aplicar(e, Tramite(0, "Licencia") with
        {
            CategoriaId        = 7,
            EsGratuito         = false,
            VigenciaDocumento  = "2 años",
            Temporalidad       = "Permanente",
            ObservacionesDiger = "Revisar el cobro con la institución",
            EstaEnSol          = true,
            SolTramo           = "licencia-de-operacion"
        });

        var vuelta = ExpedienteMapper.ToInputDto(e).Tramites.Single();

        vuelta.CategoriaId.Should().Be(7);
        vuelta.EsGratuito.Should().BeFalse();
        vuelta.VigenciaDocumento.Should().Be("2 años");
        vuelta.Temporalidad.Should().Be("Permanente");
        vuelta.ObservacionesDiger.Should().Be("Revisar el cobro con la institución");
        vuelta.EstaEnSol.Should().BeTrue();
        vuelta.SolTramo.Should().Be("licencia-de-operacion");
    }

    /// <summary>
    /// El costo tiene tres estados y el tercero es «no se sabe». Si el viaje convirtiera
    /// <c>null</c> en <c>false</c>, una ficha sin capturar aparecería como «tiene costo», y si lo
    /// convirtiera en <c>true</c> le diría al ciudadano que no pague algo que sí se paga.
    /// </summary>
    [Fact]
    public void No_haber_capturado_el_costo_no_se_vuelve_ni_gratuito_ni_con_costo()
    {
        var e = Vacio();
        Aplicar(e, Tramite(0, "Sin capturar"));

        ExpedienteMapper.ToInputDto(e).Tramites.Single().EsGratuito.Should().BeNull();
    }

    [Fact]
    public void El_tramo_de_SOL_se_guarda_normalizado()
    {
        var e = Vacio();
        Aplicar(e, Tramite(0, "Licencia") with { SolTramo = "  /licencia/  " });

        e.Tramites.Single().SolTramo.Should().Be("licencia",
            "se normaliza en el mapeador, no en cada pantalla");
    }

    // ── La modalidad ──────────────────────────────────────────────────────────

    /// <summary>
    /// La modalidad entra como texto libre y se normaliza en un solo punto, así que da igual si
    /// viene del formulario, de una importación o de una carga vieja.
    /// </summary>
    [Theory]
    [InlineData("En línea",             "Virtual")]
    [InlineData("En linea",             "Virtual")]
    [InlineData("En línea (total)",     "Virtual")]
    [InlineData("Trámite en línea",     "Virtual")]
    [InlineData("En línea, Presencial", "Hibrido")]
    [InlineData("En línea / Presencial","Hibrido")]
    [InlineData("Presencial",           "Presencial")]
    public void La_modalidad_de_texto_libre_se_normaliza_al_catalogo(string escrito, string esperado)
    {
        var e = Vacio();
        Aplicar(e, Tramite(0, "Licencia") with { Modalidad = escrito });

        e.Tramites.Single().Modalidad.Should().Be(esperado);
    }

    /// <summary>
    /// El matiz que el catálogo cerrado pierde se conserva aparte. «En línea (total)» y
    /// «En línea» acaban las dos en Virtual, y ese «(total)» lo escribió alguien queriendo decir
    /// algo; después de convertir no hay forma de recuperarlo.
    /// </summary>
    [Fact]
    public void El_texto_original_de_la_modalidad_se_conserva()
    {
        var e = Vacio();
        Aplicar(e, Tramite(0, "Licencia") with { Modalidad = "En línea (total)" });

        var t = e.Tramites.Single();
        t.Modalidad.Should().Be("Virtual");
        t.ModalidadDetalle.Should().Be("En línea (total)");
    }

    /// <summary>
    /// La trampa que tiene esto: el mapeador normaliza en <b>cada</b> guardado. Si un valor que ya
    /// es del catálogo no sobreviviera, editar cualquier otra cosa del trámite le borraría la
    /// modalidad, y nadie relacionaría una cosa con la otra.
    /// </summary>
    [Fact]
    public void Guardar_dos_veces_no_pierde_la_modalidad_ya_normalizada()
    {
        var e = Vacio();
        Aplicar(e, Tramite(0, "Licencia") with { Modalidad = "En línea, Presencial" });
        e.Tramites.Single().Modalidad.Should().Be("Hibrido");

        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e));
        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e));

        var t = e.Tramites.Single();
        t.Modalidad.Should().Be("Hibrido", "«Hibrido» no dice «linea» ni «presencial»");
        t.ModalidadDetalle.Should().Be("En línea, Presencial", "el original tampoco se pisa");
    }

    /// <summary>Un texto que no dice nada reconocible deja la modalidad vacía en vez de
    /// inventarla; una ficha sin modalidad se declara incompleta y alguien la revisa.</summary>
    [Fact]
    public void Un_texto_que_no_dice_nada_deja_la_modalidad_vacia_pero_guarda_lo_escrito()
    {
        var e = Vacio();
        Aplicar(e, Tramite(0, "Licencia") with { Modalidad = "Depende del caso" });

        var t = e.Tramites.Single();
        t.Modalidad.Should().BeNull();
        t.ModalidadDetalle.Should().Be("Depende del caso", "lo que alguien escribió no se tira");
    }

    // ── Entregables y lugares ─────────────────────────────────────────────────

    [Fact]
    public void Los_entregables_y_lugares_sobreviven_el_viaje_de_ida_y_vuelta()
    {
        var e = Vacio();
        var dto = ExpedienteMapper.ToInputDto(e) with
        {
            Tramites    = [Tramite(0, "Licencia")],
            Entregables = [new EntregableInput(0, 0, "Constancia", "Digital", "Descarga")],
            Lugares     = [new LugarInput(0, 0, "Ventanilla central", "Tegucigalpa", "Bulevar", "2222-2222")]
        };
        ExpedienteMapper.Aplicar(e, dto);

        var vuelta = ExpedienteMapper.ToInputDto(e);

        var g = vuelta.Entregables!.Single();
        g.Entregable.Should().Be("Constancia");
        g.Formato.Should().Be("Digital");
        g.Presentacion.Should().Be("Descarga");

        var l = vuelta.Lugares!.Single();
        l.Lugar.Should().Be("Ventanilla central");
        l.Ciudad.Should().Be("Tegucigalpa");
        l.Telefonos.Should().Be("2222-2222");
    }

    /// <summary>
    /// Las dos colecciones nuevas siguen la regla de las diez que ya existen: cada guardado las
    /// reemplaza en bloque. Sin esto, guardar tres veces dejaría tres copias de cada entregable
    /// y la ficha publicada mostraría el mismo documento repetido.
    /// </summary>
    [Fact]
    public void Guardar_de_nuevo_reemplaza_los_hijos_en_vez_de_acumularlos()
    {
        var e = Vacio();
        var dto = ExpedienteMapper.ToInputDto(e) with
        {
            Tramites    = [Tramite(0, "Licencia")],
            Entregables = [new EntregableInput(0, 0, "Constancia", null, null)],
            Lugares     = [new LugarInput(0, 0, "Ventanilla", null, null, null)]
        };

        ExpedienteMapper.Aplicar(e, dto);
        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e));
        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e));

        e.Entregables.Should().HaveCount(1);
        e.Lugares.Should().HaveCount(1);
    }

    /// <summary>Una fila en blanco del formulario es una fila que alguien empezó y no llenó, no
    /// un dato. Se filtra igual que en los requisitos.</summary>
    [Fact]
    public void Las_filas_en_blanco_no_se_guardan()
    {
        var e = Vacio();
        var dto = ExpedienteMapper.ToInputDto(e) with
        {
            Tramites    = [Tramite(0, "Licencia")],
            Entregables = [new EntregableInput(0, 0, "   ", null, null)],
            Lugares     = [new LugarInput(0, 0, "", null, null, null)]
        };

        ExpedienteMapper.Aplicar(e, dto);

        e.Entregables.Should().BeEmpty();
        e.Lugares.Should().BeEmpty();
    }

    // ── Armado ────────────────────────────────────────────────────────────────

    private static Expediente Vacio() =>
        Expediente.Crear("EXP-001", "SALUD", null, null, "SECRETARIA DE SALUD", "Analista");

    private static void Aplicar(Expediente e, TramiteInput t) =>
        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e) with { Tramites = [t] });

    private static TramiteInput Tramite(int indice, string nombre) => new(
        indice, nombre, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null, null);
}
