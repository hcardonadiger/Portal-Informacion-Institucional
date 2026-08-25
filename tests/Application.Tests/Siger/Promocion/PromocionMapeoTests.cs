using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// El mapeo de un trámite de expediente a una ficha SIGER.
///
/// Lo que más importa acá no es que copie —eso es mecánico— sino que <b>no copie</b>: una ficha
/// promovida nace sin publicar y sin <c>IdSiger</c>, y el reparto de propiedad impide que volver
/// a pasarla borre lo que se decidió del lado de SIGER.
/// </summary>
public sealed class PromocionMapeoTests
{
    // ── Crear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void La_ficha_nace_sin_IdSiger_y_sin_publicar()
    {
        var ficha = PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "400-P01");

        ficha.IdSiger.Should().BeNull("es la marca de que no existe en SIGER");
        ficha.Codigo.Should().Be("400-P01");
        ficha.EstadoSiger.Should().Be(ReglaPublicacion.Registrado);
        ficha.Publicado.Should().BeFalse("promover y publicar son dos actos distintos");
        ficha.EsPopular.Should().BeFalse();
    }

    [Fact]
    public void Copia_la_cabecera_del_tramite_y_la_institucion_del_expediente()
    {
        var ficha = PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "400-P01");

        ficha.Nombre.Should().Be("Permiso de importación");
        ficha.Institucion.Should().Be("Aduanas");
        ficha.InstitucionId.Should().Be("ADUANAS");
        ficha.Dependencia.Should().Be("Dirección de Operaciones");
        ficha.Objetivo.Should().Be("Autorizar la importación");
        ficha.DirigidoA.Should().Be("Importadores");
        ficha.EnlacePrincipal.Should().Be("https://aduanas.gob.hn/permiso");
        ficha.CategoriaId.Should().Be(3);
        ficha.Modalidad.Should().Be(ModalidadPublica.Hibrido);
    }

    [Fact]
    public void Los_campos_que_agrego_la_Fase_8_tambien_viajan()
    {
        var t = Tramite();
        t.VigenciaDocumento  = "2 años";
        t.Temporalidad       = "Permanente";
        t.ObservacionesDiger = "Revisar el monto";
        t.EstaEnSol          = true;
        t.SolTramo           = " /permiso-importacion/ ";

        var ficha = PromocionMapeo.CrearFicha(t, Expedienteo(), "x");

        ficha.VigenciaDocumento.Should().Be("2 años");
        ficha.Temporalidad.Should().Be("Permanente");
        ficha.ObservacionesDiger.Should().Be("Revisar el monto");
        ficha.EstaEnSol.Should().BeTrue();
        ficha.SolTramo.Should().Be("permiso-importacion", "se normaliza al pasar, como en todas partes");
    }

    // ── Tiempo y costo ────────────────────────────────────────────────────────

    /// <summary>El real es lo que de verdad le va a pasar al ciudadano; el legal es el techo.</summary>
    [Fact]
    public void El_tiempo_sale_del_real_y_si_no_del_plazo_legal()
    {
        PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "x").TiempoTexto
            .Should().Be("5 días hábiles");

        var sinReal = Tramite();
        sinReal.TiempoReal = null;
        PromocionMapeo.CrearFicha(sinReal, Expedienteo(), "x").TiempoTexto
            .Should().Be("10 días por ley");
    }

    [Fact]
    public void Un_tramite_gratuito_no_lleva_texto_de_costo()
    {
        var t = Tramite();
        t.EsGratuito = true;

        var ficha = PromocionMapeo.CrearFicha(t, Expedienteo(), "x");

        ficha.CostoEsGratuito.Should().BeTrue();
        ficha.CostoTexto.Should().BeNull("«es gratuito» ya es una respuesta completa");
    }

    [Fact]
    public void Un_tramite_con_costo_arma_el_texto_con_monto_y_metodo()
    {
        var t = Tramite();
        t.EsGratuito = false;

        PromocionMapeo.CrearFicha(t, Expedienteo(), "x").CostoTexto
            .Should().Be("L. 250.00 — Depósito bancario");
    }

    /// <summary>
    /// Que haya un método de pago escrito no prueba que el trámite cueste: prueba que alguien
    /// llenó ese campo. Inferirlo pondría en el portal ciudadano un costo que nadie declaró.
    /// </summary>
    [Fact]
    public void Un_costo_sin_capturar_deja_la_ficha_incompleta_sin_inventar()
    {
        var t = Tramite();
        t.EsGratuito = null;

        var ficha = PromocionMapeo.CrearFicha(t, Expedienteo(), "x");

        ficha.CostoEsGratuito.Should().BeNull();
        ficha.CostoTexto.Should().BeNull("no se infiere el costo de un texto de pago");

        FichaPublicaCompletitud.CamposFaltantes(
            ficha.CategoriaId, ficha.Modalidad, ficha.TiempoTexto,
            ficha.CostoEsGratuito, ficha.EstaEnSol, ficha.SolUrl, ficha.SolTramo)
            .Should().Contain("costo");
    }

    // ── El reparto de propiedad ───────────────────────────────────────────────

    /// <summary>
    /// La prueba que sostiene la fase. Volver a pasar un trámite a una ficha que lleva meses
    /// publicada no puede deshacer lo que se decidió del otro lado. Sin esto, corregir una tilde
    /// en el expediente sacaría la ficha del portal del ciudadano y nadie relacionaría una cosa
    /// con la otra.
    /// </summary>
    [Fact]
    public void Actualizar_no_toca_lo_que_SIGER_y_la_curaduria_deciden()
    {
        var ficha = PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "400-P01");

        // Alguien la aprobó, la publicó, la marcó como popular y le dio identidad en SIGER.
        ficha.EstadoSiger = ReglaPublicacion.Aprobado;
        ficha.Publicado   = true;
        ficha.EsPopular   = true;
        ficha.IdSiger     = 9911;
        ficha.SolUrl      = "https://sol-viejo.gob.hn/permiso";

        var t = Tramite();
        t.NombreTramite = "Permiso de importación (corregido)";
        PromocionMapeo.CamposDelExpediente(ficha, t, Expedienteo());

        ficha.Nombre.Should().Be("Permiso de importación (corregido)", "el expediente manda el contenido");
        ficha.Codigo.Should().Be("400-P01", "el código se genera una vez y no cambia");
        ficha.IdSiger.Should().Be(9911);
        ficha.EstadoSiger.Should().Be(ReglaPublicacion.Aprobado);
        ficha.Publicado.Should().BeTrue("actualizar no puede sacar una ficha del portal");
        ficha.EsPopular.Should().BeTrue();
        ficha.SolUrl.Should().Be("https://sol-viejo.gob.hn/permiso",
            "la dirección heredada es de antes de que se compusieran y no tiene equivalente en el expediente");
    }

    /// <summary>
    /// Los pasos del proceso son de SIGER y no viajan (D-11). El expediente modela el flujo con
    /// otro vocabulario —nodos, fases, retornos— y volcarlo sobre los pasos los destruiría.
    /// </summary>
    [Fact]
    public void Los_pasos_del_proceso_no_se_tocan()
    {
        var ficha = PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "x");
        ficha.Pasos.Add(new PasoSiger { NumeroPaso = 1, Descripcion = "Recibir la solicitud" });

        PromocionMapeo.CamposDelExpediente(ficha, Tramite(), Expedienteo());

        ficha.Pasos.Should().HaveCount(1);
        ficha.Pasos[0].Descripcion.Should().Be("Recibir la solicitud");
    }

    // ── Las colecciones ───────────────────────────────────────────────────────

    /// <summary>
    /// En el expediente el orden empieza en 0 y puede tener huecos si alguien quitó filas del
    /// medio. La ficha pública los enseña como lista numerada, y un «requisito 0» o un salto del
    /// 2 al 4 se lee como un error del portal.
    /// </summary>
    [Fact]
    public void Los_requisitos_se_numeran_desde_uno_en_el_orden_del_expediente()
    {
        var reqs = PromocionMapeo.Requisitos([
            new TramiteRequisito { Orden = 5, Requisito = "Tercero" },
            new TramiteRequisito { Orden = 1, Requisito = "Segundo" },
            new TramiteRequisito { Orden = 0, Requisito = "Primero" }
        ]);

        reqs.Select(r => (r.Numero, r.Requisito))
            .Should().Equal((1, "Primero"), (2, "Segundo"), (3, "Tercero"));
    }

    [Fact]
    public void Las_filas_en_blanco_no_llegan_a_la_ficha()
    {
        PromocionMapeo.Requisitos([new TramiteRequisito { Orden = 0, Requisito = "   " }])
            .Should().BeEmpty();

        PromocionMapeo.Entregables([new ExpedienteTramiteEntregable { Orden = 0, Entregable = "" }])
            .Should().BeEmpty();

        PromocionMapeo.Lugares([new ExpedienteTramiteLugar { Orden = 0, Lugar = " " }])
            .Should().BeEmpty();
    }

    [Fact]
    public void Los_entregables_y_lugares_viajan_completos()
    {
        var g = PromocionMapeo.Entregables([
            new ExpedienteTramiteEntregable { Orden = 0, Entregable = "Constancia", Formato = "Digital", Presentacion = "Descarga" }
        ]).Single();

        g.Numero.Should().Be(1);
        g.Entregable.Should().Be("Constancia");
        g.Formato.Should().Be("Digital");
        g.Presentacion.Should().Be("Descarga");

        var l = PromocionMapeo.Lugares([
            new ExpedienteTramiteLugar { Orden = 0, Lugar = "Ventanilla", Ciudad = "Tegucigalpa", Direccion = "Bulevar", Telefonos = "2222-2222" }
        ]).Single();

        l.Numero.Should().Be(1);
        l.Ciudad.Should().Be("Tegucigalpa");
        l.Telefonos.Should().Be("2222-2222");
    }

    // ── Armado ────────────────────────────────────────────────────────────────

    private static ExpedienteTramite Tramite() => new()
    {
        TramiteIndex = 0,
        NombreTramite = "Permiso de importación",
        AreaResponsable = "Dirección de Operaciones",
        Objetivo = "Autorizar la importación",
        Descripcion = "Permite ingresar mercadería",
        Dirigido = "Importadores",
        SitioWeb = "https://aduanas.gob.hn/permiso",
        TiempoReal = "5 días hábiles",
        PlazoLegal = "10 días por ley",
        CategoriaId = 3,
        Modalidad = ModalidadPublica.Hibrido,
        TgrMonto = "L. 250.00",
        MetodoPago = "Depósito bancario"
    };

    private static Expediente Expedienteo() =>
        Expediente.Crear("EXP-100", "ADUANAS", null, null, "Aduanas", "Analista");
}
