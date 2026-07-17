using Microsoft.AspNetCore.Mvc.Rendering;

namespace Diger.TramitesEstado.Web.Pages.Cuenta.Manage;

public static class ManageNavPages
{
    public static string Index => "Index";
    public static string Certificate => "Certificate";

    public static string IndexNavClass(ViewContext viewContext) => PageNavClass(viewContext, Index);
    public static string CertificateNavClass(ViewContext viewContext) => PageNavClass(viewContext, Certificate);

    private static string PageNavClass(ViewContext viewContext, string page)
    {
        var activePage = viewContext.ViewData["ActivePage"] as string
            ?? System.IO.Path.GetFileNameWithoutExtension(viewContext.ActionDescriptor.DisplayName);
        return string.Equals(activePage, page, StringComparison.OrdinalIgnoreCase) ? "active" : "";
    }
}
