using System.Net;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Web.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El tablero ejecutivo de proyectos (<c>/Tableros/Ejecutivo</c>) y el Inicio convertido en índice
/// de tableros, servidos de verdad sobre SQLite.
///
/// <para>Lo que se mira acá es lo que solo se rompe al servir la página: el área por defecto que
/// sale de la configuración, el descarte de parámetros que no pertenecen a lo visible, el selector
/// de área según el alcance del rol, el permiso, y en el Inicio el orden de las bandas y el catálogo
/// con sus insignias. Los números los prueban las pruebas de Application.</para>
///
/// <para>Todo el texto buscado es ASCII: Razor codifica lo demás y un <c>Contain("área")</c> pasaría
/// siempre.</para>
/// </summary>
public sealed class TableroEjecutivoTests : IAsyncLifetime
{
    private const string Ruta = "/Tableros/Ejecutivo";
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "Proyectos.Ver", "Tableros.Ver");
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Tableros.Ver");
        // Empleado ve tableros pero no proyectos: es quien no debe recibir la banda del ejecutivo.
        await _portal.OtorgarAsync("Empleado", "Tableros.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jefe = await db.Roles.SingleAsync(r => r.Id == "JefeArea");
        jefe.Actualizar(jefe.Nombre, NivelAlcance.Area, jefe.Descripcion, jefe.Color,
            esAdministrador: false, esSoloLectura: false, esSupervisor: true, esTecnicoSoporte: true,
            esJefeDeArea: true, esPmo: false);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IRolCatalogo>().RecargarAsync();

        // GOBDIG («GOBIERNO DIGITAL», del HasData del modelo) y su unidad DITRA ya existen; GOBDIG es además el
        // área predeterminada de appsettings.json.
        db.Unidades.Add(Unidad.Crear("DAI", PortalFactory.AreaDeCasa, "Desarrollo en IA"));
        db.Areas.Add(Area.Crear("AREA-OTRA", "DIGER", "Area de Resultados"));
        await db.SaveChangesAsync();

        var enDitra = Nuevo("PRY-EJ-01", "Ventanilla unica", PortalFactory.AreaDeCasa, PortalFactory.UnidadDeCasa);
        var enDai   = Nuevo("PRY-EJ-02", "Agente de IA", PortalFactory.AreaDeCasa, "DAI");
        var otra    = Nuevo("PRY-EJ-03", "Monitoreo de resultados", "AREA-OTRA", null);
        // Se queda en Planificado: es el neutro «No iniciado» que va al grupo plegado (R2).
        var planificado = Nuevo("PRY-EJ-04", "Firma avanzada", PortalFactory.AreaDeCasa, PortalFactory.UnidadDeCasa);
        db.Proyectos.AddRange(enDitra, enDai, otra, planificado);
        await db.SaveChangesAsync();

        foreach (var p in new[] { enDitra, enDai, otra })
            p.CambiarEstado(EstadoProyecto.EnEjecucion, "Pruebas");
        await db.SaveChangesAsync();

        db.ProyectoAvances.Add(AvanceProyecto.Crear(enDitra.Id, null, null, "Corte", null, "Henry Ortez",
            bloqueo: "Falta la firma del convenio"));
        await db.SaveChangesAsync();

        static Proyecto Nuevo(string codigo, string nombre, string areaId, string? unidadId)
        {
            var p = Proyecto.Crear(codigo, nombre);
            p.PrioridadId   = PortalFactory.PrioridadPorDefecto;
            p.InstitucionId = "DIGER";
            p.AreaId        = areaId;
            p.UnidadId      = unidadId;
            p.Agregar(EntregableProyecto.Crear("Unico", 1));
            return p;
        }
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> GetAsync(string ruta, string rol = "Administrador", string? area = null)
    {
        var respuesta = await _portal.ClienteComo(rol, area: area).GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    // ── /Tableros/Ejecutivo ────────────────────────────────────────────────

    [Fact]
    public async Task Sin_parametros_muestra_el_area_predeterminada_de_la_configuracion()
    {
        var html = await GetAsync(Ruta);

        html.Should().Contain("Proyectos de GOBIERNO DIGITAL");
        html.Should().Contain("PRY-EJ-01").And.Contain("PRY-EJ-02");
        html.Should().NotContain("PRY-EJ-03", "es de otra area y la cartera es una sola area");
        html.Should().Contain("href=\"/Tableros/Proyecto/", "cada fila lleva al tablero del proyecto");
        html.Should().Contain("Falta la firma del convenio", "el bloqueo vigente encabeza las decisiones");
        html.Should().Contain("class=\"salud-badge rojo\"");
    }

    [Fact]
    public async Task El_filtro_de_unidad_recorta_la_lista()
    {
        var html = await GetAsync(Ruta + "?Unidad=DAI");

        html.Should().Contain("PRY-EJ-02");
        html.Should().NotContain("PRY-EJ-01");
    }

    [Fact]
    public async Task Un_area_que_no_existe_se_descarta_y_se_aplica_la_predeterminada()
    {
        var html = await GetAsync(Ruta + "?Area=NOEXISTE");

        html.Should().Contain("Proyectos de GOBIERNO DIGITAL");
        html.Should().Contain("PRY-EJ-01");
    }

    [Fact]
    public async Task Area_vacia_explicita_es_todas_las_areas()
    {
        var html = await GetAsync(Ruta + "?Area=");

        html.Should().Contain("PRY-EJ-01").And.Contain("PRY-EJ-03");
        html.Should().Contain("Proyectos de DIGER", "sin area el titulo es la institucion");
    }

    [Fact]
    public async Task Quien_tiene_alcance_de_institucion_ve_el_selector_de_area()
    {
        var html = await GetAsync(Ruta);

        html.Should().Contain("id=\"f-area\"");
    }

    [Fact]
    public async Task Un_rol_de_alcance_de_area_no_ve_el_selector_y_queda_en_su_area()
    {
        var html = await GetAsync(Ruta + "?Area=AREA-OTRA", "JefeArea", area: PortalFactory.AreaDeCasa);

        html.Should().NotContain("id=\"f-area\"");
        html.Should().Contain("Proyectos de GOBIERNO DIGITAL", "el parametro Area se ignora para este alcance");
    }

    [Fact]
    public async Task Sin_Proyectos_Ver_el_tablero_se_niega()
    {
        var respuesta = await _portal.ClienteComo("Empleado").GetAsync(Ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>R2: lo neutro va en un details que el servidor entrega abierto (sin JS queda a la
    /// vista) y lo demás fuera de él.</summary>
    [Fact]
    public async Task Los_neutros_van_plegados_en_su_propio_grupo_y_lo_demas_queda_a_la_vista()
    {
        var html = await GetAsync(Ruta);

        var grupo = html.IndexOf("<details class=\"ejec-neutros\" open>", StringComparison.Ordinal);
        grupo.Should().BePositive("el grupo neutro se sirve abierto y lo pliega el script");
        html.Should().Contain("No iniciados (1)");

        html.IndexOf("PRY-EJ-04", StringComparison.Ordinal).Should().BeGreaterThan(grupo, "el planificado es neutro");
        html.IndexOf("PRY-EJ-01", StringComparison.Ordinal).Should().BeLessThan(grupo, "el bloqueado queda fuera del pliegue");
    }

    // ── Inicio: índice de tableros ─────────────────────────────────────────

    [Fact]
    public async Task El_Inicio_pone_el_resumen_ejecutivo_arriba_el_catalogo_y_al_final_lo_operativo()
    {
        var html = await GetAsync("/Tableros/Index");

        var ejecutivo  = html.IndexOf("id=\"ini-ejec-t\"", StringComparison.Ordinal);
        var hoy        = html.IndexOf("id=\"ini-hoy-t\"", StringComparison.Ordinal);
        var todos      = html.IndexOf("id=\"ini-todos-t\"", StringComparison.Ordinal);
        var operativos = html.IndexOf("id=\"ini-oper-t\"", StringComparison.Ordinal);

        ejecutivo.Should().BePositive();
        hoy.Should().BeGreaterThan(ejecutivo);
        todos.Should().BeGreaterThan(hoy);
        operativos.Should().BeGreaterThan(todos, "lo que ya estaba se movio abajo, no se quito");

        html.Should().Contain("Abrir tablero ejecutivo");
        html.Should().Contain("Tickets abiertos", "los tres conteos siguen en el Inicio");
    }

    [Fact]
    public async Task El_catalogo_muestra_las_insignias_con_texto()
    {
        var html = await GetAsync("/Tableros/Index");

        html.Should().Contain("class=\"cat-tablero\" href=\"/Tableros/Ejecutivo\"");
        html.Should().Contain("class=\"tablero-badge nuevo\"");
        html.Should().Contain("class=\"tablero-badge repetido\"");
        html.Should().Contain("Repetido con");
        html.Should().Contain("class=\"tablero-badge rediseno\"");
    }

    [Fact]
    public async Task Sin_Proyectos_Ver_el_Inicio_no_calcula_ni_ofrece_el_ejecutivo()
    {
        var html = await GetAsync("/Tableros/Index", "Empleado");

        html.Should().NotContain("id=\"ini-ejec-t\"");
        html.Should().NotContain("href=\"/Tableros/Ejecutivo\"", "ni la tarjeta del catalogo ni el menu");
        html.Should().Contain("id=\"ini-todos-t\"");
    }

    /// <summary>R6: «Mis proyectos» se marca como repetido con el operativo, que todo el que tiene
    /// Proyectos.Ver puede abrir, y no con «mi área», que solo ven los jefes de área.</summary>
    [Fact]
    public void Mis_proyectos_se_relaciona_con_el_operativo()
    {
        var item = TablerosCatalogo.Buscar("/Tableros/ProyectosUnidad")!;

        item.Relacionado.Should().Be("/Tableros/Proyectos");
        TablerosCatalogo.TextoInsignia(item).Should().Be("Repetido con Proyectos — seguimiento operativo");
    }

    [Fact]
    public async Task Un_tablero_marcado_muestra_su_insignia_bajo_el_titulo()
    {
        var html = await GetAsync("/Tableros/Proyectos");

        html.Should().Contain("class=\"res-meta tablero-marca\"");
        html.Should().Contain("class=\"tablero-badge rediseno\"");
    }

    [Fact]
    public async Task El_menu_ofrece_Ejecutivo_y_Todos_los_tableros()
    {
        var html = await GetAsync("/Tableros/Index");

        html.Should().Contain(">Ejecutivo</a>");
        html.Should().Contain(">Todos los tableros</a>");
        html.Should().NotContain(">Inicio</a>");
    }
}
