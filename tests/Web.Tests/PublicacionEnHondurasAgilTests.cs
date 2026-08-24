using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La publicación en HondurasÁgil, probada sobre el HTML y las peticiones que de verdad salen
/// del servidor.
///
/// Lo que más importa acá no es que la pantalla funcione, sino que <b>nada más</b> mueva la
/// bandera. Hasta la Fase 4, tres pantallas la recalculaban desde el estado de la ficha en cada
/// guardado, así que corregir una tilde podía sacar un trámite del portal del ciudadano o
/// meterlo sin que nadie lo pidiera. Las dos últimas pruebas son las que impiden que eso vuelva.
/// </summary>
public sealed class PublicacionEnHondurasAgilTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _publicadaCompleta;   // Aprobado + completa + publicada
    private int _candidataIncompleta; // Aprobado + le faltan campos + sin publicar
    private int _publicadaSinAprobar; // Registrado pero publicada a mano

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador",
            "Siger.Ver", "Siger.Editar", "Siger.Publicacion.Ver", "Siger.Publicacion.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Instituciones.Add(Institucion.Crear("IDP", "Instituto de Prueba"));
        var categoria = new CategoriaTramite { Nombre = "Pruebas", Orden = 1 };
        db.CategoriasTramite.Add(categoria);
        await db.SaveChangesAsync();

        var completa = Ficha("910-001", "Constancia publicada y completa", "Aprobado", publicado: true);
        completa.CategoriaId = categoria.Id;
        completa.Modalidad = "Presencial";
        completa.TiempoTexto = "5 dias habiles";
        completa.CostoEsGratuito = true;

        // Aprobada pero con huecos: es candidata y debe salir con aviso, no escondida.
        var candidata = Ficha("910-002", "Permiso aprobado al que le faltan campos", "Aprobado", publicado: false);

        // El caso que rompía al editar: publicada a mano pese a no estar aprobada.
        var sinAprobar = Ficha("910-003", "Tramite publicado a mano sin aprobar", "Registrado", publicado: true);

        db.TramitesSiger.AddRange(completa, candidata, sinAprobar);
        await db.SaveChangesAsync();

        _publicadaCompleta   = completa.Id;
        _candidataIncompleta = candidata.Id;
        _publicadaSinAprobar = sinAprobar.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── La pantalla ───────────────────────────────────────────────────────────

    [Fact]
    public async Task La_pantalla_lista_lo_que_el_ciudadano_esta_viendo()
    {
        var html = await LeerAsync("/Siger/Publicacion");

        html.Should().Contain("Constancia publicada y completa");
        html.Should().NotContain("Permiso aprobado al que le faltan campos",
            "la pestaña por defecto enseña lo publicado, no lo que podría publicarse");
    }

    [Fact]
    public async Task Las_candidatas_salen_con_el_aviso_de_lo_que_les_falta()
    {
        var html = await LeerAsync("/Siger/Publicacion?tab=candidatas");

        html.Should().Contain("Permiso aprobado al que le faltan campos");
        html.Should().Contain("falta", "el aviso tiene que decir qué campos faltan");
        html.Should().NotContain("Tramite publicado a mano sin aprobar",
            "una ficha Registrado no es candidata");
    }

    /// <summary>El aviso informa; no impide. Si algún día bloqueara, esta prueba lo delata.</summary>
    [Fact]
    public async Task Una_ficha_incompleta_se_puede_publicar_igual()
    {
        await EnviarAsync("/Siger/Publicacion?tab=candidatas", "Publicar",
            [new("Seleccion", _candidataIncompleta.ToString())]);

        (await PublicadoAsync(_candidataIncompleta)).Should().BeTrue();
    }

    [Fact]
    public async Task Quitar_de_Honduras_Agil_despublica_pero_no_borra()
    {
        await EnviarAsync("/Siger/Publicacion", "Quitar",
            [new("Seleccion", _publicadaCompleta.ToString())]);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ficha = await db.TramitesSiger.FirstOrDefaultAsync(t => t.Id == _publicadaCompleta);

        ficha.Should().NotBeNull("quitar de HA es despublicar, nunca borrar");
        ficha!.Publicado.Should().BeFalse();
        ficha.Nombre.Should().Be("Constancia publicada y completa");
    }

    // ── Lo que nadie más debe tocar ───────────────────────────────────────────

    /// <summary>
    /// Editar una ficha publicada cuyo estado no es Aprobado debe dejarla publicada. Con la regla
    /// vieja, este guardado la habría sacado del portal del ciudadano en silencio.
    /// </summary>
    [Fact]
    public async Task Editar_una_ficha_publicada_no_la_despublica()
    {
        await EnviarAsync("/Siger/Editor", null,
        [
            new("Form.Id",          _publicadaSinAprobar.ToString()),
            new("Form.Codigo",      "910-003"),
            new("Form.Nombre",      "Nombre corregido a mano"),
            new("Form.Institucion", "Instituto de Prueba"),
            new("Form.EstadoSiger", "Registrado")
        ]);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ficha = await db.TramitesSiger.FirstAsync(t => t.Id == _publicadaSinAprobar);

        // Sin esto la prueba pasaría en verde aunque el guardado no hubiera ocurrido.
        ficha.Nombre.Should().Be("Nombre corregido a mano", "el guardado tiene que haber pasado");
        ficha.Publicado.Should().BeTrue("editar no decide qué ve el ciudadano");
    }

    /// <summary>Y al revés: editar una ficha aprobada pero sin publicar no debe publicarla sola.</summary>
    [Fact]
    public async Task Editar_una_ficha_sin_publicar_no_la_publica()
    {
        await EnviarAsync("/Siger/Editor", null,
        [
            new("Form.Id",          _candidataIncompleta.ToString()),
            new("Form.Codigo",      "910-002"),
            new("Form.Nombre",      "Otro nombre corregido"),
            new("Form.Institucion", "Instituto de Prueba"),
            new("Form.EstadoSiger", "Aprobado")
        ]);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ficha = await db.TramitesSiger.FirstAsync(t => t.Id == _candidataIncompleta);

        ficha.Nombre.Should().Be("Otro nombre corregido", "el guardado tiene que haber pasado");
        ficha.Publicado.Should().BeFalse("aprobar no es publicar");
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Envía un formulario como lo haría el navegador. Hay que pedir la página antes para
    /// arrastrar el token antifalsificación y su cookie: sin eso Razor Pages rechaza el POST y la
    /// prueba pasaría en verde sin haber guardado nada.
    /// </summary>
    private async Task EnviarAsync(string ruta, string? handler, List<KeyValuePair<string, string>> campos)
    {
        var cliente = _portal.ClienteComo("Administrador");

        var pagina = await cliente.GetAsync(ruta);
        pagina.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await pagina.Content.ReadAsStringAsync();

        var token = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        token.Should().NotBeEmpty("sin token el POST se rechaza y la prueba no probaría nada");

        campos.Add(new("__RequestVerificationToken", token));

        var destino = handler is null ? ruta : $"{ruta}{(ruta.Contains('?') ? "&" : "?")}handler={handler}";
        var respuesta = await cliente.PostAsync(destino, new FormUrlEncodedContent(campos));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect,
            "un guardado correcto redirige; un 200 significa que la validación lo rechazó");
    }

    private async Task<bool> PublicadoAsync(int id)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TramitesSiger.Where(t => t.Id == id).Select(t => t.Publicado).FirstAsync();
    }

    private static TramiteSiger Ficha(string codigo, string nombre, string estado, bool publicado) => new()
    {
        Codigo = codigo, Nombre = nombre, EstadoSiger = estado, Publicado = publicado,
        Institucion = "Instituto de Prueba", Sigla = "IDP", InstitucionId = "IDP"
    };
}
