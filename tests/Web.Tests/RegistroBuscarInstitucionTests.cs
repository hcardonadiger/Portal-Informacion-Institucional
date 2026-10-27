using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Buscador de institución del formulario público de auto-registro (el del QR). Con decenas de
/// instituciones el &lt;select&gt; se recorría a ojo desde el teléfono; ahora se acota escribiendo
/// el nombre o la sigla. Lo que se guarda sigue siendo el nombre: la sigla solo sirve para buscar.
/// </summary>
public sealed class RegistroBuscarInstitucionTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private Guid _conMuchas;
    private Guid _conPocas;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Instituciones.Add(Institucion.Crear("ADUANAS", "Administración Aduanera de Honduras", nombreCorto: "ADUANAS"));
        db.Instituciones.Add(Institucion.Crear("SECRETARIADEFINANZAS", "Secretaría de Finanzas"));
        for (var i = 1; i <= 8; i++)
            db.Instituciones.Add(Institucion.Crear($"INST{i}", $"Institución de prueba {i}"));

        // Sin convocadas: el formulario ofrece todas las activas.
        var muchas = Reunion.Crear("Reunión abierta a todas");
        muchas.InstitucionDuenaId = "DIGER";

        // Dos convocadas: no hace falta buscar entre dos.
        var pocas = Reunion.Crear("Reunión con dos convocadas");
        pocas.InstitucionDuenaId = "DIGER";
        pocas.AgregarInstitucion("ADUANAS");
        pocas.AgregarInstitucion("SECRETARIADEFINANZAS");

        db.Reuniones.AddRange(muchas, pocas);
        await db.SaveChangesAsync();
        _conMuchas = muchas.RegistroToken;
        _conPocas  = pocas.RegistroToken;
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    /// <summary>El formulario completo sale después del paso del correo: con uno que no está en el
    /// directorio la página pasa a la fase «formulario», que es donde se elige la institución.</summary>
    private async Task<string> FormularioAsync(Guid token)
    {
        var cliente = _portal.ClienteAnonimo();
        var url     = $"/Asistencia/Registro/{token}";
        var get     = await cliente.GetStringAsync(url);
        var tokenCsrf = System.Text.RegularExpressions.Regex.Match(
            get, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        var r = await cliente.PostAsync($"{url}?handler=Buscar", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", tokenCsrf),
            new("CorreoBusqueda", "nadie.registrado@ejemplo.hn")
        ]));
        r.EnsureSuccessStatusCode();
        return System.Net.WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_campo_de_institucion_es_un_buscador_y_cada_opcion_lleva_su_sigla()
    {
        var html = await FormularioAsync(_conMuchas);

        html.Should().Contain("id=\"instBuscar\"");
        html.Should().Contain("Buscar por nombre o sigla");
        html.Should().Contain("role=\"combobox\"", "se escribe y la lista se acorta con cada letra");
        html.Should().Contain("<option value=\"Administración Aduanera de Honduras\" data-sigla=\"ADUANAS\">",
            "la sigla corta viaja con la opción");
        html.Should().Contain("<option value=\"Secretaría de Finanzas\" data-sigla=\"SECRETARIADEFINANZAS\">",
            "sin sigla corta se usa el código, que sirve igual para buscar «finanzas»");
    }

    [Fact]
    public async Task Con_convocadas_el_buscador_solo_ofrece_esas()
    {
        var html = await FormularioAsync(_conPocas);

        html.Should().Contain("id=\"instBuscar\"", "el buscador va siempre, haya muchas o pocas");
        html.Should().Contain("data-sigla=\"ADUANAS\"");
        html.Should().NotContain("Institución de prueba 1", "solo se ofrecen las convocadas");
    }
}
