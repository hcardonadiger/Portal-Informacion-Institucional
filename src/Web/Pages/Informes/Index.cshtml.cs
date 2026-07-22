using Diger.TramitesEstado.Application.Informes;
using Diger.TramitesEstado.Application.Informes.Common;
using Diger.TramitesEstado.Application.Informes.Queries;

namespace Diger.TramitesEstado.Web.Pages.Informes;

[Authorize]
public sealed class IndexModel(ISender sender, IInformeService informeSvc) : PageModel
{
    [BindProperty(SupportsGet = true)] public string?   InstitucionId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Desde         { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Hasta         { get; set; }

    public IReadOnlyList<(string Id, string Nombre)> Instituciones { get; private set; } = [];
    public InformeInstitucionDto? Informe { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await CargarInstituciones(ct);

        if (InstitucionId != null || Desde != null || Hasta != null)
            Informe = await sender.Send(new GetInformeInstitucionQuery(InstitucionId, Desde, Hasta), ct);

        return Page();
    }

    public async Task<IActionResult> OnPostPdfAsync(CancellationToken ct)
    {
        var dto = await sender.Send(new GetInformeInstitucionQuery(InstitucionId, Desde, Hasta), ct);
        var bytes = informeSvc.GenerarPdf(dto);
        var nombre = $"informe_{dto.InstitucionNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    public async Task<IActionResult> OnPostExcelAsync(CancellationToken ct)
    {
        var dto = await sender.Send(new GetInformeInstitucionQuery(InstitucionId, Desde, Hasta), ct);
        var bytes = informeSvc.GenerarExcel(dto);
        var nombre = $"informe_{dto.InstitucionNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    private async Task CargarInstituciones(CancellationToken ct)
    {
        var paged = await sender.Send(new GetInstitucionesQuery(Size: 500), ct);
        Instituciones = paged.Items.Select(i => (i.Id, i.Nombre)).ToList();
    }
}
