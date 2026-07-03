namespace Diger.TramitesEstado.Web.Pages.Asistencia;

public sealed class RegistroModel(ISender sender) : PageModel
{
    public ReunionPublicaDto? Reunion { get; private set; }
    public bool Registrado { get; private set; }
    public string? Error { get; set; }

    [BindProperty] public AsistenteAutoInput Datos { get; set; } = new();

    // Códigos de país (Centroamérica, México, EE. UU. y España).
    public static readonly (string Code, string Label)[] Paises =
    [
        ("+504", "🇭🇳 Honduras +504"), ("+502", "🇬🇹 Guatemala +502"), ("+503", "🇸🇻 El Salvador +503"),
        ("+505", "🇳🇮 Nicaragua +505"), ("+506", "🇨🇷 Costa Rica +506"), ("+507", "🇵🇦 Panamá +507"),
        ("+52", "🇲🇽 México +52"), ("+1", "🇺🇸 EE. UU. +1"), ("+34", "🇪🇸 España +34"),
    ];

    public async Task<IActionResult> OnGetAsync(Guid token, CancellationToken ct)
    {
        try { Reunion = await sender.Send(new GetReunionPublicaQuery(token), ct); }
        catch (NotFoundException) { return NotFound(); }
        Datos.CodigoPais ??= "+504";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid token, CancellationToken ct)
    {
        try { Reunion = await sender.Send(new GetReunionPublicaQuery(token), ct); }
        catch (NotFoundException) { return NotFound(); }

        if (string.IsNullOrWhiteSpace(Datos.Nombre))
        {
            Error = "El nombre completo es obligatorio.";
            return Page();
        }

        try
        {
            await sender.Send(new RegistrarAsistenciaCommand(token, Datos), ct);
            Registrado = true;
            return Page();
        }
        catch (DomainException ex)                     { Error = ex.Message; return Page(); }
        catch (FluentValidation.ValidationException ex) { Error = ex.Message; return Page(); }
    }
}
