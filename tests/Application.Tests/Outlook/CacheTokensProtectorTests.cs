using System.Text;
using Diger.TramitesEstado.Infrastructure.Outlook;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

/// <summary>La caché de MSAL lleva el refresh token: en la base tiene que ir ilegible, y una caché
/// que ya no se puede descifrar (llaves perdidas) es «reconectar», no un error 500.</summary>
public class CacheTokensProtectorTests
{
    private readonly CacheTokensProtector _p = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Lo_que_se_guarda_no_contiene_el_texto_original()
    {
        var claro = Encoding.UTF8.GetBytes("{\"RefreshToken\":\"0.AAAA-secreto\"}");

        var cifrado = _p.Cifrar(claro);

        Encoding.UTF8.GetString(cifrado).Should().NotContain("secreto");
        _p.Descifrar(cifrado).Should().Equal(claro);
    }

    [Fact]
    public void Una_cache_que_no_se_puede_descifrar_devuelve_null()
    {
        var otro = new CacheTokensProtector(new EphemeralDataProtectionProvider());
        var ajeno = otro.Cifrar([1, 2, 3]);

        _p.Descifrar(ajeno).Should().BeNull();
    }

    [Fact]
    public void Una_cache_vacia_es_nula_sin_lanzar()
    {
        _p.Descifrar([]).Should().BeNull();
    }
}
