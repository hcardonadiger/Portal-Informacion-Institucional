namespace Diger.TramitesEstado.Application.Reuniones.Common;

public static class ReunionMapper
{
    public static void Aplicar(Reunion r, ReunionFormDto d, List<AsistenteInput> asistentes, List<AcuerdoInput> acuerdos)
    {
        r.EstablecerTitulo(d.Titulo);
        r.Fecha = d.Fecha; r.Hora = d.Hora?.Trim(); r.Duracion = d.Duracion?.Trim();
        r.Modalidad = d.Modalidad?.Trim(); r.Lugar = d.Lugar?.Trim();
        r.Tipo = d.Tipo?.Trim(); r.EsCapacitacionPlataforma = d.EsCapacitacionPlataforma;
        r.Visibilidad = d.Visibilidad;
        r.ObjetivoAgenda = d.ObjetivoAgenda; r.Desarrollo = d.Desarrollo;
        r.Tema = d.Tema?.Trim(); r.ObjetivoCap = d.ObjetivoCap; r.Contenido = d.Contenido;
        r.EpNombre = d.EpNombre?.Trim(); r.EpCargo = d.EpCargo?.Trim(); r.EpCorreo = d.EpCorreo?.Trim(); r.EpTel = d.EpTel?.Trim();
        r.FacNombre = d.FacNombre?.Trim(); r.FacCargo = d.FacCargo?.Trim(); r.FacCorreo = d.FacCorreo?.Trim();
        r.Convocados = d.Convocados; r.PctAsistencia = d.PctAsistencia;
        r.SatisfaccionCalificacion = d.SatisfaccionCalificacion; r.Satisfaccion = d.Satisfaccion?.Trim();
        r.Compromisos = d.Compromisos;
        r.ValDiger = d.ValDiger?.Trim(); r.ValInst = d.ValInst?.Trim(); r.DocsRecursos = d.DocsRecursos;
        r.Foto1Url = d.Foto1Url?.Trim(); r.Foto1Desc = d.Foto1Desc?.Trim();
        r.Foto2Url = d.Foto2Url?.Trim(); r.Foto2Desc = d.Foto2Desc?.Trim();

        // Instituciones convocadas (reemplazo en bloque; preserva el orden de captura)
        r.LimpiarInstituciones();
        foreach (var institucionId in d.InstitucionesIds.Distinct())
            r.AgregarInstitucion(institucionId);

        // Colecciones (reemplazo en bloque)
        r.LimpiarHijos();
        foreach (var a in asistentes.Where(x => !string.IsNullOrWhiteSpace(x.Nombre)))
            r.Agregar(new Asistente
            {
                Nombre = a.Nombre.Trim(), Cargo = a.Cargo?.Trim(),
                InstitucionId = string.IsNullOrWhiteSpace(a.InstitucionId) ? null : a.InstitucionId.Trim(),
                Institucion = a.Institucion?.Trim(),
                Departamento = a.Departamento?.Trim(),
                Correo = a.Correo?.Trim().ToLowerInvariant(), Telefono = a.Telefono?.Trim(),
                AutoRegistro = a.AutoRegistro, RegistradoEl = a.RegistradoEl
            });

        var orden = 0;
        foreach (var ac in acuerdos.Where(x => !string.IsNullOrWhiteSpace(x.Compromiso)))
            r.Agregar(new AcuerdoReunion
            {
                Orden = orden++, Compromiso = ac.Compromiso.Trim(),
                ResponsableContactoId = ac.ResponsableContactoId > 0 ? ac.ResponsableContactoId : null,
                Responsable = ac.Responsable?.Trim(), Plazo = ac.Plazo,
                Estado = ac.Estado,
                FechaCumplimiento = ac.Estado == EstadoCompromiso.Cumplido
                    ? (ac.FechaCumplimiento ?? DateOnly.FromDateTime(DateTime.Today))
                    : ac.FechaCumplimiento,
                NotaSeguimiento = string.IsNullOrWhiteSpace(ac.NotaSeguimiento) ? null : ac.NotaSeguimiento.Trim()
            });

        // Nº de asistentes: usar el conteo real si hay registros; si no, el valor capturado.
        r.NumAsistentes = r.Asistentes.Count > 0 ? r.Asistentes.Count : d.NumAsistentes;
    }

    public static (ReunionFormDto datos, List<AsistenteInput> asistentes, List<AcuerdoInput> acuerdos) ToForm(Reunion r)
    {
        var datos = new ReunionFormDto
        {
            Titulo = r.Titulo, Fecha = r.Fecha, Hora = r.Hora, Duracion = r.Duracion,
            Modalidad = r.Modalidad, Lugar = r.Lugar,
            InstitucionesIds = r.InstitucionesParticipantes.OrderBy(x => x.Orden).Select(x => x.InstitucionId).ToList(),
            Tipo = r.Tipo,
            EsCapacitacionPlataforma = r.EsCapacitacionPlataforma, Visibilidad = r.Visibilidad,
            ObjetivoAgenda = r.ObjetivoAgenda, Desarrollo = r.Desarrollo,
            Tema = r.Tema, ObjetivoCap = r.ObjetivoCap, Contenido = r.Contenido,
            EpNombre = r.EpNombre, EpCargo = r.EpCargo, EpCorreo = r.EpCorreo, EpTel = r.EpTel,
            FacNombre = r.FacNombre, FacCargo = r.FacCargo, FacCorreo = r.FacCorreo,
            Convocados = r.Convocados, NumAsistentes = r.Asistentes.Count > 0 ? r.Asistentes.Count : r.NumAsistentes, PctAsistencia = r.PctAsistencia,
            SatisfaccionCalificacion = r.SatisfaccionCalificacion, Satisfaccion = r.Satisfaccion, Compromisos = r.Compromisos,
            ValDiger = r.ValDiger, ValInst = r.ValInst, DocsRecursos = r.DocsRecursos,
            Foto1Url = r.Foto1Url, Foto1Desc = r.Foto1Desc, Foto2Url = r.Foto2Url, Foto2Desc = r.Foto2Desc
        };
        var asistentes = r.Asistentes.Select(a => new AsistenteInput
        {
            Nombre = a.Nombre, Cargo = a.Cargo,
            InstitucionId = a.InstitucionId, Institucion = a.Institucion, Departamento = a.Departamento,
            Correo = a.Correo, Telefono = a.Telefono, AutoRegistro = a.AutoRegistro, RegistradoEl = a.RegistradoEl
        }).ToList();
        var acuerdos = r.Acuerdos.OrderBy(a => a.Orden).Select(a => new AcuerdoInput
        {
            Compromiso = a.Compromiso, ResponsableContactoId = a.ResponsableContactoId,
            Responsable = a.Responsable, Plazo = a.Plazo,
            Estado = a.Estado, FechaCumplimiento = a.FechaCumplimiento, NotaSeguimiento = a.NotaSeguimiento
        }).ToList();
        return (datos, asistentes, acuerdos);
    }
}
