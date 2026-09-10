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
/// El tablero de nivel Área (<c>/Tableros/ProyectosArea</c>), servido de verdad.
///
/// <para>Consume los mismos datos que el tablero de Unidad —<c>GetMisProyectosDashboardQuery</c>—
/// así que lo único propio que tiene, y lo único que se prueba acá, es el <b>desglose por
/// unidad</b>. Ahí está la trampa que estas pruebas existen para clavar: <c>UnidadNombre</c> llega
/// <c>null</c> cuando la unidad pertenece a otra institución (el catálogo <c>Unidades</c> lleva
/// filtro por institución activa) aunque <c>UnidadId</c> sí tenga valor. Agrupar por el nombre
/// mete ese proyecto —que sí tiene unidad— en el mismo montón que los que no la tienen, y el
/// desglose miente sin que nadie lo note. Por eso se agrupa por <c>UnidadId</c>.</para>
///
/// <para>Todo el texto que se busca acá es ASCII a propósito: Razor codifica lo demás
/// (<c>á</c> sale como <c>&amp;#xE1;</c>), así que un <c>Contain("área")</c> pasaría siempre
/// —incluso sobre una página en blanco— y no probaría nada.</para>
/// </summary>
public sealed class TableroProyectosAreaTests : IAsyncLifetime
{
    private const string Ruta = "/Tableros/ProyectosArea";

    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver");
        // Empleado tiene la misma clave y NO es jefe de área: es el contraste que muestra que la
        // pestaña la decide la capacidad del rol y no el permiso.
        await _portal.OtorgarAsync("Empleado", "Proyectos.Ver");
        // A Consultor no se le otorga nada: es el contraste de "sin la clave".

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // La pestaña "Mi área" la decide la capacidad del rol, nunca su nombre: sin este flag
        // el rol "JefeArea" no es jefe de área para el portal, por más que así se llame.
        var rolJefe = await db.Roles.SingleAsync(r => r.Id == "JefeArea");
        rolJefe.Actualizar(rolJefe.Nombre, rolJefe.NivelAlcance, rolJefe.Descripcion, rolJefe.Color,
            esAdministrador: false, esSoloLectura: false, esSupervisor: true, esTecnicoSoporte: true,
            esJefeDeArea: true, esPmo: false);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IRolCatalogo>().RecargarAsync();

        // DIGER y CONSUCOOP ya vienen del HasData del modelo (Seed.Instituciones): sembrarlas
        // otra vez choca contra el índice único de Instituciones.Nombre. Se usan tal cual —una
        // es la del usuario, la otra la ajena— porque la unidad de la ajena es la que hace que
        // UnidadNombre venga null con UnidadId lleno.
        db.Areas.AddRange(
            Area.Crear("AREA-DIGER", "DIGER", "Area de Tecnologia"),
            Area.Crear("AREA-EXT", "CONSUCOOP", "Area externa"));
        await db.SaveChangesAsync();

        db.Unidades.AddRange(
            Unidad.Crear("UNI-SIS", "AREA-DIGER", "Unidad de Sistemas"),
            Unidad.Crear("UNI-EXT", "AREA-EXT", "Unidad de otra institucion"));
        await db.SaveChangesAsync();

        var jefe = await db.Usuarios.SingleAsync(u => u.Correo == "jefearea@pruebas.gob.hn");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // (1) y (2) comparten unidad: son el grupo que tiene que salir con "(2)".
        var enSistemas = Nuevo("PRY-ARE-01", "Expediente digital", "UNI-SIS");
        enSistemas.ResponsableId = jefe.Id;
        enSistemas.FechaFinPlan = hoy.AddDays(-7);   // el unico atrasado

        var enSistemasPorInteresado = Nuevo("PRY-ARE-02", "Mesa de ayuda", "UNI-SIS");
        enSistemasPorInteresado.FechaFinPlan = hoy.AddDays(30);

        // (3) Tiene unidad, pero el catalogo no la resuelve: es de otra institucion.
        var conUnidadSinNombre = Nuevo("PRY-ARE-03", "Convenio interinstitucional", "UNI-EXT");
        conUnidadSinNombre.ResponsableId = jefe.Id;

        // (4) De verdad no tiene unidad. No puede caer en el mismo grupo que (3).
        var sinUnidad = Nuevo("PRY-ARE-04", "Plan estrategico", null);
        sinUnidad.ResponsableId = jefe.Id;

        // (5) Del area del jefe pero ajeno a el: ni responsable ni interesado. Con alcance «mios»
        //     no sale —es lo que prueba que ese alcance no ensancha nada— y con «toda mi area» si,
        //     que es justamente para lo que existe el conmutador.
        var ajeno = Nuevo("PRY-ARE-05", "Proyecto de otra persona", "UNI-SIS");
        ajeno.ResponsableId = Guid.NewGuid();

        // (6) De OTRA area de la misma institucion. No debe aparecer con ningun alcance: es la
        //     prueba de aislamiento, y tiene que fallar si alguien deja de anclar por area.
        var deOtraArea = Nuevo("PRY-ARE-06", "Proyecto de otra area", null, "AREA-EXT");
        deOtraArea.ResponsableId = Guid.NewGuid();

        var todos = new[] { enSistemas, enSistemasPorInteresado, conUnidadSinNombre, sinUnidad, ajeno, deOtraArea };
        db.Proyectos.AddRange(todos);
        await db.SaveChangesAsync();

        // El estado se mueve despues del primer guardado: CambiarEstado emite un evento de
        // dominio que la bitacora rechaza si el proyecto todavia no tiene Id.
        foreach (var p in todos)
            p.CambiarEstado(EstadoProyecto.EnEjecucion, "Pruebas");

        // AvancePct no se teclea: sale del promedio de los entregables. Un entregable sin
        // actividades vale por su estado (0 / 50 / 100), que es lo que se usa aca.
        Avance(enSistemas, EstadoEntregable.Completado);             // 100
        Avance(enSistemasPorInteresado, EstadoEntregable.Pendiente); //   0  -> grupo UNI-SIS: 50 %
        Avance(conUnidadSinNombre, EstadoEntregable.Completado);     // 100
        Avance(sinUnidad, EstadoEntregable.Completado);              // 100

        db.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            enSistemasPorInteresado.Id, jefe.Id, "Usuario jefearea", RolInteresado.Ejecutor, "Pruebas"));
        await db.SaveChangesAsync();

        static Proyecto Nuevo(string codigo, string nombre, string? unidadId, string areaId = "AREA-DIGER")
        {
            var p = Proyecto.Crear(codigo, nombre);
            // El filtro de alcance se ancla en la institucion; sin esto no lo ve nadie.
            p.InstitucionId = "DIGER";
            p.UnidadId = unidadId;
            // El area importa desde que el tablero puede acotar por ella. Dejarla en null la haria
            // visible para cualquier area —la rama Area del filtro global deja pasar los sin area—
            // y la prueba de aislamiento no probaria nada.
            p.AreaId = areaId;
            return p;
        }

        static void Avance(Proyecto p, EstadoEntregable estado)
        {
            var e = EntregableProyecto.Crear("Entregable unico", 1);
            e.CambiarEstado(estado);
            p.RecalcularAvance([e]);
        }
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> TableroAsync(string rol = "JefeArea")
    {
        var respuesta = await _portal.ClienteComo(rol).GetAsync(Ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    /// <summary>Cliente con área activa, que es lo que convierte al rol en un jefe que puede
    /// conmutar. <c>ClienteComo</c> no la manda porque la asignación sembrada no lleva área: el
    /// área activa viaja en la cabecera, igual que la institución.</summary>
    private HttpClient ClienteConArea(string rol = "JefeArea", string area = "AREA-DIGER")
    {
        var cliente = _portal.ClienteComo(rol);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.CabeceraArea, area);
        return cliente;
    }

    private static async Task<string> CuerpoAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task La_unidad_cuyo_nombre_no_resuelve_no_se_mezcla_con_los_proyectos_sin_unidad()
    {
        var html = await TableroAsync();

        html.Should().Contain("UNIDAD DE SISTEMAS (2)",
            "los dos proyectos de esa unidad forman un solo grupo");
        html.Should().Contain("UNI-EXT (1)",
            "sin nombre que mostrar, el desglose cae al Id de la unidad, que sigue siendo un grupo propio");
        html.Should().Contain("Sin unidad (1)",
            "solo un proyecto no tiene unidad de verdad");
        html.Should().NotContain("Sin unidad (2)",
            "agrupar por UnidadNombre mandaria al monton de 'sin unidad' un proyecto que si tiene unidad");
    }

    [Fact]
    public async Task El_avance_de_cada_grupo_es_el_promedio_de_sus_proyectos()
    {
        var html = await TableroAsync();

        // UNI-SIS: 100 y 0 -> 50. Los otros dos grupos: 100 cada uno.
        html.Should().MatchRegex(@"UNIDAD DE SISTEMAS \(2\)</span>[\s\S]{0,300}?bar-val"">50%");
        html.Should().MatchRegex(@"UNI-EXT \(1\)</span>[\s\S]{0,300}?bar-val"">100%");
    }

    [Fact]
    public async Task No_lista_un_proyecto_donde_el_usuario_no_figura()
    {
        var html = await TableroAsync();

        html.Should().Contain("PRY-ARE-01");
        html.Should().Contain("PRY-ARE-02", "figura como interesado");
        html.Should().NotContain("PRY-ARE-05",
            "no es responsable ni interesado; el tablero de area no ensancha el alcance de la consulta");
        html.Should().NotContain("Proyecto de otra persona");
    }

    [Fact]
    public async Task Los_KPI_muestran_lo_que_calculo_la_consulta()
    {
        var html = await TableroAsync();

        html.Should().Contain("<div class=\"kpi-num\">4</div><div class=\"kpi-lbl\">Proyectos abiertos</div>",
            "los cuatro suyos, sin el ajeno ni el de la otra area");
        html.Should().Contain("<div class=\"kpi-num\">75%</div><div class=\"kpi-lbl\">Avance promedio</div>",
            "100, 0, 100 y 100 sobre los cuatro en ejecucion");
        html.Should().Contain("<div class=\"kpi-num\">1</div><div class=\"kpi-lbl\">Atrasados</div>");
        html.Should().Contain("<div class=\"kpi-num\">4</div><div class=\"kpi-lbl\">Sin reportar 30+ d",
            "ninguno tiene avances registrados");
    }

    [Fact]
    public async Task El_estado_se_muestra_con_su_etiqueta_legible_y_no_con_el_nombre_del_enum()
    {
        var html = await TableroAsync();

        // Se ancla en la insignia de estado a propósito. Un NotContain("EnEjecucion") suelto no
        // prueba esta página: el panel de notificaciones del layout escribe «PRY-ARE-01 pasó a
        // «EnEjecucion»» porque ProyectoEstadoCambiadoEvent guarda Estado.ToString(). Ese es otro
        // defecto, en otro lugar, y no es lo que arregla esta vista.
        html.Should().Contain("--diger-fs-xs)\">En ejecuci",
            "la etiqueta compartida escribe 'En ejecucion' con espacio");
        html.Should().NotContain("--diger-fs-xs)\">EnEjecucion",
            "imprimir el enum crudo en la insignia de estado es el defecto que se corrige aqui");
    }

    [Fact]
    public async Task Con_alcance_de_area_ve_los_proyectos_de_su_area_que_no_son_suyos()
    {
        var cliente = ClienteConArea();

        var todaElArea = await CuerpoAsync(cliente, $"{Ruta}?Alcance=Area");
        todaElArea.Should().Contain("PRY-ARE-05",
            "es del area del jefe; dirigirla incluye lo que nadie le asigno");

        var soloMios = await CuerpoAsync(cliente, $"{Ruta}?Alcance=Mios");
        soloMios.Should().NotContain("PRY-ARE-05",
            "en 'solo lo mio' vuelve a mandar responsable-o-interesado");
        soloMios.Should().Contain("PRY-ARE-01", "de ese si es responsable");
    }

    [Fact]
    public async Task Nunca_muestra_un_proyecto_de_otra_area_con_ningun_alcance()
    {
        var cliente = ClienteConArea();

        foreach (var url in new[] { Ruta, $"{Ruta}?Alcance=Area", $"{Ruta}?Alcance=Mios" })
        {
            var html = await CuerpoAsync(cliente, url);
            html.Should().NotContain("PRY-ARE-06",
                $"el proyecto vive en AREA-EXT y el jefe dirige AREA-DIGER ({url})");
            html.Should().NotContain("Proyecto de otra area", $"ni su nombre se filtra ({url})");
        }
    }

    [Fact]
    public async Task El_area_no_se_puede_cambiar_desde_la_URL()
    {
        // Lo unico que se bindea es Alcance: un AreaIds tecleado a mano no llega a ninguna parte,
        // porque el area sale siempre de la asignacion activa.
        var html = await CuerpoAsync(ClienteConArea(), $"{Ruta}?Alcance=Area&AreaIds=AREA-EXT");

        html.Should().NotContain("PRY-ARE-06");
        html.Should().Contain("PRY-ARE-01", "el tablero sigue siendo el de su propia area");
    }

    [Fact]
    public async Task Quien_no_es_jefe_de_area_no_conmuta_aunque_lo_pida_en_la_URL()
    {
        // Empleado tiene Proyectos.Ver, asi que la pagina le responde 200, pero su rol no lleva
        // EsJefeDeArea: no se le ofrece el conmutador y pedirlo por URL no lo activa.
        var html = await CuerpoAsync(ClienteConArea("Empleado"), $"{Ruta}?Alcance=Area");

        html.Should().NotContain("Toda mi", "el conmutador no se le renderiza");
        html.Should().NotContain("PRY-ARE-05", "sigue viendo solo donde figura");
        html.Should().NotContain("PRY-ARE-06");
    }

    [Fact]
    public async Task Un_alcance_que_no_existe_no_rompe_la_pagina()
    {
        // Nullable a proposito: el valor basura no bindea, se trata como ausente y la pagina
        // responde con el alcance por defecto en vez de un 500.
        var html = await CuerpoAsync(ClienteConArea(), $"{Ruta}?Alcance=loquesea");

        html.Should().Contain("PRY-ARE-01");
    }

    [Fact]
    public async Task Recuerda_el_ultimo_alcance_elegido()
    {
        var cliente = ClienteConArea();

        await CuerpoAsync(cliente, $"{Ruta}?Alcance=Mios");

        // Sin parametro: tiene que respetar lo elegido antes y no volver al de por defecto.
        var sinParametro = await CuerpoAsync(cliente, Ruta);
        sinParametro.Should().NotContain("PRY-ARE-05", "quedo en 'solo lo mio'");
        sinParametro.Should().Contain("PRY-ARE-01");
    }

    [Fact]
    public async Task La_pestana_de_area_la_decide_la_capacidad_del_rol_y_no_la_clave_de_permiso()
    {
        // Sin la etiqueta: "Mi área" es texto literal del parcial, y el texto literal Razor no lo
        // codifica —sale con su acento tal cual, no como &#xE1;—. Se compara solo la parte ASCII.
        var comoJefe = await TableroAsync();
        comoJefe.Should().Contain("<a class=\"btns on\" href=\"/Tableros/ProyectosArea\">");
        comoJefe.Should().Contain("href=\"/Tableros/ProyectosUnidad\"");

        // Empleado tiene Proyectos.Ver —por eso la página le responde 200— pero su rol no lleva
        // EsJefeDeArea: la pestaña no se le ofrece.
        var comoEmpleado = await TableroAsync("Empleado");
        comoEmpleado.Should().NotContain("href=\"/Tableros/ProyectosArea\"",
            "la pestana sale de la capacidad EsJefeDeArea del rol, no de tener la clave");
    }

    [Fact]
    public async Task Sin_la_clave_Proyectos_Ver_la_pagina_se_deniega()
    {
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync(Ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
