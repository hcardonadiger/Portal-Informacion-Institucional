using System.Security.Claims;
using System.Text.Json;
using Diger.TramitesEstado.Application.Reuniones.Memoria;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Web.Mcp;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Las herramientas MCP de la ayuda memoria: <c>instrucciones_memoria_reunion</c> le da a la IA
/// las reglas que corresponden al tipo de la reunión, y <c>llenar_memoria_reunion</c> escribe el
/// JSON con el mismo comando que aplica esas reglas. Se ejecutan con la identidad de un token MCP
/// —los claims del login—, que es de donde leen la matriz de permisos y el filtro de alcance.
/// </summary>
public sealed class McpMemoriaReunionTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _capacitacionId;
    private int _reunionId;
    private int _reunionConObjetivoId;
    private int _reunionDeOtraInstitucionId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver", "Reuniones.Editar");
        await _portal.OtorgarAsync("Empleado", "Reuniones.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var capacitacion = Sembrar("Capacitación en la plataforma", "DIGER");
        capacitacion.Tipo = MemoriaDesdeTranscripcion.TipoCapacitacion;
        capacitacion.EncuestaActiva = true;

        var reunion = Sembrar("Mesa técnica", "DIGER");
        reunion.Tipo = "Reunión técnica";

        var conObjetivo = Sembrar("Mesa con objetivo", "DIGER");
        conObjetivo.ObjetivoAgenda = "Objetivo que escribió una persona";

        var ajena = Sembrar("Mesa de otra institución", "COPECO");
        ajena.AreaId = null;
        ajena.UnidadId = null;

        db.Reuniones.AddRange(capacitacion, reunion, conObjetivo, ajena);
        await db.SaveChangesAsync();

        _capacitacionId = capacitacion.Id;
        _reunionId = reunion.Id;
        _reunionConObjetivoId = conObjetivo.Id;
        _reunionDeOtraInstitucionId = ajena.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static Reunion Sembrar(string titulo, string institucion)
    {
        var r = Reunion.Crear(titulo);
        r.InstitucionId = institucion;
        r.InstitucionDuenaId = institucion;
        r.AreaId = PortalFactory.AreaDeCasa;
        r.UnidadId = PortalFactory.UnidadDeCasa;
        r.Fecha = new DateOnly(2026, 10, 1);
        r.Hora = "09:00";
        return r;
    }

    /// <summary>Ejecuta la herramienta como lo haría un token MCP del rol indicado.</summary>
    private async Task<T> ComoMcpAsync<T>(string rol, Func<ReunionesMemoriaTools, Task<T>> accion)
    {
        using var scope = _portal.Services.CreateScope();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier,   Guid.NewGuid().ToString()),
            new(AppClaims.UserId,            Guid.NewGuid().ToString()),
            new(ClaimTypes.Name,             "Usuario de prueba"),
            new(ClaimTypes.Role,             rol),
            new(AppClaims.ActiveRol,         rol),
            new(AppClaims.ActiveInstitucion, "DIGER"),
            new(AppClaims.ActiveArea,        PortalFactory.AreaDeCasa),
            new(AppClaims.ActiveUnidad,      PortalFactory.UnidadDeCasa),
        };
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "McpBearer"));
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = contexto;

        return await accion(ActivatorUtilities.CreateInstance<ReunionesMemoriaTools>(scope.ServiceProvider));
    }

    /// <summary>Lo que el cliente recibe: el objeto serializado como JSON.</summary>
    private static JsonElement Json(object resultado) =>
        JsonSerializer.SerializeToElement(resultado, JsonSerializerOptions.Web);

    private async Task<Reunion> LeerAsync(int id)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Reuniones.IgnoreQueryFilters().AsNoTracking()
            .Include(r => r.Acuerdos).SingleAsync(r => r.Id == id);
    }

    [Fact]
    public async Task Las_instrucciones_dependen_del_tipo_de_la_reunion()
    {
        var cap = Json(await ComoMcpAsync("Empleado", t => t.InstruccionesMemoriaReunion(_capacitacionId)));
        var reu = Json(await ComoMcpAsync("Empleado", t => t.InstruccionesMemoriaReunion(_reunionId)));

        cap.GetProperty("reunionId").GetInt32().Should().Be(_capacitacionId);
        cap.GetProperty("titulo").GetString().Should().Be("Capacitación en la plataforma");
        cap.GetProperty("tipo").GetString().Should().Be("Capacitación");
        cap.GetProperty("esCapacitacion").GetBoolean().Should().BeTrue();
        cap.GetProperty("encuestaActiva").GetBoolean().Should().BeTrue();
        cap.GetProperty("formato").GetString().Should().Contain("validador_institucion");
        cap.GetProperty("formato").GetString().Should().Contain("num_asistentes");
        cap.GetProperty("prompt").GetString().Should().Be(
            MemoriaDesdeTranscripcion.Instrucciones(esCapacitacion: true, conEncuesta: true).Prompt,
            "las reglas son las de MemoriaDesdeTranscripcion, no una copia");
        cap.GetProperty("campos").EnumerateArray()
            .Select(c => c.GetProperty("clave").GetString())
            .Should().Contain(["convocados", "validador_institucion"]);

        reu.GetProperty("esCapacitacion").GetBoolean().Should().BeFalse();
        reu.GetProperty("encuestaActiva").GetBoolean().Should().BeFalse();
        reu.GetProperty("formato").GetString().Should().NotContain("validador_institucion")
            .And.NotContain("num_asistentes");
    }

    [Fact]
    public async Task Llenar_por_el_MCP_escribe_y_devuelve_el_mensaje()
    {
        const string json = """
            {
              "objetivo": "Revisar el avance del trámite",
              "acuerdos": [ { "compromiso": "Enviar el borrador", "responsable": "IHTT" } ],
              "titulo": "No se toca"
            }
            """;

        var resultado = Json(await ComoMcpAsync("JefeArea", t => t.LlenarMemoriaReunion(_reunionId, json)));

        resultado.GetProperty("mensaje").GetString().Should().Be(
            MemoriaDesdeTranscripcion.Interpretar(json, esCapacitacion: false, conEncuesta: false).Mensaje);
        resultado.GetProperty("mensaje").GetString().Should().Contain("Generales");

        var fila = await LeerAsync(_reunionId);
        fila.ObjetivoAgenda.Should().Be("Revisar el avance del trámite");
        fila.Titulo.Should().Be("Mesa técnica");
        fila.Acuerdos.Should().ContainSingle(a => a.Compromiso == "Enviar el borrador" && a.Responsable == "IHTT");
    }

    [Fact]
    public async Task Sin_Reuniones_Editar_no_escribe()
    {
        var accion = () => ComoMcpAsync("Empleado",
            t => t.LlenarMemoriaReunion(_reunionId, """{ "objetivo": "Escrito sin permiso" }"""));

        (await accion.Should().ThrowAsync<McpPermisoDenegadoException>())
            .WithMessage("*Reuniones.Editar*");

        (await LeerAsync(_reunionId)).ObjetivoAgenda.Should().NotBe("Escrito sin permiso");
    }

    /// <summary>Una regla de negocio del comando (aquí: el campo ya tiene otro valor) tiene que
    /// llegarle a la IA con su texto; una excepción común el SDK la convierte en «An error
    /// occurred invoking» y la IA no sabe que debe preguntarle a la persona.</summary>
    [Fact]
    public async Task Una_regla_del_comando_llega_a_la_IA_con_su_mensaje()
    {
        var accion = () => ComoMcpAsync("JefeArea",
            t => t.LlenarMemoriaReunion(_reunionConObjetivoId, """{ "objetivo": "Otro objetivo" }"""));

        (await accion.Should().ThrowAsync<McpException>())
            .WithMessage(LlenarMemoriaReunionCommandHandler.HayCamposConInformacion);

        (await LeerAsync(_reunionConObjetivoId)).ObjetivoAgenda.Should().Be("Objetivo que escribió una persona");
    }

    [Fact]
    public async Task Sobreescribir_reemplaza_el_valor_existente()
    {
        await ComoMcpAsync("JefeArea",
            t => t.LlenarMemoriaReunion(_reunionConObjetivoId, """{ "objetivo": "Otro objetivo" }""", sobreescribir: true));

        (await LeerAsync(_reunionConObjetivoId)).ObjetivoAgenda.Should().Be("Otro objetivo");
    }

    [Fact]
    public async Task Una_reunion_fuera_de_alcance_no_se_lee_ni_se_escribe()
    {
        var leer = () => ComoMcpAsync("JefeArea", t => t.InstruccionesMemoriaReunion(_reunionDeOtraInstitucionId));
        var escribir = () => ComoMcpAsync("JefeArea",
            t => t.LlenarMemoriaReunion(_reunionDeOtraInstitucionId, """{ "objetivo": "Ajeno" }"""));

        (await leer.Should().ThrowAsync<McpPermisoDenegadoException>())
            .WithMessage($"No se encontró la reunión {_reunionDeOtraInstitucionId}*");
        await escribir.Should().ThrowAsync<McpPermisoDenegadoException>();

        (await LeerAsync(_reunionDeOtraInstitucionId)).ObjetivoAgenda.Should().BeNull();
    }

    [Fact]
    public async Task La_transcripcion_de_una_reunion_sin_Teams_le_dice_a_la_IA_que_la_pida()
    {
        var accion = () => ComoMcpAsync("Empleado", t => t.ObtenerTranscripcionReunion(_reunionId));

        (await accion.Should().ThrowAsync<McpException>())
            .WithMessage(GetTranscripcionReunionQueryHandler.SinTeams);
    }

    [Fact]
    public async Task La_transcripcion_de_una_reunion_fuera_de_alcance_no_se_lee()
    {
        var accion = () => ComoMcpAsync("JefeArea", t => t.ObtenerTranscripcionReunion(_reunionDeOtraInstitucionId));

        (await accion.Should().ThrowAsync<McpPermisoDenegadoException>())
            .WithMessage($"No se encontró la reunión {_reunionDeOtraInstitucionId}*");
    }
}
