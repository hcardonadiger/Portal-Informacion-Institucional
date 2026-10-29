namespace Diger.TramitesEstado.Application.Reuniones.Common;

public static class ReunionMapper
{
    public static void Aplicar(Reunion r, ReunionFormDto d, List<AsistenteInput> asistentes, List<AcuerdoInput> acuerdos)
    {
        // En una reunión que vino de Outlook, Outlook manda sobre estos cinco (spec 2026-10-29):
        // el editor los muestra de solo lectura y aquí se ignoran aunque lleguen en el formulario.
        if (!r.EsImportadaDeOutlook)
        {
            r.EstablecerTitulo(d.Titulo);
            r.Fecha = d.Fecha; r.Hora = d.Hora?.Trim(); r.Duracion = d.Duracion?.Trim();
            r.Lugar = d.Lugar?.Trim();
        }
        r.Modalidad = d.Modalidad?.Trim();
        r.ExpedienteId = d.ExpedienteId; r.ExpedienteCodigo = d.ExpedienteCodigo?.Trim();
        r.Tipo = d.Tipo?.Trim(); r.EsCapacitacionPlataforma = d.EsCapacitacionPlataforma;
        r.Visibilidad = d.Visibilidad;
        r.ObjetivoAgenda = d.ObjetivoAgenda; r.Desarrollo = d.Desarrollo;
        r.Tema = d.Tema?.Trim();
        r.EpNombre = d.EpNombre?.Trim(); r.EpCargo = d.EpCargo?.Trim(); r.EpCorreo = d.EpCorreo?.Trim(); r.EpTel = d.EpTel?.Trim();
        r.FacNombre = d.FacNombre?.Trim(); r.FacCargo = d.FacCargo?.Trim(); r.FacCorreo = d.FacCorreo?.Trim();
        r.Convocados = d.Convocados; r.PctAsistencia = d.PctAsistencia;
        r.SatisfaccionCalificacion = d.SatisfaccionCalificacion; r.Satisfaccion = d.Satisfaccion?.Trim();
        r.Compromisos = d.Compromisos;
        r.EncuestaActiva = d.EncuestaActiva;
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
                AutoRegistro = a.AutoRegistro, RegistradoEl = a.RegistradoEl,
                EsPreregistro = a.EsPreregistro, Confirmado = a.Confirmado, DeOutlook = a.DeOutlook
            });

        var orden = 0;
        foreach (var ac in acuerdos.Where(x => !string.IsNullOrWhiteSpace(x.Compromiso)))
            r.Agregar(new AcuerdoReunion
            {
                Orden = orden++, Compromiso = ac.Compromiso.Trim(),
                ResponsableContactoId = ac.ResponsableContactoId > 0 ? ac.ResponsableContactoId : null,
                Responsable = ac.Responsable?.Trim(), Plazo = ac.Plazo,
                ExpedienteId = ac.ExpedienteId, TramiteIndex = ac.TramiteIndex, TramiteNombre = ac.TramiteNombre?.Trim(),
                Estado = ac.Estado,
                FechaCumplimiento = ac.Estado == EstadoCompromiso.Cumplido
                    ? (ac.FechaCumplimiento ?? DateOnly.FromDateTime(DateTime.Today))
                    : ac.FechaCumplimiento,
                NotaSeguimiento = string.IsNullOrWhiteSpace(ac.NotaSeguimiento) ? null : ac.NotaSeguimiento.Trim()
            });

        AplicarAdjuntos(r, d.Adjuntos);

        // Nº de asistentes: usar el conteo real si hay registros; si no, el valor capturado.
        r.NumAsistentes = r.Asistentes.Count > 0 ? r.Asistentes.Count : d.NumAsistentes;
    }

    /// <summary>
    /// Reemplaza en bloque los documentos y enlaces del cierre.
    ///
    /// <para>Descarta las filas sin <c>Url</c>: son renglones que alguien agregó y dejó vacíos, y
    /// un adjunto que no apunta a nada no sirve de nada. En cambio una fila <b>con</b> destino pero
    /// sin nombre no se rechaza —se le pone como título el nombre del archivo, o la propia
    /// dirección— porque hacerla fallar botaría al editor y, con él, el archivo que la persona
    /// acaba de subir. El orden se numera por tarjeta, que es como se muestran.</para></summary>
    private static void AplicarAdjuntos(Reunion r, List<ReunionAdjuntoInput> adjuntos)
    {
        var ordenPorSeccion = new Dictionary<SeccionAdjuntoReunion, int>();

        foreach (var a in adjuntos.Where(x => !string.IsNullOrWhiteSpace(x.Url)))
        {
            var orden = ordenPorSeccion.TryGetValue(a.Seccion, out var n) ? n : 0;
            ordenPorSeccion[a.Seccion] = orden + 1;

            var url = a.Url!.Trim();
            var titulo = string.IsNullOrWhiteSpace(a.Titulo)
                ? (string.IsNullOrWhiteSpace(a.NombreArchivo) ? url : a.NombreArchivo!)
                : a.Titulo;

            r.Agregar(a.Clase == ClaseAdjuntoReunion.Archivo
                ? ReunionAdjunto.ParaArchivo(a.Seccion, orden, titulo, a.Descripcion,
                      string.IsNullOrWhiteSpace(a.NombreArchivo) ? url : a.NombreArchivo!, url, a.Tamano)
                : ReunionAdjunto.ParaEnlace(a.Seccion, orden, titulo, a.Descripcion, url));
        }
    }

    public static (ReunionFormDto datos, List<AsistenteInput> asistentes, List<AcuerdoInput> acuerdos) ToForm(Reunion r)
    {
        var datos = new ReunionFormDto
        {
            Titulo = r.Titulo, Fecha = r.Fecha, Hora = r.Hora, Duracion = r.Duracion,
            Modalidad = r.Modalidad, Lugar = r.Lugar, ExpedienteId = r.ExpedienteId, ExpedienteCodigo = r.ExpedienteCodigo,
            EsImportadaDeOutlook = r.EsImportadaDeOutlook, CanceladaEnOutlookEl = r.CanceladaEnOutlookEl,
            InstitucionesIds = r.InstitucionesParticipantes.OrderBy(x => x.Orden).Select(x => x.InstitucionId).ToList(),
            Tipo = r.Tipo,
            EsCapacitacionPlataforma = r.EsCapacitacionPlataforma, Visibilidad = r.Visibilidad,
            ObjetivoAgenda = r.ObjetivoAgenda, Desarrollo = r.Desarrollo,
            Tema = r.Tema,
            EpNombre = r.EpNombre, EpCargo = r.EpCargo, EpCorreo = r.EpCorreo, EpTel = r.EpTel,
            FacNombre = r.FacNombre, FacCargo = r.FacCargo, FacCorreo = r.FacCorreo,
            Convocados = r.Convocados, NumAsistentes = r.Asistentes.Count > 0 ? r.Asistentes.Count : r.NumAsistentes, PctAsistencia = r.PctAsistencia,
            SatisfaccionCalificacion = r.SatisfaccionCalificacion, Satisfaccion = r.Satisfaccion, Compromisos = r.Compromisos,
            EncuestaActiva = r.EncuestaActiva,
            ValDiger = r.ValDiger, ValInst = r.ValInst, DocsRecursos = r.DocsRecursos,
            Foto1Url = r.Foto1Url, Foto1Desc = r.Foto1Desc, Foto2Url = r.Foto2Url, Foto2Desc = r.Foto2Desc,
            Adjuntos = r.Adjuntos
                .OrderBy(a => a.Seccion).ThenBy(a => a.Orden)
                .Select(a => new ReunionAdjuntoInput
                {
                    Id = a.Id,
                    Seccion = a.Seccion, Clase = a.Clase,
                    Titulo = a.Titulo, Descripcion = a.Descripcion,
                    NombreArchivo = a.NombreArchivo, Url = a.Url, Tamano = a.Tamano
                }).ToList()
        };
        var asistentes = r.Asistentes.Select(a => new AsistenteInput
        {
            Nombre = a.Nombre, Cargo = a.Cargo,
            InstitucionId = a.InstitucionId, Institucion = a.Institucion, Departamento = a.Departamento,
            Correo = a.Correo, Telefono = a.Telefono, AutoRegistro = a.AutoRegistro, RegistradoEl = a.RegistradoEl,
            EsPreregistro = a.EsPreregistro, Confirmado = a.Confirmado, DeOutlook = a.DeOutlook
        }).ToList();
        var acuerdos = r.Acuerdos.OrderBy(a => a.Orden).Select(a => new AcuerdoInput
        {
            Compromiso = a.Compromiso, ResponsableContactoId = a.ResponsableContactoId,
            Responsable = a.Responsable, Plazo = a.Plazo,
            ExpedienteId = a.ExpedienteId, TramiteIndex = a.TramiteIndex, TramiteNombre = a.TramiteNombre,
            Estado = a.Estado, FechaCumplimiento = a.FechaCumplimiento, NotaSeguimiento = a.NotaSeguimiento
        }).ToList();
        return (datos, asistentes, acuerdos);
    }
}
