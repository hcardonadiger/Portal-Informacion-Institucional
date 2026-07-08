using System.Globalization;
using Diger.TramitesEstado.Application.Common.Catalogs;

namespace Diger.TramitesEstado.Web.Pages.Expedientes;

/// <summary>Convierte entre la forma JSON del editor (<see cref="OriginalExpedienteDto"/>)
/// y el DTO de aplicación (<see cref="ExpedienteInputDto"/>), en ambas direcciones.</summary>
public static class OriginalShapeMapper
{
    // ── Forma editor → DTO de aplicación (al guardar) ─────────────────────
    public static ExpedienteInputDto ToInput(OriginalExpedienteDto o, string institucionId)
    {
        var n = Math.Max(1, o.NumTramites);

        var tramites = new List<TramiteInput>();
        for (var t = 0; t < n; t++)
        {
            var f = t < o.Tramites.Count ? o.Tramites[t] : new();
            string? G(string k) => f.TryGetValue(k, out var v) ? v : null;
            tramites.Add(new TramiteInput(
                t,
                At(o.TramiteNombres, t) ?? G("nombre_tramite") ?? "",
                G("nombre_corto"), At(o.TramiteAreas, t),
                G("modalidad"), G("plazo_legal"), G("tercero"), G("tiempo_real"),
                G("metodo_pago"), G("pago_banco"), G("pago_cuenta"), G("tgr_inst"), G("tgr_rubro"), G("tgr_monto"),
                G("doc_entregado"), G("objetivo"), G("alcance"), G("alcance_obs"), G("descripcion"),
                G("dirigido"), G("horario"), G("telefono"), G("email_tramite"), G("sitio_web")));
        }

        var requisitos = new List<RequisitoInput>();
        for (var t = 0; t < o.ReqsTram.Count && t < n; t++)
        {
            var reqs = o.ReqsTram[t] ?? [];
            var accs = t < o.AccionesTram.Count ? o.AccionesTram[t] : null;
            for (var k = 0; k < reqs.Count; k++)
            {
                var acc = accs is not null && k < accs.Count ? accs[k] : null;
                requisitos.Add(new RequisitoInput(t, k, reqs[k].Requisito ?? "", reqs[k].Obs,
                    ParseAccion(acc?.Accion), acc?.Justificacion));
            }
        }

        var flujos = new List<FlujoNodoInput>();
        AddFlujos(flujos, o.FlujosActual, FaseFlujo.Actual, n);
        AddFlujos(flujos, o.FlujosPropuesto, FaseFlujo.Propuesto, n);

        var legal = o.Legal.Select((l, i) => new LegalInput(i, l.Instrumento ?? "", l.Articulos, l.Obs)).ToList();
        var docs  = o.Docs.Select((d, i) => new DocSolicitadoInput(
            i, d.Nombre ?? "", d.Tipo, string.Equals(d.Recibido, "Recibido", StringComparison.OrdinalIgnoreCase),
            ParseDate(d.Fecha), d.Url)).ToList();
        var docsInt = o.DocsInternos.Select((d, i) => new DocInternoInput(i, d.Documento ?? "", d.Area, d.Obs)).ToList();

        var inf = o.Infra ?? new();
        var perfiles = inf.Perfiles.Select(p => new PerfilInput(p.Perfil ?? "", p.Nombre, p.Correo)).ToList();
        var condiciones = inf.DcCond.ToList();

        // checklist: el editor lo envía en el orden plano del catálogo de infraestructura
        var flat = FlatInfra();
        var checklist = new List<ChecklistInput>();
        for (var i = 0; i < inf.Checklist.Count; i++)
        {
            var c = inf.Checklist[i];
            var grupo = i < flat.Count ? flat[i].grupo : "";
            checklist.Add(new ChecklistInput(i, grupo, c.Req ?? "", ParseStatus(c.Status), c.Obs));
        }

        var secciones = new List<SeccionInput>();
        for (var s = 0; s <= 6; s++)
        {
            var key = s.ToString();
            var est = o.Estados.TryGetValue(key, out var ev) ? ev : null;
            var nota = o.Notas.TryGetValue(key, out var nv) ? nv : null;
            if (est is null && string.IsNullOrWhiteSpace(nota)) continue;
            secciones.Add(new SeccionInput(s, ParseSeccion(est), nota));
        }

        return new ExpedienteInputDto(
            institucionId, o.Inst?.Trim() ?? "", ParseDate(o.FechaApertura), o.Analista?.Trim() ?? "",
            o.DirSede, o.NumTramitesProd,
            o.ContactoNombre, o.ContactoCargo, o.ContactoCorreo, o.ContactoTel,
            o.ObsLegal, ParseInt(o.NumFunc), ParseInt(o.NumAnio), o.TiempoObs, o.TNorm,
            o.DescProceso, o.DocsAdd, o.ObsFlujo, ParseInt(o.FuncDig), o.TiempoDig, o.ObsModelo,
            inf.Personal, ParseInt(inf.PersonalTI), inf.RespSol, inf.Acomp, inf.DcModalidad,
            inf.DcVirt, inf.DcVirtOtro, inf.DcDisp, inf.DcObs, inf.Plan,
            ParseEstadoExp(o.EstadoExp), ParseEstadoLev(o.EstadoLev),
            o.ObsExpediente, o.ObsLevantamiento, o.ValidadoDiger, o.ValidadoInst,
            ParseDate(o.FechaValidacion), o.NumActa,
            tramites, requisitos, flujos, legal, docs, docsInt, perfiles, condiciones, checklist, secciones);
    }

    // ── DTO de aplicación → forma editor (al abrir para editar) ───────────
    public static OriginalExpedienteDto FromInput(ExpedienteInputDto d)
    {
        var o = new OriginalExpedienteDto
        {
            Inst = d.Institucion, FechaApertura = Fmt(d.FechaApertura), Analista = d.Analista, DirSede = d.DirSede,
            ContactoNombre = d.ContactoNombre, ContactoCargo = d.ContactoCargo,
            ContactoCorreo = d.ContactoCorreo, ContactoTel = d.ContactoTel,
            NumTramites = Math.Max(1, d.Tramites.Count), NumTramitesProd = d.NumTramitesProd,
            ObsLegal = d.ObsLegal, NumFunc = d.NumFuncionarios?.ToString(), NumAnio = d.VolumenAnual?.ToString(),
            TiempoObs = d.TiempoObservado, TNorm = d.TiempoNorma, DescProceso = d.DescProceso,
            DocsAdd = d.DocsAdicionales, ObsFlujo = d.ObsFlujo,
            FuncDig = d.FuncionariosDig?.ToString(), TiempoDig = d.TiempoDig, ObsModelo = d.ObsModelo,
            EstadoExp = FmtEstadoExp(d.EstadoExpediente), EstadoLev = FmtEstadoLev(d.EstadoLevantamiento),
            ObsExpediente = d.ObsExpediente, ObsLevantamiento = d.ObsLevantamiento,
            ValidadoDiger = d.ValidadoDiger, ValidadoInst = d.ValidadoInst,
            FechaValidacion = Fmt(d.FechaValidacion), NumActa = d.NumActa
        };

        foreach (var t in d.Tramites.OrderBy(x => x.TramiteIndex))
        {
            o.TramiteNombres.Add(t.NombreTramite);
            o.TramiteAreas.Add(t.AreaResponsable);
            o.Tramites.Add(new Dictionary<string, string?>
            {
                ["nombre_tramite"] = t.NombreTramite, ["nombre_corto"] = t.NombreCorto, ["modalidad"] = t.Modalidad,
                ["plazo_legal"] = t.PlazoLegal, ["tercero"] = t.Tercero, ["tiempo_real"] = t.TiempoReal,
                ["metodo_pago"] = t.MetodoPago, ["pago_banco"] = t.PagoBanco, ["pago_cuenta"] = t.PagoCuenta,
                ["tgr_inst"] = t.TgrInst, ["tgr_rubro"] = t.TgrRubro, ["tgr_monto"] = t.TgrMonto,
                ["doc_entregado"] = t.DocEntregado, ["objetivo"] = t.Objetivo, ["alcance"] = t.Alcance,
                ["alcance_obs"] = t.AlcanceObs, ["descripcion"] = t.Descripcion, ["dirigido"] = t.Dirigido,
                ["horario"] = t.Horario, ["telefono"] = t.Telefono, ["email_tramite"] = t.EmailTramite, ["sitio_web"] = t.SitioWeb
            });
        }

        var nt = o.NumTramites;
        o.ReqsTram = Enumerable.Range(0, nt).Select(_ => new List<OriginalExpedienteDto.ReqOrig>()).ToList();
        o.AccionesTram = Enumerable.Range(0, nt).Select(_ => new List<OriginalExpedienteDto.AccionOrig>()).ToList();
        foreach (var r in d.Requisitos.OrderBy(x => x.TramiteIndex).ThenBy(x => x.Orden))
        {
            if (r.TramiteIndex < 0 || r.TramiteIndex >= nt) continue;
            o.ReqsTram[r.TramiteIndex].Add(new() { Requisito = r.Requisito, Obs = r.Obs });
            o.AccionesTram[r.TramiteIndex].Add(new() { Accion = FmtAccion(r.Accion), Justificacion = r.Justificacion });
        }

        o.FlujosActual = Enumerable.Range(0, nt).Select(_ => new List<OriginalExpedienteDto.NodoOrig>()).ToList();
        o.FlujosPropuesto = Enumerable.Range(0, nt).Select(_ => new List<OriginalExpedienteDto.NodoOrig>()).ToList();
        foreach (var fnodo in d.Flujos.OrderBy(x => x.TramiteIndex).ThenBy(x => x.Orden))
        {
            if (fnodo.TramiteIndex < 0 || fnodo.TramiteIndex >= nt) continue;
            var target = fnodo.Fase == FaseFlujo.Actual ? o.FlujosActual : o.FlujosPropuesto;
            target[fnodo.TramiteIndex].Add(new()
            {
                Tipo = fnodo.Tipo.ToString().ToLowerInvariant(), Titulo = fnodo.Titulo, Area = fnodo.Area,
                Tiempo = fnodo.Tiempo, DocEmitido = fnodo.DocEmitido, Obs = fnodo.Obs, RetornoA = ParseInt(fnodo.RetornoA)
            });
        }

        o.Legal = d.Legal.OrderBy(x => x.Orden).Select(l => new OriginalExpedienteDto.LegalOrig { Instrumento = l.Instrumento, Articulos = l.Articulos, Obs = l.Obs }).ToList();
        o.Docs = d.DocsSolicitados.OrderBy(x => x.Orden).Select(x => new OriginalExpedienteDto.DocOrig { Nombre = x.Nombre, Tipo = x.Tipo, Recibido = x.Recibido ? "Recibido" : "Pendiente", Fecha = Fmt(x.Fecha), Url = x.Url }).ToList();
        o.DocsInternos = d.DocsInternos.OrderBy(x => x.Orden).Select(x => new OriginalExpedienteDto.DocIntOrig { Documento = x.Documento, Area = x.Area, Obs = x.Obs }).ToList();

        o.Infra = new OriginalExpedienteDto.InfraOrig
        {
            Personal = d.InfraPersonal, PersonalTI = d.InfraPersonalTI?.ToString(), RespSol = d.InfraRespSol,
            Acomp = d.InfraAcomp, DcModalidad = d.InfraDcModalidad, DcVirt = d.InfraDcVirt, DcVirtOtro = d.InfraDcVirtOtro,
            DcDisp = d.InfraDcDisp, DcObs = d.InfraDcObs, Plan = d.InfraPlan,
            DcCond = d.Condiciones.ToList(),
            Perfiles = d.Perfiles.Select(p => new OriginalExpedienteDto.PerfilOrig { Perfil = p.Perfil, Nombre = p.Nombre, Correo = p.Correo }).ToList(),
            Checklist = d.ChecklistInfra.OrderBy(x => x.Orden).Select(c => new OriginalExpedienteDto.ChkOrig { Req = c.Requisito, Status = FmtStatus(c.Status), Obs = c.Obs }).ToList()
        };

        foreach (var s in d.Secciones)
        {
            o.Estados[s.Seccion.ToString()] = FmtSeccion(s.Estado);
            o.Notas[s.Seccion.ToString()] = s.Nota;
        }
        return o;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void AddFlujos(List<FlujoNodoInput> dst, List<List<OriginalExpedienteDto.NodoOrig>> src, FaseFlujo fase, int n)
    {
        for (var t = 0; t < src.Count && t < n; t++)
        {
            var nodos = src[t] ?? [];
            for (var k = 0; k < nodos.Count; k++)
            {
                var nd = nodos[k];
                dst.Add(new FlujoNodoInput(t, fase, k, ParseTipo(nd.Tipo), nd.Titulo, nd.Area, nd.Tiempo,
                    nd.DocEmitido, nd.Obs, nd.RetornoA?.ToString()));
            }
        }
    }

    private static List<(string grupo, string item)> FlatInfra() =>
        InfraCatalog.Reqs.SelectMany(g => g.Items.Select(i => (g.Grupo, i))).ToList();

    private static string? At(List<string?> list, int i) => i >= 0 && i < list.Count ? list[i] : null;
    private static int? ParseInt(string? s) => int.TryParse(s, out var v) ? v : null;
    private static DateOnly? ParseDate(string? s) => DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    private static string? Fmt(DateOnly? d) => d?.ToString("yyyy-MM-dd");

    private static AccionRequisito? ParseAccion(string? s) => s switch
    {
        "Eliminar" => AccionRequisito.Eliminar, "Simplificar" => AccionRequisito.Simplificar,
        "Digitalizar" => AccionRequisito.Digitalizar, "Mantener" => AccionRequisito.Mantener, _ => null
    };
    private static string? FmtAccion(AccionRequisito? a) => a?.ToString();

    private static TipoNodoFlujo ParseTipo(string? s) => s switch
    {
        "inicio" => TipoNodoFlujo.Inicio, "decision" => TipoNodoFlujo.Decision,
        "fin" => TipoNodoFlujo.Fin, _ => TipoNodoFlujo.Paso
    };

    private static InfraStatus ParseStatus(string? s) => s switch
    {
        "Cumple" => InfraStatus.Cumple, "No cumple" => InfraStatus.NoCumple,
        "Parcial" => InfraStatus.Parcial, "No aplica" => InfraStatus.NoAplica, _ => InfraStatus.Pendiente
    };
    private static string FmtStatus(InfraStatus s) => s switch
    {
        InfraStatus.Cumple => "Cumple", InfraStatus.NoCumple => "No cumple",
        InfraStatus.Parcial => "Parcial", InfraStatus.NoAplica => "No aplica", _ => "Pendiente"
    };

    private static EstadoSeccion ParseSeccion(string? s) => s switch
    {
        "En progreso" => EstadoSeccion.EnProgreso, "Completo" => EstadoSeccion.Completo,
        "Validado" => EstadoSeccion.Validado, _ => EstadoSeccion.Pendiente
    };
    private static string FmtSeccion(EstadoSeccion s) => s switch
    {
        EstadoSeccion.EnProgreso => "En progreso", EstadoSeccion.Completo => "Completo",
        EstadoSeccion.Validado => "Validado", _ => "Pendiente"
    };

    private static EstadoExpediente ParseEstadoExp(string? s) => s switch
    {
        "En levantamiento" => EstadoExpediente.EnLevantamiento, "En modelado" => EstadoExpediente.EnModelado,
        "En validación" => EstadoExpediente.EnValidacion, "Cerrado" => EstadoExpediente.Cerrado,
        _ => EstadoExpediente.EnExploracion
    };
    private static string FmtEstadoExp(EstadoExpediente s) => s switch
    {
        EstadoExpediente.EnLevantamiento => "En levantamiento", EstadoExpediente.EnModelado => "En modelado",
        EstadoExpediente.EnValidacion => "En validación", EstadoExpediente.Cerrado => "Cerrado",
        _ => "En exploración"
    };

    private static EstadoLevantamientoExp? ParseEstadoLev(string? s) => s switch
    {
        "En proceso" => EstadoLevantamientoExp.EnProceso, "Completo" => EstadoLevantamientoExp.Completo,
        "Pendiente de validar" => EstadoLevantamientoExp.PendienteDeValidar,
        "Requiere revisita" => EstadoLevantamientoExp.RequiereRevisita, _ => null
    };
    private static string? FmtEstadoLev(EstadoLevantamientoExp? s) => s switch
    {
        EstadoLevantamientoExp.EnProceso => "En proceso", EstadoLevantamientoExp.Completo => "Completo",
        EstadoLevantamientoExp.PendienteDeValidar => "Pendiente de validar",
        EstadoLevantamientoExp.RequiereRevisita => "Requiere revisita", _ => null
    };
}
