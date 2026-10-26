using Diger.TramitesEstado.Application.Expedientes.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

/// <summary>
/// El equipo institucional del expediente, que reemplazó al «Contacto institucional principal».
///
/// Lo delicado no es guardar la lista sino lo que cuelga de ella: la prioridad 1 se copia a
/// <c>Expedientes.Contacto*</c>, que es lo que leen el resumen, los recordatorios y los
/// informes. Si el equipo y el contacto se desacoplan, el portal le escribe a alguien que ya
/// no es el primero de la lista.
/// </summary>
public class EquipoInstitucionalTests
{
    [Fact]
    public void La_prioridad_1_es_el_contacto_principal()
    {
        var e = Vacio();

        Aplicar(e, Miembro(1, "Ana Rivera", "Jefa de TI", "ana@inst.hn", "2222-0000"),
                   Miembro(2, "Luis Paz", "Enlace", null, null));

        e.ContactoNombre.Should().Be("Ana Rivera");
        e.ContactoCargo.Should().Be("Jefa de TI");
        e.ContactoCorreo.Should().Be("ana@inst.hn");
        e.ContactoTel.Should().Be("2222-0000");
        ExpedienteMapper.ToInputDto(e).Equipo!.Select(m => (m.Prioridad, m.Nombre))
            .Should().Equal((1, "Ana Rivera"), (2, "Luis Paz"));
    }

    /// <summary>Reordenar en el editor es la forma de decir «ahora atiende otro primero».</summary>
    [Fact]
    public void Reordenar_cambia_el_contacto_principal()
    {
        var e = Vacio();
        Aplicar(e, Miembro(1, "Ana Rivera"), Miembro(2, "Luis Paz", correo: "luis@inst.hn"));

        Aplicar(e, Miembro(1, "Luis Paz", correo: "luis@inst.hn"), Miembro(2, "Ana Rivera"));

        e.ContactoNombre.Should().Be("Luis Paz");
        e.ContactoCorreo.Should().Be("luis@inst.hn");
        e.Equipo.OrderBy(m => m.Prioridad).Select(m => m.Nombre).Should().Equal("Luis Paz", "Ana Rivera");
    }

    /// <summary>La prioridad que se guarda es 1..n sin huecos ni empates, venga como venga:
    /// un empate no dice quién atiende primero.</summary>
    [Fact]
    public void La_prioridad_se_renumera_sin_huecos_ni_empates()
    {
        var e = Vacio();

        Aplicar(e, Miembro(5, "Tercero"), Miembro(2, "Primero"), Miembro(2, "Segundo"));

        e.Equipo.OrderBy(m => m.Prioridad).Select(m => (m.Prioridad, m.Nombre))
            .Should().Equal((1, "Primero"), (2, "Segundo"), (3, "Tercero"));
    }

    [Fact]
    public void Una_fila_sin_nombre_no_se_guarda_ni_se_vuelve_contacto()
    {
        var e = Vacio();

        Aplicar(e, Miembro(1, "  ", correo: "suelto@inst.hn"), Miembro(2, "Luis Paz"));

        e.Equipo.Should().ContainSingle().Which.Prioridad.Should().Be(1);
        e.ContactoNombre.Should().Be("Luis Paz");
        e.ContactoCorreo.Should().BeNull();
    }

    [Fact]
    public void Vaciar_el_equipo_deja_al_expediente_sin_contacto()
    {
        var e = Vacio();
        Aplicar(e, Miembro(1, "Ana Rivera", correo: "ana@inst.hn"));

        Aplicar(e);

        e.Equipo.Should().BeEmpty();
        e.ContactoNombre.Should().BeNull();
        e.ContactoCorreo.Should().BeNull();
    }

    /// <summary>
    /// Quien llama sin conocer el equipo (un importador, una llamada de antes) manda
    /// <c>Equipo = null</c>. Eso no es «vaciarlo»: el equipo guardado se queda y el contacto
    /// se toma del DTO, como antes de que existiera el equipo.
    /// </summary>
    [Fact]
    public void Sin_equipo_en_el_DTO_se_conserva_el_guardado()
    {
        var e = Vacio();
        Aplicar(e, Miembro(1, "Ana Rivera"), Miembro(2, "Luis Paz"));

        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e) with
        {
            Equipo = null, ContactoNombre = "Escrito a mano"
        });

        e.Equipo.Select(m => m.Nombre).Should().BeEquivalentTo("Ana Rivera", "Luis Paz");
        e.ContactoNombre.Should().Be("Escrito a mano");
    }

    [Fact]
    public void Guardar_dos_veces_lo_mismo_no_cambia_el_equipo()
    {
        var e = Vacio();
        Aplicar(e, Miembro(1, "Ana Rivera"), Miembro(2, "Luis Paz"));

        var cambios = ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e));

        cambios.Should().BeNull();
        e.Equipo.OrderBy(m => m.Prioridad).Select(m => m.Nombre).Should().Equal("Ana Rivera", "Luis Paz");
    }

    private static Expediente Vacio() =>
        Expediente.Crear("EXP-001", "SALUD", null, null, "SECRETARIA DE SALUD", "Analista");

    private static void Aplicar(Expediente e, params MiembroEquipoInput[] equipo) =>
        ExpedienteMapper.Aplicar(e, ExpedienteMapper.ToInputDto(e) with { Equipo = [.. equipo] });

    private static MiembroEquipoInput Miembro(
        int prioridad, string nombre, string? cargo = null, string? correo = null, string? telefono = null) =>
        new(prioridad, nombre, cargo, correo, telefono);
}
