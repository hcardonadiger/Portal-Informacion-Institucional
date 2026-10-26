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
    private int _idAgente;

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

        _idAgente = enDai.Id;
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

    /// <summary>
    /// La fila de un proyecto en la cartera, reconocida por su nombre. Hasta el 2026-10-22 la fila
    /// llevaba el código (PRY-…) y estas pruebas lo buscaban; se quitó a propósito —es detalle del
    /// proyecto y vive en su tablero— y en su lugar van responsable y unidad. Se busca el span del
    /// nombre y no el nombre a secas porque el bloque de bloqueos por proyecto también lo escribe,
    /// con otra clase, y un <c>Contain</c> suelto no distinguiría la cartera de ese bloque.
    /// </summary>
    private static string Fila(string nombre) => $"<span class=\"ejec-nombre\">{nombre}</span>";

    // ── /Tableros/Ejecutivo ────────────────────────────────────────────────

    [Fact]
    public async Task Sin_parametros_muestra_el_area_predeterminada_de_la_configuracion()
    {
        var html = await GetAsync(Ruta);

        html.Should().Contain("Proyectos de GOBIERNO DIGITAL");
        html.Should().Contain(Fila("Ventanilla unica")).And.Contain(Fila("Agente de IA"));
        html.Should().NotContain("Monitoreo de resultados", "es de otra area y la cartera es una sola area");
        html.Should().Contain("href=\"/Tableros/Proyecto/", "cada fila lleva al tablero del proyecto");
        html.Should().Contain("Falta la firma del convenio", "el bloqueo vigente encabeza las decisiones");
        html.Should().Contain("class=\"salud-badge rojo\"");
    }

    [Fact]
    public async Task El_filtro_de_unidad_recorta_la_lista()
    {
        var html = await GetAsync(Ruta + "?Unidad=DAI");

        html.Should().Contain(Fila("Agente de IA"));
        html.Should().NotContain(Fila("Ventanilla unica"));
    }

    [Fact]
    public async Task Cambiar_de_categoria_conserva_la_unidad_elegida()
    {
        var html = await GetAsync(Ruta + "?Unidad=DAI");

        // «Todas» es la primera categoria: su enlace ya no suelta la unidad.
        html.Should().Contain("href=\"/Tableros/Ejecutivo?Unidad=DAI\"");
    }

    [Fact]
    public async Task Cambiar_de_categoria_conserva_el_responsable_y_los_proyectos()
    {
        var resp = Guid.NewGuid();
        var html = await GetAsync($"{Ruta}?Responsable={resp}&Proyecto={_idAgente}");

        html.Should().Contain($"href=\"/Tableros/Ejecutivo?Responsable={resp}&amp;Proyecto={_idAgente}\"");
    }

    [Fact]
    public async Task El_selector_de_area_conserva_unidad_y_proyectos()
    {
        var html = await GetAsync($"{Ruta}?Unidad=DAI&Proyecto={_idAgente}");

        var area = html[html.IndexOf("class=\"ejec-area\"")..];
        area = area[..area.IndexOf("</form>")];
        area.Should().Contain("name=\"Unidad\" value=\"DAI\"");
        area.Should().Contain($"name=\"Proyecto\" value=\"{_idAgente}\"");
    }

    [Fact]
    public async Task Solo_los_proyectos_elegidos_entran_al_tablero()
    {
        var html = await GetAsync($"{Ruta}?Proyecto={_idAgente}");

        html.Should().Contain(Fila("Agente de IA"));
        html.Should().NotContain(Fila("Ventanilla unica"));
        html.Should().NotContain("Falta la firma del convenio", "las decisiones tambien se recortan");
        html.Should().Contain($"name=\"Proyecto\" value=\"{_idAgente}\" checked");
        html.Should().Contain("1 proyecto elegido");
    }

    [Fact]
    public async Task El_filtro_de_proyectos_ofrece_los_de_la_cartera()
    {
        var html = await GetAsync(Ruta);

        html.Should().Contain("class=\"ejec-proy-op\"");
        html.Should().Contain($"name=\"Proyecto\" value=\"{_idAgente}\"");
        html.Should().NotContain("Monitoreo de resultados", "es de otra area: no se ofrece");
    }

    [Fact]
    public async Task Una_unidad_sin_proyectos_en_la_cartera_se_mantiene_y_se_explica()
    {
        // DAI solo tiene proyectos en GOBIERNO DIGITAL: en AREA-OTRA la cartera queda vacia.
        var html = await GetAsync(Ruta + "?Area=AREA-OTRA&Unidad=DAI");

        html.Should().NotContain(Fila("Monitoreo de resultados"), "la unidad sigue aplicada");
        html.Should().Contain("<span>Unidad: DESARROLLO EN IA</span>", "el filtro sigue marcado con su nombre");
        html.Should().Contain("class=\"empty-state\"");
        html.Should().Contain("Los filtros se mantienen al cambiar de categor");
    }

    [Fact]
    public async Task Un_area_que_no_existe_se_descarta_y_se_aplica_la_predeterminada()
    {
        var html = await GetAsync(Ruta + "?Area=NOEXISTE");

        html.Should().Contain("Proyectos de GOBIERNO DIGITAL");
        html.Should().Contain(Fila("Ventanilla unica"));
    }

    [Fact]
    public async Task Area_vacia_explicita_es_todas_las_areas()
    {
        var html = await GetAsync(Ruta + "?Area=");

        html.Should().Contain(Fila("Ventanilla unica")).And.Contain(Fila("Monitoreo de resultados"));
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

        html.IndexOf(Fila("Firma avanzada"), StringComparison.Ordinal).Should().BeGreaterThan(grupo, "el planificado es neutro");

        // BePositive primero: un IndexOf que no encuentra devuelve -1, y -1 también es «antes del grupo».
        var bloqueado = html.IndexOf(Fila("Ventanilla unica"), StringComparison.Ordinal);
        bloqueado.Should().BePositive();
        bloqueado.Should().BeLessThan(grupo, "el bloqueado queda fuera del pliegue");
    }

    // ── Inicio: índice de tableros ─────────────────────────────────────────

    [Fact]
    public async Task El_Inicio_pone_el_resumen_ejecutivo_arriba_el_catalogo_y_al_final_lo_operativo()
    {
        var html = await GetAsync("/Tableros/Index");

        var ejecutivo  = html.IndexOf("id=\"ini-ejec-t\"", StringComparison.Ordinal);
        var todos      = html.IndexOf("id=\"ini-todos-t\"", StringComparison.Ordinal);
        var operativos = html.IndexOf("id=\"ini-oper-t\"", StringComparison.Ordinal);

        ejecutivo.Should().BePositive();
        todos.Should().BeGreaterThan(ejecutivo);
        operativos.Should().BeGreaterThan(todos, "lo que ya estaba se movio abajo, no se quito");

        html.Should().Contain("Abrir tablero ejecutivo");
        html.Should().Contain("Tickets abiertos", "los tres conteos siguen en el Inicio");
        // «Atención hoy» se quitó del Inicio el 2026-10-22; los conteos quedaron en los operativos.
        html.Should().NotContain("id=\"ini-hoy-t\"");
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
