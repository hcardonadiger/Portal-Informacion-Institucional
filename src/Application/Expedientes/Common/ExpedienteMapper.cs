namespace Diger.TramitesEstado.Application.Expedientes.Common;

/// <summary>Aplica un <see cref="ExpedienteInputDto"/> sobre un <see cref="Expediente"/>
/// (campos escalares + reconstrucción en bloque de las colecciones hijas).</summary>
public static class ExpedienteMapper
{
    public static void Aplicar(Expediente e, ExpedienteInputDto d)
    {
        // Apertura
        e.FechaApertura   = d.FechaApertura;
        e.Analista        = d.Analista.Trim();
        e.DirSede         = d.DirSede?.Trim();
        e.NumTramitesProd = d.NumTramitesProd;

        // Contacto
        e.ContactoNombre = d.ContactoNombre?.Trim();
        e.ContactoCargo  = d.ContactoCargo?.Trim();
        e.ContactoCorreo = d.ContactoCorreo?.Trim();
        e.ContactoTel    = d.ContactoTel?.Trim();

        // Legal / proceso / modelo
        e.ObsLegal        = d.ObsLegal;
        e.NumFuncionarios = d.NumFuncionarios;
        e.VolumenAnual    = d.VolumenAnual;
        e.TiempoObservado = d.TiempoObservado;
        e.TiempoNorma     = d.TiempoNorma;
        e.DescProceso     = d.DescProceso;
        e.DocsAdicionales = d.DocsAdicionales;
        e.ObsFlujo        = d.ObsFlujo;
        e.FuncionariosDig = d.FuncionariosDig;
        e.TiempoDig       = d.TiempoDig;
        e.ObsModelo       = d.ObsModelo;

        // Infra escalares
        e.InfraPersonal    = d.InfraPersonal;
        e.InfraPersonalTI  = d.InfraPersonalTI;
        e.InfraRespSol     = d.InfraRespSol;
        e.InfraAcomp       = d.InfraAcomp;
        e.InfraDcModalidad = d.InfraDcModalidad;
        e.InfraDcVirt      = d.InfraDcVirt;
        e.InfraDcVirtOtro  = d.InfraDcVirtOtro;
        e.InfraDcDisp      = d.InfraDcDisp;
        e.InfraDcObs       = d.InfraDcObs;
        e.InfraPlan        = d.InfraPlan;

        // Estado / cierre
        e.EstadoExpediente    = d.EstadoExpediente;
        e.EstadoLevantamiento = d.EstadoLevantamiento;
        e.ObsExpediente       = d.ObsExpediente;
        e.ObsLevantamiento    = d.ObsLevantamiento;
        e.ValidadoDiger       = d.ValidadoDiger?.Trim();
        e.ValidadoInst        = d.ValidadoInst?.Trim();
        e.FechaValidacion     = d.FechaValidacion;
        e.NumActa             = d.NumActa?.Trim();

        // ── Colecciones (reemplazo en bloque) ─────────────────────
        e.LimpiarHijos();

        foreach (var t in d.Tramites.Where(x => !string.IsNullOrWhiteSpace(x.NombreTramite)))
            e.Agregar(new ExpedienteTramite
            {
                TramiteIndex = t.TramiteIndex, NombreTramite = t.NombreTramite.Trim(),
                NombreCorto = t.NombreCorto, AreaResponsable = t.AreaResponsable,
                Modalidad = t.Modalidad, PlazoLegal = t.PlazoLegal, Tercero = t.Tercero,
                TiempoReal = t.TiempoReal, MetodoPago = t.MetodoPago, PagoBanco = t.PagoBanco,
                PagoCuenta = t.PagoCuenta, TgrInst = t.TgrInst, TgrRubro = t.TgrRubro, TgrMonto = t.TgrMonto,
                DocEntregado = t.DocEntregado, Objetivo = t.Objetivo, Alcance = t.Alcance,
                AlcanceObs = t.AlcanceObs, Descripcion = t.Descripcion, Dirigido = t.Dirigido,
                Horario = t.Horario, Telefono = t.Telefono, EmailTramite = t.EmailTramite, SitioWeb = t.SitioWeb
            });

        foreach (var r in d.Requisitos.Where(x => !string.IsNullOrWhiteSpace(x.Requisito)))
            e.Agregar(new TramiteRequisito
            {
                TramiteIndex = r.TramiteIndex, Orden = r.Orden, Requisito = r.Requisito.Trim(),
                Obs = r.Obs, Accion = r.Accion, Justificacion = r.Justificacion
            });

        foreach (var n in d.Flujos)
            e.Agregar(new FlujoNodo
            {
                TramiteIndex = n.TramiteIndex, Fase = n.Fase, Orden = n.Orden, Tipo = n.Tipo,
                Titulo = n.Titulo, Area = n.Area, Tiempo = n.Tiempo, DocEmitido = n.DocEmitido,
                Obs = n.Obs, RetornoA = n.RetornoA
            });

        foreach (var l in d.Legal.Where(x => !string.IsNullOrWhiteSpace(x.Instrumento)))
            e.Agregar(new FundamentoLegal
            {
                Orden = l.Orden, Instrumento = l.Instrumento.Trim(), Articulos = l.Articulos, Obs = l.Obs
            });

        foreach (var x in d.DocsSolicitados.Where(z => !string.IsNullOrWhiteSpace(z.Nombre)))
            e.Agregar(new DocumentoSolicitado
            {
                Orden = x.Orden, Nombre = x.Nombre.Trim(), Tipo = x.Tipo,
                Recibido = x.Recibido, Fecha = x.Fecha, Url = x.Url
            });

        foreach (var x in d.DocsInternos.Where(z => !string.IsNullOrWhiteSpace(z.Documento)))
            e.Agregar(new DocumentoInterno
            {
                Orden = x.Orden, Documento = x.Documento.Trim(), Area = x.Area, Obs = x.Obs
            });

        foreach (var p in d.Perfiles.Where(z => !string.IsNullOrWhiteSpace(z.Perfil)))
            e.Agregar(new InfraPerfil { Perfil = p.Perfil.Trim(), Nombre = p.Nombre, Correo = p.Correo });

        foreach (var c in d.Condiciones.Where(z => !string.IsNullOrWhiteSpace(z)))
            e.Agregar(new InfraCondicion { Condicion = c.Trim() });

        foreach (var c in d.ChecklistInfra.Where(z => !string.IsNullOrWhiteSpace(z.Requisito)))
            e.Agregar(new InfraChecklistItem
            {
                Orden = c.Orden, Grupo = c.Grupo, Requisito = c.Requisito, Status = c.Status, Obs = c.Obs
            });

        foreach (var s in d.Secciones)
            e.Agregar(new ExpedienteSeccionEstado { Seccion = s.Seccion, Estado = s.Estado, Nota = s.Nota });
    }

    /// <summary>Proyecta la entidad de vuelta al DTO de entrada (para repoblar el editor).</summary>
    public static ExpedienteInputDto ToInputDto(Expediente e) => new(
        e.InstitucionId, e.Institucion, e.FechaApertura, e.Analista, e.DirSede, e.NumTramitesProd,
        e.ContactoNombre, e.ContactoCargo, e.ContactoCorreo, e.ContactoTel,
        e.ObsLegal, e.NumFuncionarios, e.VolumenAnual, e.TiempoObservado, e.TiempoNorma,
        e.DescProceso, e.DocsAdicionales, e.ObsFlujo, e.FuncionariosDig, e.TiempoDig, e.ObsModelo,
        e.InfraPersonal, e.InfraPersonalTI, e.InfraRespSol, e.InfraAcomp, e.InfraDcModalidad,
        e.InfraDcVirt, e.InfraDcVirtOtro, e.InfraDcDisp, e.InfraDcObs, e.InfraPlan,
        e.EstadoExpediente, e.EstadoLevantamiento, e.ObsExpediente, e.ObsLevantamiento,
        e.ValidadoDiger, e.ValidadoInst, e.FechaValidacion, e.NumActa,
        e.Tramites.OrderBy(t => t.TramiteIndex).Select(t => new TramiteInput(
            t.TramiteIndex, t.NombreTramite, t.NombreCorto, t.AreaResponsable, t.Modalidad, t.PlazoLegal,
            t.Tercero, t.TiempoReal, t.MetodoPago, t.PagoBanco, t.PagoCuenta, t.TgrInst, t.TgrRubro,
            t.TgrMonto, t.DocEntregado, t.Objetivo, t.Alcance, t.AlcanceObs, t.Descripcion, t.Dirigido,
            t.Horario, t.Telefono, t.EmailTramite, t.SitioWeb)).ToList(),
        e.Requisitos.OrderBy(r => r.TramiteIndex).ThenBy(r => r.Orden).Select(r => new RequisitoInput(
            r.TramiteIndex, r.Orden, r.Requisito, r.Obs, r.Accion, r.Justificacion)).ToList(),
        e.Flujos.OrderBy(n => n.TramiteIndex).ThenBy(n => n.Fase).ThenBy(n => n.Orden).Select(n => new FlujoNodoInput(
            n.TramiteIndex, n.Fase, n.Orden, n.Tipo, n.Titulo, n.Area, n.Tiempo, n.DocEmitido, n.Obs, n.RetornoA)).ToList(),
        e.Legal.OrderBy(l => l.Orden).Select(l => new LegalInput(l.Orden, l.Instrumento, l.Articulos, l.Obs)).ToList(),
        e.DocsSolicitados.OrderBy(x => x.Orden).Select(x => new DocSolicitadoInput(
            x.Orden, x.Nombre, x.Tipo, x.Recibido, x.Fecha, x.Url)).ToList(),
        e.DocsInternos.OrderBy(x => x.Orden).Select(x => new DocInternoInput(x.Orden, x.Documento, x.Area, x.Obs)).ToList(),
        e.Perfiles.Select(p => new PerfilInput(p.Perfil, p.Nombre, p.Correo)).ToList(),
        e.Condiciones.Select(c => c.Condicion).ToList(),
        e.ChecklistInfra.OrderBy(c => c.Orden).Select(c => new ChecklistInput(c.Orden, c.Grupo, c.Requisito, c.Status, c.Obs)).ToList(),
        e.Secciones.OrderBy(s => s.Seccion).Select(s => new SeccionInput(s.Seccion, s.Estado, s.Nota)).ToList());
}
