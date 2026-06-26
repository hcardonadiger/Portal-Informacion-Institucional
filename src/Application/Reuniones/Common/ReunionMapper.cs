namespace Diger.TramitesEstado.Application.Reuniones.Common;

public static class ReunionMapper
{
    public static void Aplicar(Reunion r, ReunionFormDto d, List<AsistenteInput> asistentes, List<AcuerdoInput> acuerdos)
    {
        r.EstablecerTitulo(d.Titulo);
        r.Fecha = d.Fecha; r.Hora = d.Hora?.Trim(); r.Duracion = d.Duracion?.Trim();
        r.Modalidad = d.Modalidad?.Trim(); r.Lugar = d.Lugar?.Trim();
        r.Tipo = d.Tipo?.Trim(); r.EsCapacitacionPlataforma = d.EsCapacitacionPlataforma;
        r.ObjetivoAgenda = d.ObjetivoAgenda; r.Desarrollo = d.Desarrollo;
        r.Tema = d.Tema?.Trim(); r.ObjetivoCap = d.ObjetivoCap; r.Contenido = d.Contenido;
        r.EpNombre = d.EpNombre?.Trim(); r.EpCargo = d.EpCargo?.Trim(); r.EpCorreo = d.EpCorreo?.Trim(); r.EpTel = d.EpTel?.Trim();
        r.FacNombre = d.FacNombre?.Trim(); r.FacCargo = d.FacCargo?.Trim(); r.FacCorreo = d.FacCorreo?.Trim();
        r.Convocados = d.Convocados; r.PctAsistencia = d.PctAsistencia; r.Satisfaccion = d.Satisfaccion?.Trim();
        r.Compromisos = d.Compromisos;
        r.ValDiger = d.ValDiger?.Trim(); r.ValInst = d.ValInst?.Trim(); r.DocsRecursos = d.DocsRecursos;
        r.Foto1Url = d.Foto1Url?.Trim(); r.Foto1Desc = d.Foto1Desc?.Trim();
        r.Foto2Url = d.Foto2Url?.Trim(); r.Foto2Desc = d.Foto2Desc?.Trim();

        // Colecciones (reemplazo en bloque)
        r.LimpiarHijos();
        foreach (var a in asistentes.Where(x => !string.IsNullOrWhiteSpace(x.Nombre)))
            r.Agregar(new Asistente
            {
                Nombre = a.Nombre.Trim(), Cargo = a.Cargo?.Trim(), Institucion = a.Institucion?.Trim(),
                Correo = a.Correo?.Trim().ToLowerInvariant(), Telefono = a.Telefono?.Trim()
            });

        var orden = 0;
        foreach (var ac in acuerdos.Where(x => !string.IsNullOrWhiteSpace(x.Compromiso)))
            r.Agregar(new AcuerdoReunion
            {
                Orden = orden++, Compromiso = ac.Compromiso.Trim(),
                Responsable = ac.Responsable?.Trim(), Plazo = ac.Plazo
            });

        // Nº de asistentes: usar el conteo real si hay registros; si no, el valor capturado.
        r.NumAsistentes = r.Asistentes.Count > 0 ? r.Asistentes.Count : d.NumAsistentes;
    }

    public static (ReunionFormDto datos, List<AsistenteInput> asistentes, List<AcuerdoInput> acuerdos) ToForm(Reunion r)
    {
        var datos = new ReunionFormDto
        {
            Titulo = r.Titulo, Fecha = r.Fecha, Hora = r.Hora, Duracion = r.Duracion,
            Modalidad = r.Modalidad, Lugar = r.Lugar, InstitucionId = r.InstitucionId, Tipo = r.Tipo,
            EsCapacitacionPlataforma = r.EsCapacitacionPlataforma,
            ObjetivoAgenda = r.ObjetivoAgenda, Desarrollo = r.Desarrollo,
            Tema = r.Tema, ObjetivoCap = r.ObjetivoCap, Contenido = r.Contenido,
            EpNombre = r.EpNombre, EpCargo = r.EpCargo, EpCorreo = r.EpCorreo, EpTel = r.EpTel,
            FacNombre = r.FacNombre, FacCargo = r.FacCargo, FacCorreo = r.FacCorreo,
            Convocados = r.Convocados, NumAsistentes = r.Asistentes.Count > 0 ? r.Asistentes.Count : r.NumAsistentes, PctAsistencia = r.PctAsistencia,
            Satisfaccion = r.Satisfaccion, Compromisos = r.Compromisos,
            ValDiger = r.ValDiger, ValInst = r.ValInst, DocsRecursos = r.DocsRecursos,
            Foto1Url = r.Foto1Url, Foto1Desc = r.Foto1Desc, Foto2Url = r.Foto2Url, Foto2Desc = r.Foto2Desc
        };
        var asistentes = r.Asistentes.Select(a => new AsistenteInput
        {
            Nombre = a.Nombre, Cargo = a.Cargo, Institucion = a.Institucion, Correo = a.Correo, Telefono = a.Telefono
        }).ToList();
        var acuerdos = r.Acuerdos.OrderBy(a => a.Orden).Select(a => new AcuerdoInput
        {
            Compromiso = a.Compromiso, Responsable = a.Responsable, Plazo = a.Plazo
        }).ToList();
        return (datos, asistentes, acuerdos);
    }
}
