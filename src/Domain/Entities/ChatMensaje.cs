namespace Diger.TramitesEstado.Domain.Entities;

public sealed class ChatMensaje : BaseEntity
{
    public int      SesionId    { get; private set; }
    public string   Texto       { get; private set; } = "";
    public bool     EsDelTecnico { get; private set; }
    public bool     EsSistema   { get; private set; }
    public string   AutorNombre { get; private set; } = "";
    public DateTime Enviado     { get; private set; }
    public bool     Leido       { get; private set; }

    private ChatMensaje() { }

    internal static ChatMensaje Crear(int sesionId, string texto, bool esDelTecnico, bool esSistema, string autorNombre)
        => new()
        {
            SesionId    = sesionId,
            Texto       = texto,
            EsDelTecnico = esDelTecnico,
            EsSistema   = esSistema,
            AutorNombre = autorNombre,
            Enviado     = DateTime.UtcNow,
            Leido       = false,
        };

    public void MarcarLeido() => Leido = true;
}
