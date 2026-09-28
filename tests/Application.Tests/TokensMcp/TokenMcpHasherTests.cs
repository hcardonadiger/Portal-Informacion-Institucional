using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.TokensMcp;

/// <summary>
/// El hasher de los tokens MCP. Lo que se protege acá es que el secreto nunca quede guardado
/// en claro y que la verificación no acepte nada que no sea el token exacto.
/// </summary>
public class TokenMcpHasherTests
{
    private readonly TokenMcpHasher _hasher = new();

    [Fact]
    public void El_token_tiene_la_forma_pdt_punto_prefijo_punto_secreto()
    {
        var (token, prefijo, _) = _hasher.Generar();

        token.Should().StartWith("pdt.");
        token.Split('.').Should().HaveCount(3);
        token.Split('.')[1].Should().Be(prefijo);
    }

    /// <summary>
    /// Regresión. El separador fue un guión bajo hasta que esto lo cazó: el alfabeto de
    /// Base64Url incluye <c>-</c> y <c>_</c>, así que cada vez que el azar metía un guión bajo
    /// en el secreto —una buena parte de las veces— el token se partía en cuatro trozos,
    /// <c>LeerPrefijo</c> lo rechazaba por malformado y esa credencial no validaba nunca.
    /// Con una sola muestra la prueba pasaba; hacen falta muchas para que salga.
    /// </summary>
    [Fact]
    public void Todo_token_generado_se_puede_volver_a_leer()
    {
        for (var i = 0; i < 500; i++)
        {
            var (token, prefijo, hash) = _hasher.Generar();

            _hasher.LeerPrefijo(token).Should().Be(prefijo,
                $"el token «{token}» tiene que poder descomponerse igual que se armó");
            _hasher.Verificar(hash, token).Should().BeTrue();
        }
    }

    /// <summary>El separador no puede pertenecer al alfabeto de lo que separa.</summary>
    [Fact]
    public void El_separador_no_aparece_dentro_del_prefijo_ni_del_secreto()
    {
        for (var i = 0; i < 200; i++)
        {
            var (token, _, _) = _hasher.Generar();
            var partes = token.Split('.');

            partes.Should().HaveCount(3);
            partes[1].Should().NotContain(".");
            partes[2].Should().NotContain(".");
        }
    }

    /// <summary>Va en una cabecera HTTP: sin '+', '/' ni relleno '='.</summary>
    [Fact]
    public void El_token_es_seguro_para_una_cabecera_http()
    {
        for (var i = 0; i < 50; i++)
        {
            var (token, _, _) = _hasher.Generar();
            token.Should().NotContainAny("+", "/", "=");
        }
    }

    [Fact]
    public void Lo_que_se_guarda_no_es_el_token()
    {
        var (token, prefijo, hash) = _hasher.Generar();

        hash.Should().NotBe(token);
        hash.Should().NotContain(token.Split('.')[2], "el secreto no puede quedar legible en la base");
        prefijo.Should().NotBe(token);
    }

    [Fact]
    public void Verificar_acepta_el_token_que_genero()
    {
        var (token, _, hash) = _hasher.Generar();

        _hasher.Verificar(hash, token).Should().BeTrue();
    }

    [Fact]
    public void Verificar_rechaza_otro_token()
    {
        var (_, _, hash) = _hasher.Generar();
        var (otro, _, _) = _hasher.Generar();

        _hasher.Verificar(hash, otro).Should().BeFalse();
    }

    [Fact]
    public void Verificar_rechaza_el_token_alterado_en_un_caracter()
    {
        var (token, _, hash) = _hasher.Generar();

        var alterado = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');

        _hasher.Verificar(hash, alterado).Should().BeFalse();
    }

    /// <summary>El prefijo solo sirve para encontrar la fila. Presentarlo no es presentar el token.</summary>
    [Fact]
    public void Verificar_rechaza_el_prefijo_solo()
    {
        var (_, prefijo, hash) = _hasher.Generar();

        _hasher.Verificar(hash, prefijo).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-hash-base64")]
    public void Verificar_no_revienta_con_un_hash_invalido(string hash)
    {
        var (token, _, _) = _hasher.Generar();

        _hasher.Verificar(hash, token).Should().BeFalse();
    }

    [Fact]
    public void Dos_tokens_generados_no_se_repiten()
    {
        var generados = Enumerable.Range(0, 200).Select(_ => _hasher.Generar()).ToList();

        generados.Select(g => g.Token).Distinct().Should().HaveCount(200);
        generados.Select(g => g.Prefijo).Distinct().Should().HaveCount(200,
            "el prefijo tiene índice único: una colisión rompería la emisión");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sinprefijo")]
    [InlineData("otro.prefijo.secreto")]
    [InlineData("pdt.solodospartes")]
    [InlineData("pdt..secreto")]
    [InlineData("pdt.prefijo.")]
    [InlineData("pdt.prefijo.secreto.demas")]
    [InlineData("pdt_prefijo_secreto")]
    public void LeerPrefijo_devuelve_null_ante_algo_que_no_es_un_token(string entrada)
    {
        _hasher.LeerPrefijo(entrada).Should().BeNull();
    }

    [Fact]
    public void LeerPrefijo_extrae_el_prefijo_de_un_token_valido()
    {
        var (token, prefijo, _) = _hasher.Generar();

        _hasher.LeerPrefijo(token).Should().Be(prefijo);
    }
}
