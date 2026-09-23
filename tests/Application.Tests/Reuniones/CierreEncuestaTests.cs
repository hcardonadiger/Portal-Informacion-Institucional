using Diger.TramitesEstado.Application.Reuniones.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>La encuesta de satisfacción se enciende desde «Generales» y es lo que decide si el
/// cierre pide la segunda firma. Como es un campo propio —y no algo que se deduzca de si ya hay
/// respuestas— tiene que sobrevivir el ida y vuelta entre la entidad y el formulario: una encuesta
/// recién activada todavía no tiene ninguna respuesta, y si se dedujera aparecería apagada al
/// recargar la pantalla.</summary>
public class CierreEncuestaTests
{
    private static ReunionFormDto FormBase(bool encuestaActiva) => new()
    {
        Titulo = "Sesion de prueba",
        EncuestaActiva = encuestaActiva,
        ValDiger = "Ana Lopez",
        ValInst = "Carlos Ruiz"
    };

    [Fact]
    public void La_encuesta_activa_viaja_del_formulario_a_la_reunion()
    {
        var r = Reunion.Crear("Sesion de prueba");

        ReunionMapper.Aplicar(r, FormBase(encuestaActiva: true), [], []);

        r.EncuestaActiva.Should().BeTrue();
    }

    [Fact]
    public void Una_reunion_nace_sin_encuesta()
    {
        var r = Reunion.Crear("Sesion de prueba");

        r.EncuestaActiva.Should().BeFalse("apagada por omision: el cierre solo pide la firma de la DIGER");
    }

    [Fact]
    public void El_estado_de_la_encuesta_vuelve_al_formulario_al_reabrir()
    {
        var r = Reunion.Crear("Sesion de prueba");
        ReunionMapper.Aplicar(r, FormBase(encuestaActiva: true), [], []);

        var (datos, _, _) = ReunionMapper.ToForm(r);

        datos.EncuestaActiva.Should().BeTrue(
            "si no vuelve, al reabrir la reunion el boton aparece apagado y se pierde la segunda firma");
        datos.ValInst.Should().Be("Carlos Ruiz");
    }

    [Fact]
    public void Apagar_la_encuesta_no_borra_la_firma_de_la_institucion()
    {
        // El campo se esconde con CSS pero sigue en el formulario, asi que su valor viaja igual.
        // Si se quitara del DOM llegaria null y el guardado borraria lo ya capturado.
        var r = Reunion.Crear("Sesion de prueba");
        ReunionMapper.Aplicar(r, FormBase(encuestaActiva: true), [], []);

        var apagada = FormBase(encuestaActiva: false);
        ReunionMapper.Aplicar(r, apagada, [], []);

        r.EncuestaActiva.Should().BeFalse();
        r.ValInst.Should().Be("Carlos Ruiz", "apagar la encuesta oculta la firma, no la destruye");
    }

    [Fact]
    public void La_satisfaccion_ya_capturada_sobrevive_a_guardar_desde_el_editor()
    {
        // La calificacion y el comentario salieron del cierre: viajan como campos ocultos para que
        // lo que muestra el acta de reuniones anteriores no se pierda al volver a guardar.
        var r = Reunion.Crear("Sesion anterior");
        r.SatisfaccionCalificacion = 4;
        r.Satisfaccion = "Muy buena";

        var (datos, asistentes, acuerdos) = ReunionMapper.ToForm(r);
        ReunionMapper.Aplicar(r, datos, asistentes, acuerdos);

        r.SatisfaccionCalificacion.Should().Be(4);
        r.Satisfaccion.Should().Be("Muy buena");
    }
}
