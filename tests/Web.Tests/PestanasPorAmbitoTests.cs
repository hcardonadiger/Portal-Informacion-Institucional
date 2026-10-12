using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Application.Common.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Las pestañas de Expedientes y SIGER solo salen en el ámbito al que están acotadas.
///
/// <para>Es la cadena completa —claims → <c>CurrentUserService</c> → <c>PermissionCache</c> →
/// <c>AccesoModulosService</c> → navbar— y estaba sin probar de punta a punta. La lógica de
/// recorte tiene sus propias pruebas en <c>AmbitoDeModulosTests</c>; lo que se verifica acá es que
/// el navbar efectivamente la consulte, que es donde el usuario nota la fuga.</para>
///
/// <para>Cuando esto falla no hay excepción ni registro: simplemente aparece una pestaña de más.
/// Por eso la prueba afirma las dos caras —que se ve donde debe y que no se ve donde no debe—: una
/// prueba que solo comprueba la ausencia pasaría igual si el navbar dejara de pintar el módulo
/// para todo el mundo.</para>
/// </summary>
public sealed class PestanasPorAmbitoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private const string Inst   = "DIGER";
    private const string Area   = "GOBDIG";
    private const string Unidad = "DITRA";

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        // «Empleado» es alcance Unidad y no tiene ninguna capacidad de administrador, así que es
        // el rol que de verdad pasa por el recorte. Un Administrador lo saltearía por código.
        // Reuniones.Ver es el contraste: un módulo sin acotar, para distinguir «el recorte
        // funciona» de «el navbar no está pintando nada».
        await _portal.OtorgarAsync("Empleado", "Expedientes.Ver", "Siger.Ver", "Reuniones.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Las mismas dos filas que hay hoy en la base.
        db.ModuloAmbitos.AddRange(
            ModuloAmbito.Crear("Expedientes", Inst, Area, Unidad),
            ModuloAmbito.Crear("Siger",       Inst, Area, Unidad));
        await db.SaveChangesAsync();

        scope.ServiceProvider.GetRequiredService<IPermissionCache>().InvalidarTodo();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>El navbar visto por un «Empleado» ubicado en el área y unidad indicadas.</summary>
    private async Task<string> NavbarAsync(string? area, string? unidad)
    {
        var cliente = _portal.ClienteComo("Empleado");
        if (area is not null) cliente.DefaultRequestHeaders.Add(TestAuthHandler.CabeceraArea, area);
        if (unidad is not null) cliente.DefaultRequestHeaders.Add(TestAuthHandler.CabeceraUnidad, unidad);

        var r = await cliente.GetAsync("/Reuniones");
        r.IsSuccessStatusCode.Should().BeTrue("se necesita una página que el Empleado sí pueda abrir");

        var html = System.Net.WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        return System.Text.RegularExpressions.Regex.Match(html, "(?s)<nav.*?</nav>").Value;
    }

    private static void TienePestana(string navbar, string etiqueta) =>
        navbar.Should().MatchRegex($@">\s*{etiqueta}\s*<", $"«{etiqueta}» debe estar en el navbar");

    private static void NoTienePestana(string navbar, string etiqueta) =>
        navbar.Should().NotMatchRegex($@">\s*{etiqueta}\s*<", $"«{etiqueta}» NO debe estar en el navbar");

    [Fact]
    public async Task Digitalizacion_de_tramites_ve_las_dos_pestanas()
    {
        var navbar = await NavbarAsync(Area, Unidad);

        TienePestana(navbar, "Expedientes");
        TienePestana(navbar, "SIGER");
    }

    [Theory]
    [InlineData("GOBDIG", "OTRAUNIDAD")]   // misma área, otra unidad
    [InlineData("OTRAAREA", "OTRAUNIDAD")] // otra área
    [InlineData("OTRAAREA", null)]         // sin unidad
    [InlineData(null, null)]               // sin área ni unidad
    public async Task Fuera_de_digitalizacion_no_sale_ninguna_de_las_dos(string? area, string? unidad)
    {
        var navbar = await NavbarAsync(area, unidad);

        NoTienePestana(navbar, "Expedientes");
        NoTienePestana(navbar, "SIGER");
    }

    [Fact]
    public async Task Las_pestanas_de_siempre_siguen_saliendo_fuera_del_ambito()
    {
        // El contraste que hace útil a la prueba anterior: lo que no está acotado no se toca.
        var navbar = await NavbarAsync("OTRAAREA", "OTRAUNIDAD");

        TienePestana(navbar, "Reuniones");
    }
}
