namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Une un proyecto con una reunión.
///
/// <para><b>Opcional por los dos lados y de muchos a muchos.</b> Una reunión puede no pertenecer a
/// ningún proyecto —la mayoría no pertenece— y puede tocar a varios: una mesa técnica sobre
/// interconectividad avanza el nodo central y el convenio marco a la vez. Un proyecto, por su
/// parte, vive perfectamente sin reuniones registradas.</para>
///
/// <para><b>Tabla de vínculo y no una columna en <see cref="Reunion"/>.</b> Una columna habría
/// impuesto «una reunión, un proyecto» desde el primer día, que es justo la decisión que no hay
/// por qué tomar ahora. El precedente de <see cref="Reunion.ExpedienteId"/> —columna simple, sin
/// interfaz y con cero filas en año y medio— es la advertencia de lo que pasa cuando se elige la
/// forma cómoda y no la que el dato pide.</para>
///
/// <para><b>El alcance se hereda del proyecto</b>, no de la reunión: quien puede abrir el proyecto
/// ve qué reuniones tiene vinculadas. Es deliberado y tiene una consecuencia que hay que conocer:
/// <see cref="Proyecto.InstitucionId"/> es siempre la institución que EJECUTA —DIGER— mientras que
/// la de la reunión es la beneficiaria, así que el vínculo cruza instituciones por construcción.
/// Ver la nota de la consulta que lo lista.</para>
/// </summary>
public sealed class ProyectoReunion : BaseEntity
{
    public int ProyectoId { get; private set; }
    public int ReunionId  { get; private set; }

    /// <summary>Por qué se vinculó. Opcional, y no decorativo: seis meses después «se acordó el
    /// alcance del enlace de fibra» explica el vínculo que el título de la reunión no explica.</summary>
    public string? Nota { get; private set; }

    /// <summary>Quién lo vinculó, como copia del nombre, y cuándo. Mismo criterio que el resto del
    /// portal: el registro tiene que seguir siendo legible cuando la persona ya no esté.</summary>
    public string   VinculadoPor { get; private set; } = default!;
    public DateTime VinculadoEn  { get; private set; }

    private ProyectoReunion() { }   // EF

    public static ProyectoReunion Crear(int proyectoId, int reunionId, string? actor, string? nota = null)
    {
        if (proyectoId <= 0) throw new DomainException("El vínculo necesita un proyecto.");
        if (reunionId  <= 0) throw new DomainException("El vínculo necesita una reunión.");

        return new ProyectoReunion
        {
            ProyectoId   = proyectoId,
            ReunionId    = reunionId,
            Nota         = Limpiar(nota),
            VinculadoPor = string.IsNullOrWhiteSpace(actor) ? "—" : actor.Trim(),
            VinculadoEn  = DateTime.UtcNow
        };
    }

    /// <returns><c>true</c> si algo cambió.</returns>
    public bool CambiarNota(string? nota)
    {
        var limpia = Limpiar(nota);
        if (Nota == limpia) return false;
        Nota = limpia;
        return true;
    }

    internal static string? Limpiar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Trim();
        if (limpio.Length > MaxNota)
            throw new DomainException($"La nota del vínculo no puede superar los {MaxNota} caracteres.");
        return limpio;
    }

    public const int MaxNota = 400;
}

/// <summary>
/// Une un proyecto con un expediente de digitalización.
///
/// <para>Mismas reglas que <see cref="ProyectoReunion"/>, y por los mismos motivos. Acá el muchos a
/// muchos es todavía más claro: un expediente alimenta el proyecto de su institución y también el
/// transversal que agrupa a varias, y no hay razón para obligar a elegir.</para>
/// </summary>
public sealed class ProyectoExpediente : BaseEntity
{
    public int ProyectoId   { get; private set; }
    public int ExpedienteId { get; private set; }

    public string?  Nota         { get; private set; }
    public string   VinculadoPor { get; private set; } = default!;
    public DateTime VinculadoEn  { get; private set; }

    private ProyectoExpediente() { }   // EF

    public static ProyectoExpediente Crear(int proyectoId, int expedienteId, string? actor, string? nota = null)
    {
        if (proyectoId   <= 0) throw new DomainException("El vínculo necesita un proyecto.");
        if (expedienteId <= 0) throw new DomainException("El vínculo necesita un expediente.");

        return new ProyectoExpediente
        {
            ProyectoId   = proyectoId,
            ExpedienteId = expedienteId,
            Nota         = ProyectoReunion.Limpiar(nota),
            VinculadoPor = string.IsNullOrWhiteSpace(actor) ? "—" : actor.Trim(),
            VinculadoEn  = DateTime.UtcNow
        };
    }

    /// <returns><c>true</c> si algo cambió.</returns>
    public bool CambiarNota(string? nota)
    {
        var limpia = ProyectoReunion.Limpiar(nota);
        if (Nota == limpia) return false;
        Nota = limpia;
        return true;
    }
}
