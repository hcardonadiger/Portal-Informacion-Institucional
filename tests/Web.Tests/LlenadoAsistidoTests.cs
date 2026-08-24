using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La cola del llenado asistido, probada sobre el HTML y las peticiones que de verdad salen del
/// servidor.
///
/// Lo que se protege acá no es que la pantalla se vea: es que <b>aprobar nunca pise lo que
/// escribió una persona</b> y que <b>rechazar deje el campo vacío</b>. Son 1 032 fichas y las
/// aprobaciones van a ser en bloque de cientos; un fallo en cualquiera de esas dos reglas no se
/// notaría hasta verlo publicado en el portal del ciudadano.
/// </summary>
public sealed class LlenadoAsistidoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _fichaVacia;      // sin categoría ni tiempo: la propuesta debe aplicarse
    private int _fichaYaLlena;    // alguien le puso la categoría a mano después de proponerla
    private int _propCategoria;   // propuesta sobre _fichaVacia
    private int _propTiempo;      // propuesta sobre _fichaVacia
    private int _propSuperada;    // propuesta sobre _fichaYaLlena

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador",
            "Siger.Ver", "Siger.Editar", "Siger.Llenado.Ver", "Siger.Llenado.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Instituciones.Add(Institucion.Crear("IDP", "Instituto de Prueba"));
        var categoria = new CategoriaTramite { Nombre = "Pruebas de llenado", Orden = 900 };
        db.CategoriasTramite.Add(categoria);
        await db.SaveChangesAsync();

        var vacia    = Ficha("920-001", "Emisión de licencia de conducir");
        var yaLlena  = Ficha("920-002", "Renovación de placa vehicular");
        db.TramitesSiger.AddRange(vacia, yaLlena);
        await db.SaveChangesAsync();

        _fichaVacia   = vacia.Id;
        _fichaYaLlena = yaLlena.Id;

        // Alguien llenó la categoría a mano *después* de que se propusiera. La propuesta quedó
        // obsoleta sin que la cola se entere: ese es justo el caso que hay que atrapar.
        yaLlena.CategoriaId = categoria.Id;

        var pCat = Propuesta(vacia.Id,   CampoFicha.Categoria, categoria.Id.ToString(), CertezaLlenado.Media);
        var pTie = Propuesta(vacia.Id,   CampoFicha.Tiempo,    "5 días hábiles",        CertezaLlenado.Alta);
        var pSup = Propuesta(yaLlena.Id, CampoFicha.Categoria, categoria.Id.ToString(), CertezaLlenado.Baja);

        db.PropuestasLlenado.AddRange(pCat, pTie, pSup);
        await db.SaveChangesAsync();

        _propCategoria = pCat.Id;
        _propTiempo    = pTie.Id;
        _propSuperada  = pSup.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── La pantalla ───────────────────────────────────────────────────────────

    [Fact]
    public async Task La_cola_enseña_lo_propuesto_con_su_justificacion()
    {
        var html = await LeerAsync("/Siger/Llenado");

        html.Should().Contain("Emisión de licencia de conducir");
        html.Should().Contain("5 días hábiles");
        html.Should().Contain("porque sí, es una prueba",
            "sin la justificación a la vista, aprobar en bloque es firmar a ciegas");
    }

    /// <summary>La categoría se guarda como id, pero un «1» en pantalla no le dice nada a nadie.</summary>
    [Fact]
    public async Task La_categoria_se_enseña_por_su_nombre_y_no_por_su_numero()
    {
        var html = await LeerAsync("/Siger/Llenado?campo=Categoria");

        html.Should().Contain("Pruebas de llenado");
    }

    [Fact]
    public async Task Se_puede_filtrar_por_certeza()
    {
        var html = await LeerAsync("/Siger/Llenado?certeza=Alta");

        html.Should().Contain("5 días hábiles");
        html.Should().NotContain("Renovación de placa vehicular",
            "esa propuesta es de certeza Baja y el filtro pidió Alta");
    }

    // ── Aprobar ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Aprobar_escribe_el_valor_en_la_ficha()
    {
        await EnviarAsync("/Siger/Llenado", "Aprobar", [new("Seleccion", _propTiempo.ToString())]);

        var ficha = await FichaAsync(_fichaVacia);
        ficha.TiempoTexto.Should().Be("5 días hábiles");

        var propuesta = await PropuestaAsync(_propTiempo);
        propuesta.Estado.Should().Be(EstadoPropuesta.Aprobada);
        propuesta.DecididaPor.Should().NotBeNullOrEmpty("la procedencia tiene que sobrevivir a la aprobación");
        propuesta.DecididaEl.Should().NotBeNull();
    }

    /// <summary>
    /// La prueba que sostiene toda la fase. Si alguien llenó el campo a mano entre la propuesta y
    /// la aprobación, la aprobación no puede pisarlo: el trabajo humano gana siempre. Sin esto,
    /// un clic en «aprobar las 300 del filtro» borraría en silencio trescientas correcciones.
    /// </summary>
    [Fact]
    public async Task Aprobar_no_pisa_un_campo_que_alguien_lleno_a_mano()
    {
        var antes = (await FichaAsync(_fichaYaLlena)).CategoriaId;
        antes.Should().NotBeNull("la prueba no valdría nada si el campo estuviera vacío");

        await EnviarAsync("/Siger/Llenado", "Aprobar", [new("Seleccion", _propSuperada.ToString())]);

        var ficha = await FichaAsync(_fichaYaLlena);
        ficha.CategoriaId.Should().Be(antes, "lo que escribió una persona no se toca");

        var propuesta = await PropuestaAsync(_propSuperada);
        propuesta.Estado.Should().Be(EstadoPropuesta.Rechazada,
            "la propuesta quedó superada; dejarla pendiente la haría reaparecer para siempre");
    }

    /// <summary>Aprobar por filtro alcanza todo lo que coincide, no solo lo marcado en la página
    /// visible. Es la única forma de que mil propuestas se resuelvan en una tarde.</summary>
    [Fact]
    public async Task Aprobar_por_filtro_alcanza_mas_que_la_pagina_visible()
    {
        await EnviarAsync("/Siger/Llenado?certeza=Alta", "AprobarFiltro", []);

        (await PropuestaAsync(_propTiempo)).Estado.Should().Be(EstadoPropuesta.Aprobada);
        (await PropuestaAsync(_propCategoria)).Estado.Should().Be(EstadoPropuesta.Pendiente,
            "esa es de certeza Media y el filtro pedía Alta: aprobar por filtro no puede desbordarse");
    }

    // ── Rechazar ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rechazar_deja_el_campo_vacio_y_guarda_la_decision()
    {
        await EnviarAsync("/Siger/Llenado", "Rechazar", [new("Seleccion", _propCategoria.ToString())]);

        (await FichaAsync(_fichaVacia)).CategoriaId.Should().BeNull();

        var propuesta = await PropuestaAsync(_propCategoria);
        propuesta.Estado.Should().Be(EstadoPropuesta.Rechazada);
        propuesta.ValorPropuesto.Should().NotBeNull(
            "el valor rechazado se conserva: es lo que impide que la siguiente corrida vuelva a proponerlo");
    }

    // ── Permisos ──────────────────────────────────────────────────────────────

    /// <summary>Decidir qué se escribe en 1 032 fichas no es lo mismo que poder mirar la cola.</summary>
    [Fact]
    public async Task Quien_solo_puede_ver_no_puede_aprobar()
    {
        await _portal.OtorgarAsync("Empleado", "Siger.Llenado.Ver");
        var cliente = _portal.ClienteComo("Empleado");

        var pagina = await cliente.GetAsync("/Siger/Llenado");
        pagina.StatusCode.Should().Be(HttpStatusCode.OK, "ver la cola sí puede");

        var respuesta = await cliente.PostAsync("/Siger/Llenado?handler=Aprobar",
            new FormUrlEncodedContent([
                new KeyValuePair<string, string>("Seleccion", _propTiempo.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", Token(await pagina.Content.ReadAsStringAsync()))
            ]));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "el POST se rechaza de plano; el permiso de ver no arrastra el de decidir");

        (await FichaAsync(_fichaVacia)).TiempoTexto.Should().BeNull("no se aprobó nada");
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        // Se decodifica porque Razor escapa los acentos (Emisi&#xF3;n) y las fichas reales los
        // llevan: comparar contra el HTML crudo obligaría a escribir las pruebas sin tildes.
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }

    private async Task EnviarAsync(string ruta, string handler, List<KeyValuePair<string, string>> campos)
    {
        var cliente = _portal.ClienteComo("Administrador");

        var pagina = await cliente.GetAsync(ruta);
        pagina.StatusCode.Should().Be(HttpStatusCode.OK);

        campos.Add(new("__RequestVerificationToken", Token(await pagina.Content.ReadAsStringAsync())));

        var destino = $"{ruta}{(ruta.Contains('?') ? "&" : "?")}handler={handler}";
        var respuesta = await cliente.PostAsync(destino, new FormUrlEncodedContent(campos));

        respuesta.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.Redirect, HttpStatusCode.Found },
            "un guardado correcto redirige; un 200 significa que la validación lo rechazó");
    }

    private static string Token(string html)
    {
        var token = Regex.Match(html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""").Groups[1].Value;
        token.Should().NotBeEmpty("sin token el POST se rechaza y la prueba no probaría nada");
        return token;
    }

    private async Task<TramiteSiger> FichaAsync(int id)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TramitesSiger.AsNoTracking().FirstAsync(t => t.Id == id);
    }

    private async Task<PropuestaLlenado> PropuestaAsync(int id)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PropuestasLlenado.AsNoTracking().FirstAsync(p => p.Id == id);
    }

    private static TramiteSiger Ficha(string codigo, string nombre) => new()
    {
        Codigo = codigo, Nombre = nombre, EstadoSiger = "Registrado",
        Institucion = "Instituto de Prueba", Sigla = "IDP", InstitucionId = "IDP"
    };

    private static PropuestaLlenado Propuesta(int fichaId, CampoFicha campo, string valor, CertezaLlenado certeza) => new()
    {
        TramiteSigerId = fichaId,
        Campo          = campo,
        ValorPropuesto = valor,
        Certeza        = certeza,
        Justificacion  = "porque sí, es una prueba",
        Estado         = EstadoPropuesta.Pendiente
    };
}
