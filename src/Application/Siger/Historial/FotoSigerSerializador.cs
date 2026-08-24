using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diger.TramitesEstado.Application.Siger.Historial;

/// <summary>Convierte una ficha SIGER en el documento que guarda el archivo, y de vuelta.</summary>
public static class FotoSigerSerializador
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        // Sin este encoder, «Prórroga de estadía» se guarda como «Prórroga de estadía».
        // El archivo está hecho para que una persona lo pueda abrir y leer dentro de años; un
        // documento lleno de escapes cumple la letra de «no perder la información» y traiciona
        // su intención. No hay riesgo de inyección: esto nunca se interpola en HTML ni en SQL.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    /// <summary>Retrata la ficha con las colecciones que traiga cargadas. Si alguna viene vacía
    /// porque no se cargó, la foto queda incompleta —de ahí que quien llame deba cargarlas todas.</summary>
    public static FichaFoto Retratar(TramiteSiger t) => new(
        t.Id, t.IdSiger, t.Codigo, t.Nombre, t.Institucion, t.Sigla, t.Dependencia,
        t.Descripcion, t.Objetivo, t.DirigidoA, t.EstadoSiger, t.Publicado,
        t.DisponibleEnLinea, t.EnPlanDigitalizacion, t.VigenciaDocumento, t.Temporalidad,
        t.DiagramaUrl, t.EnlacePrincipal, t.ObservacionesDiger, t.FechaIngreso,
        t.UltimaModificacion, t.InstitucionId, t.EstaEnSol, t.SolUrl, t.SolVerificadoEl,
        t.CategoriaId, t.CostoTexto, t.CostoEsGratuito, t.TiempoTexto, t.Modalidad, t.EsPopular,
        [.. t.Pasos.OrderBy(p => p.NumeroPaso)
                   .Select(p => new PasoFoto(p.NumeroPaso, p.Descripcion, p.LugarDependencia,
                                             p.SalidaResultado, p.TiempoRegistrado, p.Titulo, p.Modalidad))],
        [.. t.Requisitos.OrderBy(r => r.Numero)
                        .Select(r => new RequisitoFoto(r.Numero, r.Requisito, r.Tipo,
                                                       r.DocumentoSoporte, r.Formato))],
        [.. t.Entregables.OrderBy(e => e.Numero)
                         .Select(e => new EntregableFoto(e.Numero, e.Entregable, e.Formato, e.Presentacion))],
        [.. t.LugaresAtencion.OrderBy(l => l.Numero)
                             .Select(l => new LugarFoto(l.Numero, l.Lugar, l.Ciudad, l.Direccion, l.Telefonos))],
        [.. t.Enlaces.OrderBy(e => e.Numero)
                     .Select(e => new EnlaceFoto(e.Numero, e.Url, e.Tipo))],
        [.. t.TareasDigitalizacion.OrderBy(x => x.NumeroTarea)
                                  .Select(x => new TareaFoto(x.NumeroTarea, x.Descripcion,
                                                             x.Estado, x.FechaCumplimiento))]);

    public static string Serializar(FichaFoto foto) => JsonSerializer.Serialize(foto, Opciones);

    /// <summary>Lee un documento del archivo. Devuelve null si está corrupto o vacío: el archivo
    /// se consulta desde pantallas, y una foto ilegible no debe tumbar la página que la muestra.</summary>
    public static FichaFoto? Leer(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return null;
        try
        {
            return JsonSerializer.Deserialize<FichaFoto>(contenido, Opciones);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
