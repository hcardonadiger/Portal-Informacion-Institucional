using Diger.TramitesEstado.Api.Consultas;
using Diger.TramitesEstado.Api.Lectura;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Api.Tests.Consultas;

public sealed class ConsultaCatalogoTests : IDisposable
{
    private readonly BaseDePruebas _b = new();

    public void Dispose() => _b.Dispose();

    private ConsultaCatalogo Consulta => new(_b.Ctx);

    [Fact]
    public async Task Solo_devuelve_tramites_publicados()
    {
        _b.Ctx.Fichas.AddRange(
            BaseDePruebas.Ficha("100-001", "Trámite publicado"),
            BaseDePruebas.Ficha("100-002", "Trámite sin publicar", publicado: false));
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync(new CatalogoFiltros(), CancellationToken.None);

        r.Total.Should().Be(1);
        r.Items.Should().ContainSingle(t => t.Codigo == "100-001");
    }

    [Fact]
    public async Task Filtra_por_institucion()
    {
        _b.Ctx.Fichas.AddRange(
            BaseDePruebas.Ficha("100-001", "De INPREMA", institucionId: "INPREMA"),
            BaseDePruebas.Ficha("100-002", "De IHTT", institucionId: "IHTT"));
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync(new CatalogoFiltros(Institucion: "IHTT"), CancellationToken.None);

        r.Items.Should().ContainSingle(t => t.Codigo == "100-002");
    }

    [Fact]
    public async Task Modalidad_virtual_tambien_incluye_los_hibridos()
    {
        var v = BaseDePruebas.Ficha("100-001", "Trámite virtual");    v.Modalidad = "Virtual";
        var h = BaseDePruebas.Ficha("100-002", "Trámite híbrido");    h.Modalidad = "Hibrido";
        var p = BaseDePruebas.Ficha("100-003", "Trámite presencial"); p.Modalidad = "Presencial";
        _b.Ctx.Fichas.AddRange(v, h, p);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync(new CatalogoFiltros(Modalidad: "Virtual"), CancellationToken.None);

        r.Items.Select(t => t.Codigo).Should().BeEquivalentTo(["100-001", "100-002"]);
    }

    [Fact]
    public async Task Solo_gratuitos_filtra_por_costo_es_gratuito_verdadero()
    {
        var gratuito = BaseDePruebas.Ficha("100-001", "Gratuito");        gratuito.CostoEsGratuito = true;
        var pago     = BaseDePruebas.Ficha("100-002", "Con costo");       pago.CostoEsGratuito = false;
        var sinDato  = BaseDePruebas.Ficha("100-003", "Sin dato de costo");
        _b.Ctx.Fichas.AddRange(gratuito, pago, sinDato);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync(new CatalogoFiltros(SoloGratuitos: true), CancellationToken.None);

        r.Items.Should().ContainSingle(t => t.Codigo == "100-001");
    }

    // ── El filtro de fichas completas ─────────────────────────────────────────
    //
    // Antes acá había dos pruebas que comprobaban la REGLA: que exigiera categoría, modalidad,
    // tiempo y costo, y que exigiera el enlace cuando la ficha estuviera en SOL. Esa regla ya no
    // vive en esta API: la decide PortalDigital y la publica en una columna.
    //
    // Lo que queda por comprobar acá es lo único de lo que esta API responde: que el filtro
    // respete esa columna y que el resumen la refleje. La regla en sí la comprueban, del lado de
    // PortalDigital, FichaPublicaCompletitudTests y la tabla de verdad de
    // scripts/sql/16-verificar-ficha-completa.sql.

    [Fact]
    public async Task Solo_fichas_completas_respeta_la_columna_que_mantiene_portaldigital()
    {
        var completa   = BaseDePruebas.Ficha("100-001", "Ficha completa");   completa.FichaCompleta = true;
        var incompleta = BaseDePruebas.Ficha("100-002", "Ficha incompleta"); incompleta.FichaCompleta = false;
        _b.Ctx.Fichas.AddRange(completa, incompleta);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync(new CatalogoFiltros(SoloFichasCompletas: true), CancellationToken.None);

        r.Items.Should().ContainSingle(t => t.Codigo == "100-001");
    }

    /// <summary>
    /// El campo informativo del resumen y el filtro salen del mismo sitio. Antes eran dos
    /// expresiones distintas de la misma regla —una en C# y otra en SQL— y podían discrepar; hoy
    /// discrepar es imposible porque las dos leen la misma columna.
    /// </summary>
    [Fact]
    public async Task El_campo_ficha_completa_del_resumen_dice_lo_mismo_que_el_filtro()
    {
        var completa   = BaseDePruebas.Ficha("100-001", "Ficha completa");   completa.FichaCompleta = true;
        var incompleta = BaseDePruebas.Ficha("100-002", "Ficha incompleta"); incompleta.FichaCompleta = false;
        _b.Ctx.Fichas.AddRange(completa, incompleta);
        await _b.SembrarAsync();

        var todas = await Consulta.EjecutarAsync(new CatalogoFiltros(), CancellationToken.None);

        todas.Items.Single(t => t.Codigo == "100-001").FichaCompleta.Should().BeTrue();
        todas.Items.Single(t => t.Codigo == "100-002").FichaCompleta.Should().BeFalse();
    }

    [Fact]
    public async Task La_busqueda_encuentra_por_nombre_descripcion_u_objetivo()
    {
        var porNombre = BaseDePruebas.Ficha("100-001", "Registro de vehículo");
        var porDescripcion = BaseDePruebas.Ficha("100-002", "Otro trámite");
        porDescripcion.Descripcion = "Contiene la palabra vehículo en la descripción";
        var sinCoincidencia = BaseDePruebas.Ficha("100-003", "Nada que ver");
        _b.Ctx.Fichas.AddRange(porNombre, porDescripcion, sinCoincidencia);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync(new CatalogoFiltros(Busqueda: "vehículo"), CancellationToken.None);

        r.Items.Select(t => t.Codigo).Should().BeEquivalentTo(["100-001", "100-002"]);
    }

    [Fact]
    public async Task La_paginacion_respeta_pagina_y_tamano()
    {
        for (var i = 1; i <= 5; i++)
            _b.Ctx.Fichas.Add(BaseDePruebas.Ficha($"100-{i:000}", $"Trámite {i}"));
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync(new CatalogoFiltros(Pagina: 2, Tamano: 2, Orden: "nombre"), CancellationToken.None);

        r.Total.Should().Be(5);
        r.Pagina.Should().Be(2);
        r.Tamano.Should().Be(2);
        r.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Incluye_el_nombre_de_la_categoria()
    {
        var categoria = new CategoriaTramite { Nombre = "Salud y Seguridad Social", Activo = true };
        _b.Ctx.Categorias.Add(categoria);
        await _b.SembrarAsync();

        var t = BaseDePruebas.Ficha("100-001", "Trámite con categoría");
        t.CategoriaId = categoria.Id;
        _b.Ctx.Fichas.Add(t);
        await _b.SembrarAsync();

        var r = await Consulta.EjecutarAsync(new CatalogoFiltros(), CancellationToken.None);

        r.Items.Single().CategoriaNombre.Should().Be("Salud y Seguridad Social");
    }
}
