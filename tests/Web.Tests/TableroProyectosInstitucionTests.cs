using System.Net;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El tablero de nivel Institución (<c>/Tableros/Proyectos</c>), servido de verdad.
///
/// <para>Lo que se prueba acá es lo que Task 10 le agrega y que solo se rompe al servir la
/// página: (1) la <b>redirección por defecto</b> —quien no alcanza el nivel de institución cae
/// en el tablero que sí le corresponde, y ese destino responde 200 en vez de rebotarlo de
/// vuelta—, (2) el <b>filtro de área</b>, que tiene que recortar de verdad el portafolio, y
/// (3) el <b>desplegable de áreas</b>, que no puede ofrecer áreas de otra institución: el
/// filtro global de <c>Area</c> se cortocircuita con <c>_alcanceGlobal</c> y un Administrador
/// vería el catálogo entero si la consulta no se acotara a su institución activa.</para>
///
/// <para>Todo el texto que se busca acá es ASCII a propósito: Razor codifica lo demás
/// (<c>á</c> sale como <c>&amp;#xE1;</c>), así que un <c>Contain("área")</c> pasaría siempre
/// —incluso sobre una página en blanco— y no probaría nada.</para>
/// </summary>
public sealed class TableroProyectosInstitucionTests : IAsyncLifetime
{
    private const string Ruta = "/Tableros/Proyectos";

    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "Proyectos.Ver");
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver");
        await _portal.OtorgarAsync("Empleado", "Proyectos.Ver");
        // Consultor también la recibe acá: en este archivo es el rol de alcance de INSTITUCIÓN
        // sin ser global, que es la otra mitad de la condición del redirect.
        await _portal.OtorgarAsync("Consultor", "Proyectos.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Las capacidades se encienden en la tabla Roles, nunca por el nombre del rol.
        var jefe = await db.Roles.SingleAsync(r => r.Id == "JefeArea");
        jefe.Actualizar(jefe.Nombre, NivelAlcance.Area, jefe.Descripcion, jefe.Color,
            esAdministrador: false, esSoloLectura: false, esSupervisor: true, esTecnicoSoporte: true,
            esJefeDeArea: true, esPmo: false);

        // Consultor pasa a alcance de Institución: prueba la rama `NivelAlcance.Institucion` del
        // redirect sin pasar por EsGlobal, que es la que cubre Administrador.
        var consultor = await db.Roles.SingleAsync(r => r.Id == "Consultor");
        consultor.Actualizar(consultor.Nombre, NivelAlcance.Institucion, consultor.Descripcion, consultor.Color,
            esAdministrador: false, esSoloLectura: true, esSupervisor: false, esTecnicoSoporte: false,
            esJefeDeArea: false, esPmo: false);

        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IRolCatalogo>().RecargarAsync();

        // DIGER y CONSUCOOP ya vienen del HasData del modelo (Seed.Instituciones).
        var vieja = Area.Crear("AREA-VIEJA", "DIGER", "Area disuelta");
        vieja.Desactivar();

        db.Areas.AddRange(
            Area.Crear("AREA-TEC", "DIGER", "Area de Tecnologia"),
            Area.Crear("AREA-ADM", "DIGER", "Area Administrativa"),
            vieja,
            // La ajena: el desplegable no puede ofrecerla, porque filtrar por ella devolvería un
            // tablero vacío sin explicación.
            Area.Crear("AREA-EXT", "CONSUCOOP", "Area de otra institucion"));
        await db.SaveChangesAsync();

        var enTecnologia    = Nuevo("PRY-INS-01", "Expediente digital", "AREA-TEC");
        var enAdministracion = Nuevo("PRY-INS-02", "Compras consolidadas", "AREA-ADM");
        // Sin área: al filtrar por un área concreta tiene que salir del tablero — se pidieron
        // esas áreas, no «esas o ninguna».
        var sinArea         = Nuevo("PRY-INS-03", "Plan estrategico", null);
        // En el área ya disuelta: es el proyecto que sostiene el caso de la URL guardada.
        var enAreaInactiva  = Nuevo("PRY-INS-04", "Archivo historico", "AREA-VIEJA");

        var todos = new[] { enTecnologia, enAdministracion, sinArea, enAreaInactiva };
        db.Proyectos.AddRange(todos);
        await db.SaveChangesAsync();

        // El estado se mueve después del primer guardado: CambiarEstado emite un evento de
        // dominio que la bitácora rechaza si el proyecto todavía no tiene Id.
        foreach (var p in todos)
            p.CambiarEstado(EstadoProyecto.EnEjecucion, "Pruebas");

        await db.SaveChangesAsync();

        static Proyecto Nuevo(string codigo, string nombre, string? areaId)
        {
            var p = Proyecto.Crear(codigo, nombre);
            // El filtro de alcance se ancla en la institución; sin esto no lo ve un rol no global.
            p.InstitucionId = "DIGER";
            p.AreaId = areaId;
            return p;
        }
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> TableroAsync(string rol = "Administrador", string consulta = "")
    {
        var respuesta = await _portal.ClienteComo(rol).GetAsync(Ruta + consulta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    // ── Redirección por defecto ─────────────────────────────────────────────

    [Fact]
    public async Task Un_rol_de_alcance_global_ve_el_tablero_de_institucion_y_su_pestana_activa()
    {
        var html = await TableroAsync();

        html.Should().Contain("PRY-INS-01").And.Contain("PRY-INS-02").And.Contain("PRY-INS-03");
        html.Should().Contain("<a class=\"btns on\" href=\"/Tableros/Proyectos\">",
            "la pestana de Institucion viene marcada como activa en su propia pagina");
        html.Should().Contain("href=\"/Tableros/ProyectosUnidad\"",
            "las pestanas del parcial se dibujan en las tres paginas");
    }

    [Fact]
    public async Task Un_rol_de_alcance_de_institucion_tambien_ve_el_tablero_sin_redireccion()
    {
        // Consultor no es global (EsAdministrador == false) pero su alcance es Institucion:
        // la condición del redirect no puede depender solo de EsGlobal.
        var html = await TableroAsync("Consultor");

        html.Should().Contain("PRY-INS-01");
    }

    [Fact]
    public async Task Un_jefe_de_area_sin_alcance_de_institucion_es_redirigido_a_su_tablero_de_area()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var respuesta = await cliente.GetAsync(Ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Found);
        respuesta.Headers.Location!.OriginalString.Should().Be("/Tableros/ProyectosArea");

        // Se sigue el salto a mano: un destino que rebotara de vuelta armaría un ciclo que el
        // navegador corta con un error, y una aserción sobre el Location no lo vería.
        var destino = await cliente.GetAsync(respuesta.Headers.Location);
        destino.StatusCode.Should().Be(HttpStatusCode.OK,
            "el destino de la redireccion tiene que servir la pagina, no redirigir otra vez");
    }

    [Fact]
    public async Task Un_rol_sin_alcance_de_institucion_y_sin_jefatura_de_area_va_al_tablero_de_unidad()
    {
        var cliente = _portal.ClienteComo("Empleado");
        var respuesta = await cliente.GetAsync(Ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Found);
        respuesta.Headers.Location!.OriginalString.Should().Be("/Tableros/ProyectosUnidad");

        var destino = await cliente.GetAsync(respuesta.Headers.Location);
        destino.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Filtro de área ──────────────────────────────────────────────────────

    [Fact]
    public async Task El_filtro_de_area_recorta_el_tablero()
    {
        var html = await TableroAsync(consulta: "?AreaIds=AREA-TEC");

        html.Should().Contain("PRY-INS-01", "es el unico proyecto del area pedida");
        html.Should().NotContain("PRY-INS-02", "es de otra area");
        html.Should().NotContain("PRY-INS-03", "no tiene area: se pidio un area concreta, no 'esa o ninguna'");
    }

    [Fact]
    public async Task El_filtro_de_area_acepta_varias_areas_a_la_vez()
    {
        var html = await TableroAsync(consulta: "?AreaIds=AREA-TEC&AreaIds=AREA-ADM");

        html.Should().Contain("PRY-INS-01").And.Contain("PRY-INS-02");
        html.Should().NotContain("PRY-INS-03");
    }

    [Fact]
    public async Task El_desplegable_solo_ofrece_areas_de_la_institucion_activa()
    {
        var html = await TableroAsync();

        html.Should().Contain("value=\"AREA-TEC\"").And.Contain("value=\"AREA-ADM\"");
        html.Should().NotContain("AREA-EXT",
            "el filtro global de Area se cortocircuita para un rol global: sin acotar la consulta a "
          + "la institucion activa, el desplegable ofreceria areas de instituciones que este tablero no muestra");
    }

    [Fact]
    public async Task El_desplegable_omite_las_areas_inactivas_pero_conserva_la_que_ya_venia_seleccionada()
    {
        var sinFiltro = await TableroAsync();
        sinFiltro.Should().NotContain("AREA-VIEJA",
            "un area desactivada ya no es una opcion que ofrecer");

        // Una URL guardada de cuando el área seguía activa: si la opción desaparece, el tablero
        // se ve recortado sin que nada en pantalla diga por qué, y no hay forma de deseleccionarla.
        var conFiltro = await TableroAsync(consulta: "?AreaIds=AREA-VIEJA");
        conFiltro.Should().Contain("value=\"AREA-VIEJA\"");
        conFiltro.Should().Contain("PRY-INS-04", "el filtro sigue aplicando sobre el area inactiva");
        conFiltro.Should().NotContain("PRY-INS-01");
    }

    [Fact]
    public async Task Sin_la_clave_Proyectos_Ver_la_pagina_se_deniega_antes_de_redirigir()
    {
        // El usuario sin rol no tiene ninguna clave: la denegación gana sobre el redirect.
        var respuesta = await _portal.ClienteComo(null).GetAsync(Ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
