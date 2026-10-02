using Diger.TramitesEstado.Application.Reuniones.Common;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>
/// Documentos y enlaces del cierre de una reunión (<see cref="ReunionAdjunto"/>).
///
/// <para>Son hijos del agregado y se reemplazan en bloque como los acuerdos, así que lo que hay que
/// defender es el ida y vuelta: que guardar dos veces no duplique, que una fila a medio llenar no
/// bote la pantalla, y que un enlace escrito a mano no pueda traer un esquema peligroso —el acta la
/// ven más personas de las que pueden editar la reunión.</para>
/// </summary>
public class AdjuntosDeReunionTests
{
    private static ReunionFormDto Form(params ReunionAdjuntoInput[] adjuntos) => new()
    {
        Titulo = "Taller de modelado",
        Adjuntos = [.. adjuntos]
    };

    private static ReunionAdjuntoInput Enlace(string titulo, string? url,
        SeccionAdjuntoReunion seccion = SeccionAdjuntoReunion.Documento) => new()
        {
            Seccion = seccion,
            Clase   = ClaseAdjuntoReunion.Enlace,
            Titulo  = titulo,
            Url     = url
        };

    private static ReunionAdjuntoInput Archivo(string titulo, string url, string nombre, long tamano = 1024) => new()
    {
        Seccion       = SeccionAdjuntoReunion.Documento,
        Clase         = ClaseAdjuntoReunion.Archivo,
        Titulo        = titulo,
        Url           = url,
        NombreArchivo = nombre,
        Tamano        = tamano
    };

    // ── Enlaces: lo que puede escribir una persona ────────────────────────────

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("file:///C:/Windows/System32/drivers/etc/hosts")]
    [InlineData("vbscript:msgbox(1)")]
    public void Un_enlace_con_esquema_peligroso_se_rechaza(string url)
    {
        var r = Reunion.Crear("Taller de modelado");

        var guardar = () => ReunionMapper.Aplicar(r, Form(Enlace("Grabación", url)), [], []);

        guardar.Should().Throw<DomainException>(
            "el acta pinta esta dirección en un href y la ve más gente de la que puede editar la reunión");
    }

    [Theory]
    [InlineData("onedrive.com/algo")]      // sin esquema: el navegador lo leería como ruta relativa
    [InlineData("/uploads/reuniones/x.pdf")]
    public void Un_enlace_que_no_es_una_direccion_absoluta_se_rechaza(string url)
    {
        var r = Reunion.Crear("Taller de modelado");

        var guardar = () => ReunionMapper.Aplicar(r, Form(Enlace("Grabación", url)), [], []);

        guardar.Should().Throw<DomainException>();
    }

    [Fact]
    public void Un_enlace_en_blanco_se_descarta_sin_quejarse()
    {
        // No es lo mismo escribir mal una dirección que no escribir ninguna: lo primero merece un
        // mensaje, lo segundo es un renglón vacío y se tira sin molestar a nadie.
        var r = Reunion.Crear("Taller de modelado");

        ReunionMapper.Aplicar(r, Form(Enlace("Grabación", "   ")), [], []);

        r.Adjuntos.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://digerhn-my.sharepoint.com/:v:/g/personal/abc/XYZ?e=aB1cD2")]
    [InlineData("http://intranet.diger.gob.hn/material/taller.zip")]
    public void Un_enlace_http_o_https_se_acepta(string url)
    {
        var r = Reunion.Crear("Taller de modelado");

        ReunionMapper.Aplicar(r, Form(Enlace("Grabación del taller", url)), [], []);

        r.Adjuntos.Should().HaveCount(1);
        r.Adjuntos.Single().Clase.Should().Be(ClaseAdjuntoReunion.Enlace);
        r.Adjuntos.Single().Url.Should().StartWith(url.Split(':')[0] + "://");
    }

    [Fact]
    public void El_tope_de_largo_de_la_url_aguanta_un_enlace_compartido_de_onedrive()
    {
        // Un enlace de OneDrive compartido trae el token en el query string. Por eso la columna es
        // nvarchar(1000) y no los 600 que alcanzan para una ruta local.
        var largo = "https://digerhn-my.sharepoint.com/:v:/g/personal/usuario_diger_gob_hn/"
                  + new string('X', 700) + "?e=aB1cD2";

        var r = Reunion.Crear("Taller de modelado");
        ReunionMapper.Aplicar(r, Form(Enlace("Grabación", largo)), [], []);

        r.Adjuntos.Single().Url.Length.Should().BeLessThanOrEqualTo(1000);
    }

    // ── Filas a medio llenar ──────────────────────────────────────────────────

    [Fact]
    public void Una_fila_sin_destino_se_descarta_en_vez_de_fallar()
    {
        var r = Reunion.Crear("Taller de modelado");

        ReunionMapper.Aplicar(r, Form(
            Enlace("Fila que alguien agregó y dejó vacía", null),
            Enlace("Grabación", "https://ejemplo.hn/video")), [], []);

        r.Adjuntos.Should().HaveCount(1, "el renglón en blanco no es un error, es un renglón en blanco");
        r.Adjuntos.Single().Titulo.Should().Be("Grabación");
    }

    [Fact]
    public void Un_archivo_sin_titulo_toma_el_nombre_del_archivo()
    {
        // Hacer fallar esto botaría al editor y, con él, el archivo que la persona acaba de subir.
        var r = Reunion.Crear("Taller de modelado");

        ReunionMapper.Aplicar(r, Form(
            Archivo(titulo: "  ", url: "/uploads/reuniones/abc123.pdf", nombre: "Acta firmada.pdf")), [], []);

        r.Adjuntos.Single().Titulo.Should().Be("Acta firmada.pdf");
    }

    [Fact]
    public void Un_enlace_sin_titulo_toma_su_propia_direccion()
    {
        var r = Reunion.Crear("Taller de modelado");

        ReunionMapper.Aplicar(r, Form(Enlace("", "https://ejemplo.hn/video")), [], []);

        r.Adjuntos.Single().Titulo.Should().Be("https://ejemplo.hn/video");
    }

    // ── Reemplazo en bloque ───────────────────────────────────────────────────

    [Fact]
    public void Guardar_dos_veces_no_duplica_los_adjuntos()
    {
        var r = Reunion.Crear("Taller de modelado");
        var form = Form(
            Archivo("Presentación", "/uploads/reuniones/a.pptx", "Presentación.pptx"),
            Enlace("Grabación", "https://ejemplo.hn/video", SeccionAdjuntoReunion.Evidencia));

        ReunionMapper.Aplicar(r, form, [], []);
        var (datos, asistentes, acuerdos) = ReunionMapper.ToForm(r);
        ReunionMapper.Aplicar(r, datos, asistentes, acuerdos);

        r.Adjuntos.Should().HaveCount(2, "los hijos se reemplazan en bloque, no se acumulan");
    }

    [Fact]
    public void Los_adjuntos_vuelven_al_formulario_separados_por_tarjeta()
    {
        var r = Reunion.Crear("Taller de modelado");
        ReunionMapper.Aplicar(r, Form(
            Archivo("Presentación", "/uploads/reuniones/a.pptx", "Presentación.pptx"),
            Enlace("Lista de asistencia", "https://ejemplo.hn/lista"),
            Enlace("Grabación", "https://ejemplo.hn/video", SeccionAdjuntoReunion.Evidencia)), [], []);

        var (datos, _, _) = ReunionMapper.ToForm(r);

        datos.Adjuntos.Where(a => a.Seccion == SeccionAdjuntoReunion.Documento).Should().HaveCount(2);
        datos.Adjuntos.Where(a => a.Seccion == SeccionAdjuntoReunion.Evidencia).Should().HaveCount(1);
    }

    [Fact]
    public void El_orden_se_numera_por_tarjeta_y_no_corrido()
    {
        // Cada tarjeta se muestra por su cuenta; si el orden fuera corrido, la evidencia
        // arrancaría en 2 y el primer enlace no se vería primero al reabrir.
        var r = Reunion.Crear("Taller de modelado");

        ReunionMapper.Aplicar(r, Form(
            Enlace("Doc 1", "https://ejemplo.hn/1"),
            Enlace("Doc 2", "https://ejemplo.hn/2"),
            Enlace("Evi 1", "https://ejemplo.hn/3", SeccionAdjuntoReunion.Evidencia),
            Enlace("Evi 2", "https://ejemplo.hn/4", SeccionAdjuntoReunion.Evidencia)), [], []);

        r.Adjuntos.Where(a => a.Seccion == SeccionAdjuntoReunion.Documento)
            .Select(a => a.Orden).Should().BeEquivalentTo([0, 1]);
        r.Adjuntos.Where(a => a.Seccion == SeccionAdjuntoReunion.Evidencia)
            .Select(a => a.Orden).Should().BeEquivalentTo([0, 1]);
    }

    [Fact]
    public void Quitar_una_fila_y_guardar_la_borra_de_verdad()
    {
        var r = Reunion.Crear("Taller de modelado");
        ReunionMapper.Aplicar(r, Form(
            Enlace("Doc 1", "https://ejemplo.hn/1"),
            Enlace("Doc 2", "https://ejemplo.hn/2")), [], []);

        var (datos, _, _) = ReunionMapper.ToForm(r);
        datos.Adjuntos.RemoveAt(0);
        ReunionMapper.Aplicar(r, datos, [], []);

        r.Adjuntos.Should().HaveCount(1);
        r.Adjuntos.Single().Titulo.Should().Be("Doc 2");
    }

    [Fact]
    public void Una_reunion_sin_adjuntos_los_deja_vacios()
    {
        var r = Reunion.Crear("Taller de modelado");

        ReunionMapper.Aplicar(r, Form(), [], []);

        r.Adjuntos.Should().BeEmpty();
    }

    // ── Lo que quedó del detalle de capacitación ──────────────────────────────

    [Fact]
    public void Guardar_una_reunion_vieja_no_borra_su_detalle_de_capacitacion()
    {
        // «Detalle de capacitación» se quitó del portal el 2026-10-02, pero sus columnas se
        // quedaron con lo que ya tenían. Que el mapper no las toque es lo que hace que una reunión
        // importada de Supabase no pierda ese texto la primera vez que alguien la abre y guarda.
        var r = Reunion.Crear("Capacitación de 2026");
        r.ObjetivoCap = "Que el personal domine la ventanilla única";
        r.Contenido   = "Módulo 1\nMódulo 2";

        ReunionMapper.Aplicar(r, Form(), [], []);

        r.ObjetivoCap.Should().Be("Que el personal domine la ventanilla única");
        r.Contenido.Should().Be("Módulo 1\nMódulo 2");
    }

    [Fact]
    public void El_tema_sigue_viajando_de_ida_y_vuelta()
    {
        // El Tema era la única parte de «Detalle de capacitación» que no repetía nada: no lo
        // captura ninguna pantalla, lo trae el importador, y ahora encabeza la memoria del acta.
        var r = Reunion.Crear("Capacitación de 2026");
        var form = Form();
        form.Tema = "Ventanilla única";

        ReunionMapper.Aplicar(r, form, [], []);
        var (datos, _, _) = ReunionMapper.ToForm(r);

        datos.Tema.Should().Be("Ventanilla única");
    }
}
