namespace Diger.TramitesEstado.Application.Common.Catalogs;

/// <summary>
/// Clasificador TGR de SEFIN (tgr1.sefin.gob.hn), portado del formulario original.
/// Se usa en la ficha del trámite para el pago vía TGR (institución → rubros).
/// </summary>
public static class TgrCatalog
{
    public static readonly string[] Instituciones =
    [
        "1 - Congreso Nacional", "2 - Tribunal Superior de Cuentas",
        "3 - Comisionado Nacional de los Derechos Humanos (CONADEH)", "10 - Poder Judicial",
        "20 - Presidencia de la República", "22 - Fondo Hondureño de Inversión Social (FHIS)",
        "24 - Instituto de la Propiedad (IP)", "26 - Instituto Nacional de la Juventud",
        "28 - Instituto Nacional de Conservación y Desarrollo Forestal (ICF)", "30 - Secretaría de la Presidencia",
        "32 - Instituto de Acceso a la Información Pública (IAIP)", "33 - Comisión Nacional de Vivienda y Asentamientos Humanos (CONVIVIENDA)",
        "35 - Instituto Hondureño de Geología y Minas (INHGEOMIN)", "37 - Servicio de Administración de Rentas (SAR)",
        "38 - Dirección Adjunta de Rentas Aduaneras (DARA)", "40 - Secretaría de Gobernación, Justicia y Descentralización (SGJD)",
        "41 - Comisión Permanente de Contingencias (COPECO)", "42 - Cuerpo de Bomberos de Honduras",
        "43 - Empresa Nacional de Artes Gráficas (ENAG)", "44 - Instituto Nacional Penitenciario (INP)",
        "45 - Instituto Nacional de Migración (INM)", "50 - Secretaría de Educación (SEDUC)",
        "60 - Secretaría de Salud (SESAL)", "61 - Ente Regulador de Servicios de Agua Potable y Saneamiento (ERSAPS)",
        "62 - Agencia de Regulación Sanitaria (ARSA)", "70 - Secretaría de Seguridad (SESEGU)",
        "80 - Secretaría de Relaciones Exteriores y Cooperación Internacional (SRECI)", "90 - Secretaría de Defensa Nacional (SEDENA)",
        "91 - Agencia Hondureña de Aeronáutica Civil (AHAC)", "100 - Secretaría de Finanzas (SEFIN)",
        "101 - Comisión Nacional de Telecomunicaciones (CONATEL)", "102 - Comisión Administradora Zona Libre Turística (ZOLITUR)",
        "103 - Dirección Nacional de Bienes del Estado (DNBE)", "120 - Secretaría de Infraestructura y Servicios Públicos (INSEP)",
        "121 - Dirección General de la Marina Mercante (DGMM)", "122 - Fondo Vial",
        "123 - Instituto Hondureño de Transporte Terrestre (IHTT)", "130 - Secretaría de Trabajo y Seguridad Social (STSS)",
        "140 - Secretaría de Agricultura y Ganadería (SAG)", "141 - Dirección de Ciencia y Tecnología Agropecuaria (DICTA)",
        "145 - Servicio Nacional de Sanidad e Inocuidad Agroalimentaria (SENASA)", "150 - Secretaría de Energía, Recursos Naturales, Ambiente y Minas (MiAmbiente)",
        "153 - Comisión Reguladora de Energía Eléctrica (CREE)"
    ];

    public static readonly string[] Rubros =
    [
        "11401 - A casinos de juego envite o azar", "11402 - A la venta de timbres de contratación",
        "11404 - A servicios de vías públicas", "11405 - Sobre traspaso de vehículos",
        "11406 - Sobre servicios turísticos", "12102 - Control migratorio",
        "12103 - Inspección de vehículos", "12107 - Marchamos", "12108 - Servicios consulares",
        "12109 - Papeles de aduana", "12114 - Papel notarial", "12118 - Registros ley de propiedad",
        "12120 - Servicios de auténticas y traducciones", "12121 - Emisión de constancias, certificaciones y otros",
        "12126 - Actos administrativos", "12199 - Tasas varias", "12201 - Libreta pasaporte",
        "12203 - Registro marcas de fábricas", "12204 - Registro patente de invención",
        "12205 - Registro de prestamistas", "12206 - Incorporación de empresas mercantiles",
        "12208 - Licencia de conducir", "12209 - Otras licencias", "12211 - Permisos y renovaciones migratorias",
        "12213 - Registro nacional de armas", "12217 - Emisión y reposición de placas y calcomanías",
        "12218 - Registro nacional de las personas", "12219 - Registro derechos de autor y conexos",
        "12299 - Derechos varios", "12301 - Concesiones y frecuencias radioeléctricas", "12399 - Regalías varias"
    ];

    private static readonly Dictionary<string, string> RubroExtra = new()
    {
        ["12806"] = "Devoluciones de ejercicios fiscales anteriores por pagos en exceso"
    };

    private static readonly Dictionary<string, string> RubroNames = BuildRubroNames();

    private static Dictionary<string, string> BuildRubroNames()
    {
        var m = new Dictionary<string, string>();
        foreach (var s in Rubros)
        {
            var p = s.IndexOf(" - ", StringComparison.Ordinal);
            if (p > 0) m[s[..p].Trim()] = s[(p + 3)..];
        }
        foreach (var kv in RubroExtra) m[kv.Key] = kv.Value;
        return m;
    }

    // Mapeo institución (código) → rubros permitidos (fuente: portal TGR-1).
    private static readonly Dictionary<string, string[]> InstRubros = new()
    {
        ["1"] = ["11402", "12121", "12806"],
        ["2"] = ["11402", "12121", "12499", "12804", "12806"],
        ["3"] = ["11402", "12121", "12806"],
        ["10"] = ["11402", "12121", "12806"],
        ["20"] = ["11402", "12121", "12806"],
        ["21"] = ["11402", "12121", "12806"],
        ["22"] = ["11402", "12121", "12806"],
        ["23"] = ["11402", "12121", "12806"],
        ["24"] = ["11402", "12118", "12121", "12203", "12204", "12219", "12806", "15101"],
        ["25"] = ["11402", "12121", "12806"],
        ["26"] = ["11402", "12121", "12806"],
        ["27"] = ["11402", "12121", "12806"],
        ["28"] = ["11402", "12121", "12199", "12499", "12806", "15101", "15104"],
        ["29"] = ["11402", "12121", "12806", "12899"],
        ["30"] = ["11402", "12121", "12806"],
        ["31"] = ["11402", "12121", "12806", "15299"],
        ["32"] = ["11402", "12121", "12806"],
        ["33"] = ["11402", "12121", "12806"],
        ["34"] = ["11402", "12121", "12806"],
        ["35"] = ["11402", "12116", "12117", "12121", "12302", "12306", "12417", "12806", "12899", "15101", "17603"],
        ["36"] = ["11402", "12121", "12806", "15207", "17603"],
        ["37"] = ["11402", "12121", "12806", "12899"],
        ["38"] = ["11402", "12806", "12899"],
        ["39"] = ["11402", "12121"],
        ["40"] = ["11402", "12120", "12121", "12211", "12806", "12899"],
        ["41"] = ["11402", "12121", "12806"],
        ["42"] = ["11402", "12121", "12806", "15299"],
        ["43"] = ["11402", "12121", "12806", "15204"],
        ["44"] = ["11402", "12121", "12806", "15101"],
        ["45"] = ["11402", "12102", "12121", "12201", "12211", "12416", "12806", "12899"],
        ["46"] = ["11402", "12121", "12806", "15101"],
        ["50"] = ["11402", "12121", "12806", "15204", "15207", "17605"],
        ["51"] = ["11402", "12121", "12806", "12899", "15299"],
        ["60"] = ["11402", "12121", "12405", "12806", "15206"],
        ["61"] = ["11402", "12121", "12806", "15299"],
        ["62"] = ["12121", "12199", "12203", "12209", "12499", "12806", "15299"],
        ["70"] = ["11402", "12120", "12121", "12208", "12213", "12413", "12806", "12899", "15205"],
        ["71"] = ["11402", "12121", "12804", "12806"],
        ["72"] = ["11402", "12121", "12806"],
        ["80"] = ["11402", "12108", "12120", "12121", "12201", "12218", "12806"],
        ["90"] = ["11402", "12121", "12499", "12806", "15104", "15203", "15207", "15299", "17601", "17603"],
        ["91"] = ["11402", "11409", "12115", "12121", "12806"],
        ["100"] = ["11402", "12121", "12499", "12806", "12899"],
        ["101"] = ["11402", "12121", "12124", "12301", "12412", "12806"],
        ["102"] = ["11110", "11402", "12121", "12123", "12209", "12806", "15101"],
        ["103"] = ["11402"],
        ["104"] = ["11402", "12121", "12801", "12806", "12899"],
        ["110"] = ["11402"],
        ["111"] = ["11402"],
        ["120"] = ["11402", "11409", "12121", "12299", "12409", "12806", "12899"],
        ["121"] = ["11402", "12105", "12121", "12202", "12207", "12806"],
        ["122"] = ["11402"],
        ["123"] = ["11402", "11409", "12121", "12409", "12806"],
        ["130"] = ["11402", "12121", "12410", "12804", "12806", "15204"],
        ["140"] = ["11402", "12121", "12199", "12209", "12309", "12499", "12801", "12804", "12806", "12899", "15102", "15299", "17601", "17603", "17604", "18105", "18302"],
        ["141"] = ["11402", "12121", "12806", "15102", "15299", "17603"],
        ["142"] = ["11402"],
        ["143"] = ["11402"],
        ["144"] = ["11402", "12121", "12806", "15101"],
        ["145"] = ["11402", "12121", "12199", "12499", "12806", "15104"],
        ["150"] = ["11402", "12117", "12120", "12121", "12199", "12209", "12299", "12304", "12406", "12499", "12806", "12807", "12899", "15104", "15217", "17603"],
        ["151"] = ["11402"],
        ["152"] = ["11402"],
        ["153"] = ["11402", "12121", "12806", "15299"],
        ["160"] = ["11402", "12121", "12806"],
        ["170"] = ["11402"],
        ["180"] = ["11402", "12218", "12806"],
        ["190"] = ["11402", "12121", "12806"],
        ["200"] = ["11402", "12121", "12404", "12410", "12499", "12804", "12806"],
        ["290"] = ["11402", "11406", "12121", "12199", "12499", "12806", "15199", "15299"],
        ["404"] = ["12121", "12806", "15101"],
        ["409"] = ["11402", "12121", "12806", "15299"]
    };

    public static string TgrCodeOf(string? instValue)
    {
        if (string.IsNullOrWhiteSpace(instValue)) return "";
        var m = System.Text.RegularExpressions.Regex.Match(instValue, @"^\s*(\d+)");
        return m.Success ? m.Groups[1].Value : "";
    }

    public static string RubroLabel(string code) =>
        $"{code} - {(RubroNames.TryGetValue(code, out var n) ? n : $"Rubro {code}")}";

    /// <summary>Devuelve los rubros (etiquetados) para una institución TGR; si no hay
    /// mapeo específico, retorna el catálogo completo.</summary>
    public static IReadOnlyList<string> RubrosFor(string? instValue)
    {
        var code = TgrCodeOf(instValue);
        if (code.Length > 0 && InstRubros.TryGetValue(code, out var list) && list.Length > 0)
            return list.Select(RubroLabel).ToList();
        return Rubros;
    }
}
