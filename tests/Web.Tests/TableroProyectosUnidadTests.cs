using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El tablero de nivel Unidad (<c>/Tableros/ProyectosUnidad</c>), servido de verdad.
///
/// <para>Lo que se prueba acá no es la aritmética de <c>GetMisProyectosDashboardQuery</c> —eso ya
/// tiene sus pruebas en Application— sino las tres cosas que solo se rompen al servir la página:
/// que el recorte «mis proyectos» llegue intacto al HTML (que no aparezca el proyecto ajeno), que
/// los cuatro KPI muestren el número que la consulta calculó, y que las pestañas se dibujen según
/// el <b>alcance del rol</b> y no según su nombre — un rol de unidad no puede ver la pestaña de
/// institución aunque tenga <c>Proyectos.Ver</c>.</para>
///
/// <para>Todo el texto que se busca acá es ASCII a propósito: Razor codifica lo demás
/// (<c>í</c> sale como <c>&amp;#xED;</c>), así que un <c>Contain("días")</c> pasaría siempre
/// —incluso sobre una página en blanco— y no probaría nada.</para>
/// </summary>
public sealed class TableroProyectosUnidadTests : IAsyncLifetime
{
    private const string Ruta = "/Tableros/ProyectosUnidad";

    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Empleado", "Proyectos.Ver");
        // A Consultor no se le otorga nada: es el contraste de "sin la clave".

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // DIGER y CONSUCOOP ya vienen del HasData del modelo. La unidad de la institución ajena
        // es la que hace que UnidadNombre venga null con UnidadId lleno: el catálogo Unidades
        // lleva filtro por institución activa y no la resuelve.
        db.Areas.Add(Area.Crear("AREA-EXT", "CONSUCOOP", "Area externa"));
        await db.SaveChangesAsync();
        db.Unidades.Add(Unidad.Crear("UNI-EXT", "AREA-EXT", "Unidad de otra institucion"));
        await db.SaveChangesAsync();

        var empleado = await db.Usuarios.SingleAsync(u => u.Correo == "empleado@pruebas.gob.hn");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // (1) Suyo por responsable, y con la fecha de cierre ya pasada: es el atrasado.
        var comoResponsable = Nuevo("PRY-UNI-01", "Proyecto donde soy responsable");
        comoResponsable.ResponsableId = empleado.Id;
        comoResponsable.FechaFinPlan  = hoy.AddDays(-10);

        // (2) Suyo por interesado: el otro camino de acceso, el que sostiene el tablero cuando
        //     el proyecto lo lleva otra persona.
        //     Además lleva una unidad que el catálogo no resuelve, para fijar el respaldo de la
        //     columna Unidad — ver la prueba del fallback.
        var comoInteresado = Nuevo("PRY-UNI-02", "Proyecto donde soy interesado");
        comoInteresado.FechaFinPlan = hoy.AddDays(30);
        comoInteresado.UnidadId     = "UNI-EXT";

        // (3) Ajeno: ni responsable ni interesado. Su ausencia es la mitad de la prueba.
        var ajeno = Nuevo("PRY-UNI-03", "Proyecto de otra persona");
        ajeno.ResponsableId = Guid.NewGuid();

        db.Proyectos.AddRange(comoResponsable, comoInteresado, ajeno);
        await db.SaveChangesAsync();

        // El estado se mueve después del primer guardado: CambiarEstado emite un evento de
        // dominio que la bitácora rechaza si el proyecto todavía no tiene Id.
        foreach (var p in new[] { comoResponsable, comoInteresado, ajeno })
            p.CambiarEstado(EstadoProyecto.EnEjecucion, "Pruebas");

        db.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            comoInteresado.Id, empleado.Id, "Usuario empleado", RolInteresado.Ejecutor, "Pruebas"));
        await db.SaveChangesAsync();

        static Proyecto Nuevo(string codigo, string nombre)
        {
            var p = Proyecto.Crear(codigo, nombre);
            // El filtro de alcance se ancla en la institución; sin esto no lo ve nadie.
            p.InstitucionId = "DIGER";
            return p;
        }
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> TableroAsync(string rol)
    {
        var respuesta = await _portal.ClienteComo(rol).GetAsync(Ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Lista_solo_los_proyectos_donde_el_usuario_es_responsable_o_interesado()
    {
        var html = await TableroAsync("Empleado");

        html.Should().Contain("PRY-UNI-01", "es responsable de este");
        html.Should().Contain("PRY-UNI-02", "es interesado de este");
        html.Should().NotContain("PRY-UNI-03",
            "no figura en este proyecto; el tablero de unidad no es el portafolio completo");
    }

    [Fact]
    public async Task Los_cuatro_KPI_muestran_lo_que_calculo_la_consulta()
    {
        var html = await TableroAsync("Empleado");

        html.Should().Contain("<div class=\"kpi-num\">2</div><div class=\"kpi-lbl\">Proyectos</div>");
        html.Should().Contain("<div class=\"kpi-num\">1</div><div class=\"kpi-lbl\">Atrasados</div>",
            "solo el primero tiene la fecha de cierre vencida");
        html.Should().Contain("<div class=\"kpi-num\">2</div><div class=\"kpi-lbl\">Sin reportar 30+ d",
            "ninguno de los dos tiene avances registrados");
        html.Should().Contain("Avance promedio");
    }

    [Fact]
    public async Task El_proyecto_vencido_se_marca_y_el_vigente_no()
    {
        var html = await TableroAsync("Empleado");

        html.Should().Contain("badge-danger", "el atrasado lleva su marca de vencido");
        html.Should().Contain("bar-fill c-red", "y su barra de avance se pinta en rojo");
    }

    [Fact]
    public async Task El_estado_se_muestra_con_su_etiqueta_legible_y_no_con_el_nombre_del_enum()
    {
        var html = await TableroAsync("Empleado");

        // Se ancla en la celda de la tabla a propósito. Un NotContain("EnEjecucion") suelto no
        // prueba esta página: el panel de notificaciones del layout escribe «PRY-UNI-01 pasó a
        // «EnEjecucion»» porque ProyectoEstadoCambiadoEvent guarda Estado.ToString(). Ese es otro
        // defecto, en otro lugar, y no es lo que arregla esta vista.
        html.Should().Contain("<td class=\"res-meta\">En ejecuci",
            "la etiqueta compartida escribe 'En ejecucion' con espacio, como sus dos tableros hermanos");
        html.Should().NotContain("<td class=\"res-meta\">EnEjecucion",
            "imprimir el enum crudo en la columna Estado es el defecto que se corrige aqui");
    }

    [Fact]
    public async Task La_columna_Unidad_cae_al_Id_cuando_el_catalogo_no_resuelve_el_nombre()
    {
        var html = await TableroAsync("Empleado");

        // Mismo respaldo que /Tableros/ProyectosArea, y por la misma razón: un proyecto que SÍ
        // tiene unidad no puede mostrarse igual que uno que no la tiene. Con el guion como único
        // respaldo, las dos vistas hermanas contestaban distinto sobre el mismo proyecto.
        html.Should().Contain("<td class=\"res-meta\">UNI-EXT</td>",
            "el nombre no se resuelve —la unidad es de otra institucion— pero el Id sigue identificandola");
        html.Should().Contain("<td class=\"res-meta\">Sin unidad</td>",
            "el proyecto que de verdad no tiene unidad se dice con todas las letras, no con un guion");
    }

    [Fact]
    public async Task La_pestana_de_unidad_viene_activa_y_la_de_institucion_no_se_ofrece_a_un_rol_de_unidad()
    {
        var html = await TableroAsync("Empleado");

        html.Should().Contain("<a class=\"btns on\" href=\"/Tableros/ProyectosUnidad\">Mi unidad</a>");

        // El rol Empleado tiene NivelAlcance.Unidad: el portafolio de la institución no es suyo.
        // La comilla de cierre importa — sin ella, "/Tableros/ProyectosUnidad" haría pasar la
        // aserción por ser prefijo.
        html.Should().NotContain("href=\"/Tableros/Proyectos\"",
            "un rol de alcance de unidad no puede saltar al tablero de la institucion");
    }

    [Fact]
    public async Task Un_rol_de_alcance_global_si_ve_la_pestana_de_institucion_y_su_tablero_vacio()
    {
        // Administrador no es responsable ni interesado de ningún proyecto: el estado vacío es
        // justamente lo que tiene que ver, en vez de una tabla en blanco sin explicación.
        var html = await TableroAsync("Administrador");

        html.Should().Contain("href=\"/Tableros/Proyectos\"");
        html.Should().Contain("No hay proyectos donde figures");
        html.Should().NotContain("PRY-UNI-01");
    }

    [Fact]
    public async Task Sin_la_clave_Proyectos_Ver_la_pagina_se_deniega()
    {
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync(Ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
