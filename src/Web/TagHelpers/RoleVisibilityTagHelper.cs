using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Linq;

namespace Diger.TramitesEstado.Web.TagHelpers;

[HtmlTargetElement(Attributes = "restrict-role")]
public class RoleVisibilityTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    [HtmlAttributeName("restrict-role")]
    public string RestrictRole { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var user = ViewContext.HttpContext.User;
        var activeRol = user.FindFirst("diger:rol")?.Value;

        if (!string.IsNullOrEmpty(activeRol) && !string.IsNullOrEmpty(RestrictRole))
        {
            var restrictedRoles = RestrictRole.Split(',').Select(r => r.Trim()).ToList();
            if (restrictedRoles.Contains(activeRol, StringComparer.OrdinalIgnoreCase))
            {
                output.SuppressOutput();
            }
            else
            {
                output.Attributes.RemoveAll("restrict-role");
            }
        }
        else
        {
            output.Attributes.RemoveAll("restrict-role");
        }
    }
}
