using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

public sealed class NotificacionAgrupadaTests
{
    private static readonly DateTime T0 = new(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
    private static string Plural(int n) => $"Tiene {n} compromisos vencidos";

    [Fact]
    public void Un_aviso_nuevo_tiene_un_elemento_y_lleva_directo()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.CompromisoVencido, "Compromiso vencido: Acta", "/Reuniones/CompromisoDetalle/1", ahoraUtc: T0);

        n.Cantidad.Should().Be(1);
        n.Elementos.Should().ContainSingle(e => e.Clave == "/Reuniones/CompromisoDetalle/1");
        n.Destino.Should().Be("/Reuniones/CompromisoDetalle/1");
        n.FechaActualizacion.Should().Be(T0);
    }

    [Fact]
    public void Sumar_otro_elemento_cambia_el_titulo_y_el_destino()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.CompromisoVencido, "Compromiso vencido: Acta", "/c/1", ahoraUtc: T0);

        var sumo = n.Sumar("/c/2", "Compromiso vencido: Informe", "/c/2", Plural, T0.AddMinutes(15));

        sumo.Should().BeTrue();
        n.Cantidad.Should().Be(2);
        n.Titulo.Should().Be("Tiene 2 compromisos vencidos");
        n.Destino.Should().Be(Notificacion.RutaDetalle(n.Id));
        n.FechaActualizacion.Should().Be(T0.AddMinutes(15));
    }

    [Fact]
    public void El_mismo_elemento_no_cuenta_dos_veces_pero_sube_el_aviso()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.ChatRecibido, "Nuevo chat de Ana", "/Chat#4", ahoraUtc: T0);

        var sumo = n.Sumar("/Chat#4", "Nuevo chat de Ana", "/Chat#4", x => $"{x} chats nuevos", T0.AddMinutes(1));

        sumo.Should().BeFalse();
        n.Cantidad.Should().Be(1);
        n.Titulo.Should().Be("Nuevo chat de Ana");
        n.FechaActualizacion.Should().Be(T0.AddMinutes(1));
    }

    [Fact]
    public void No_se_suma_a_un_aviso_leido()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.CompromisoVencido, "x", "/c/1", ahoraUtc: T0);
        n.MarcarLeida();

        n.Invoking(x => x.Sumar("/c/2", "y", "/c/2", Plural, T0)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Un_titulo_largo_se_recorta_al_largo_de_la_columna()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.CompromisoVencido, new string('a', 300), "/c/1", ahoraUtc: T0);

        n.Titulo.Length.Should().Be(Notificacion.LargoTitulo);
    }
}
