namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Un aviso de la campana. Puede juntar varios elementos del mismo tipo («Tiene 6 compromisos
/// vencidos»): <see cref="Sumar"/> agrega a un aviso sin leer, y al leerlo lo siguiente empieza un
/// aviso nuevo, para que nada nuevo quede escondido dentro de algo ya leído.
/// </summary>
public sealed class Notificacion : BaseEntity
{
    public const int LargoTitulo = 200;

    public Guid              DestinatarioId     { get; private set; }
    public TipoNotificacion  Tipo               { get; private set; }
    public string            Titulo             { get; private set; } = "";
    public string?           Url                { get; private set; }
    public bool              Leida              { get; private set; }
    public DateTime          FechaCreacion      { get; private set; }
    public DateTime?         FechaLectura       { get; private set; }
    public int               Cantidad           { get; private set; } = 1;

    /// <summary>Cuándo entró el último elemento. La campana ordena por esto.</summary>
    public DateTime          FechaActualizacion { get; private set; }

    private readonly List<NotificacionElemento> _elementos = [];
    public IReadOnlyCollection<NotificacionElemento> Elementos => _elementos.AsReadOnly();

    /// <summary>Adónde lleva el aviso: con un elemento, directo a él; con varios, a la lista.</summary>
    public string? Destino => Cantidad > 1 ? RutaDetalle(Id) : Url;

    public static string RutaDetalle(int id) => $"/Notificaciones/{id}";

    private Notificacion() { }

    public static Notificacion Crear(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                                     string? url = null, string? clave = null, DateTime? ahoraUtc = null)
    {
        var ahora = ahoraUtc ?? DateTime.UtcNow;
        var n = new Notificacion
        {
            DestinatarioId     = destinatarioId,
            Tipo               = tipo,
            Titulo             = Recortar(titulo, LargoTitulo),
            Url                = url,
            Leida              = false,
            FechaCreacion      = ahora,
            FechaActualizacion = ahora,
            Cantidad           = 1,
        };
        n._elementos.Add(NotificacionElemento.Crear(clave ?? url ?? titulo, titulo, url, ahora));
        return n;
    }

    /// <summary>Junta un elemento más en este aviso.</summary>
    /// <returns>Si el elemento era nuevo. Si ya estaba, el aviso solo sube en la campana.</returns>
    public bool Sumar(string clave, string titulo, string? url, Func<int, string> tituloGrupo, DateTime ahoraUtc)
    {
        if (Leida)
            throw new InvalidOperationException("No se suma a un aviso ya leído: lo nuevo va en un aviso nuevo.");

        FechaActualizacion = ahoraUtc;
        if (_elementos.Any(e => e.Clave == clave)) return false;

        _elementos.Add(NotificacionElemento.Crear(clave, titulo, url, ahoraUtc));
        Cantidad = _elementos.Count;
        Titulo   = Recortar(tituloGrupo(Cantidad), LargoTitulo);
        return true;
    }

    public void MarcarLeida()
    {
        if (Leida) return;
        Leida        = true;
        FechaLectura = DateTime.UtcNow;
    }

    /// <summary>Recorta al largo de la columna en vez de dejar que SQL Server rechace el guardado.</summary>
    public static string Recortar(string texto, int largo) =>
        texto.Length <= largo ? texto : texto[..(largo - 1)] + "…";
}
