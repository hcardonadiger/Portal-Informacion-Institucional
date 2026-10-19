namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
[Permission("Reuniones", AccionModulo.Editar, "Crear y editar reuniones")]
public sealed class EditorModel(
    ISender sender, IInstitucionRepository institucionRepo,
    ICurrentUserService currentUser, IWebHostEnvironment env) : PageModel
{
    /// <summary>Enciende el modo edición de la pantalla. Hoy siempre es true porque la clase
    /// entera exige Reuniones.Editar — la vista de solo lectura de una reunión es Acta.cshtml,
    /// que es adonde Index manda a quien no puede editar. Se deja resuelto contra la matriz y
    /// no fijo en true para que siga siendo correcto si alguna vez se afloja el gateo de la
    /// página.</summary>
    public bool EsAdmin { get; private set; } = true;
    public int? ReunionId { get; private set; }
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public IReadOnlyList<ContactoDto> ContactosDirectorio { get; private set; } = [];

    /// <summary>Todas las instituciones activas, sin acotar por el alcance del usuario. Convocar
    /// no es "ver": un usuario no global igual tiene que poder invitar a cualquier institución a
    /// una reunión, no solo a la suya —antes esto se filtraba como si fuera un dato con alcance,
    /// y solo aparecía la propia institución al crear una reunión desde un usuario no admin.
    ///
    /// Quitar ese filtro de acá no bastaba: el recorte de verdad estaba una capa más abajo, en el
    /// filtro global de EF sobre Institucion, así que `GetAllActivasAsync` ya llegaba con una sola
    /// fila desde la base. Por eso se pide explícitamente la variante que se lo salta.</summary>
    private async Task<IReadOnlyList<Institucion>> InstitucionesEnAlcanceAsync(CancellationToken ct) =>
        await institucionRepo.GetActivasParaConvocarAsync(ct);

    [BindProperty] public ReunionFormDto      Datos      { get; set; } = new();
    [BindProperty] public List<AsistenteInput> Asistentes { get; set; } = [];
    [BindProperty] public List<AcuerdoInput>   Acuerdos   { get; set; } = [];
    [BindProperty] public IFormFile?          Foto1File  { get; set; }
    [BindProperty] public IFormFile?          Foto2File  { get; set; }

    /// <summary>Filas de «Documentos y recursos»: archivos subidos al portal y enlaces externos.</summary>
    [BindProperty] public List<AdjuntoFormInput> AdjuntosDoc { get; set; } = [];
    /// <summary>Filas de enlaces de «Evidencia fotográfica y de video». Solo enlaces: el video no
    /// se sube —ver <see cref="ReunionAdjunto"/>— y las dos fotos siguen siendo Foto1/Foto2.</summary>
    [BindProperty] public List<AdjuntoFormInput> AdjuntosEvi { get; set; } = [];

    /// <summary>Tope por archivo, en MB, para mostrarlo en pantalla junto al botón de subir.</summary>
    public int    AdjuntoMaxMb  => (int)(UploadsConfig.ReunionesMaxBytes / (1024 * 1024));
    /// <summary>Valor del atributo <c>accept</c> del selector de archivos.</summary>
    public string AdjuntoAccept => string.Join(",", UploadsConfig.ExtensionesPermitidas);

    /// <summary>Guarda una imagen subida en wwwroot/uploads/reuniones y devuelve su ruta relativa.</summary>
    private async Task<string?> GuardarFotoAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > UploadsConfig.ReunionesMaxBytes)
            throw new DomainException($"La imagen supera el límite de {UploadsConfig.ReunionesMaxBytes / (1024 * 1024)} MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!UploadsConfig.ExtensionesImagenesPermitidas.Contains(ext)) throw new DomainException("Formato de imagen no permitido (use JPG, PNG, WEBP o GIF).");

        var dir = Path.Combine(env.ContentRootPath, "App_Data", "uploads", "reuniones");
        Directory.CreateDirectory(dir);
        var nombre = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(dir, nombre)))
            await file.CopyToAsync(fs, ct);
        return $"/uploads/reuniones/{nombre}";
    }

    /// <summary>
    /// Guarda un documento adjunto en la misma carpeta que las fotos y devuelve su ruta relativa.
    ///
    /// <para>Separado de <see cref="GuardarFotoAsync"/> porque acá la lista de extensiones es la
    /// general del portal —PDF, Word, Excel, PowerPoint, texto, ZIP— y no solo imágenes. El tope de
    /// tamaño sí es el mismo: lo que no quepa va a OneDrive como enlace, que es justamente la razón
    /// por la que esta pantalla ofrece las dos cosas.</para>
    /// </summary>
    private async Task<(string url, string nombre, long tamano)?> GuardarDocumentoAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > UploadsConfig.ReunionesMaxBytes)
            throw new DomainException(
                $"«{file.FileName}» pesa más de {AdjuntoMaxMb} MB. Súbalo a OneDrive y pegue acá el enlace.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!UploadsConfig.ExtensionesPermitidas.Contains(ext))
            throw new DomainException(
                $"No se permiten archivos «{ext}». Formatos aceptados: {string.Join(", ", UploadsConfig.ExtensionesPermitidas)}.");

        var dir = Path.Combine(env.ContentRootPath, "App_Data", "uploads", "reuniones");
        Directory.CreateDirectory(dir);
        var nombre = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(dir, nombre)))
            await file.CopyToAsync(fs, ct);

        return ($"/uploads/reuniones/{nombre}", Path.GetFileName(file.FileName), file.Length);
    }

    /// <summary>
    /// Convierte las filas de una tarjeta en lo que entiende la capa Application.
    ///
    /// <para><b>Escribe de vuelta en la fila</b> lo que resultó de subir el archivo. No es un
    /// efecto colateral descuidado: si más adelante algo falla y la página se vuelve a pintar, el
    /// navegador no puede repoblar un <c>&lt;input type=file&gt;</c>, así que sin esto el archivo
    /// que la persona acaba de subir desaparecería de la pantalla aunque ya esté en disco.</para>
    ///
    /// <para>Las filas sin destino —ni archivo nuevo ni URL— se descartan acá mismo: son renglones
    /// que alguien agregó y dejó en blanco.</para>
    ///
    /// <para><b>La clase no se le cree al formulario.</b> <c>Clase</c> y <c>Url</c> viajan en el
    /// POST, asi que una fila podia llegar diciendo «yo soy un archivo» con una ruta que nadie
    /// subio: bastaba apuntar a cualquier archivo de <c>App_Data/uploads</c> —la carpeta de todo el
    /// portal, no la de esta reunion— y bajarlo despues por <c>Acta?handler=Adjunto</c>, saltandose
    /// el alcance del modulo dueno del archivo. Aca la clase la decide el servidor: es archivo si
    /// se acaba de subir uno, o si la ruta ya estaba guardada <b>en esta misma reunion</b>
    /// (<paramref name="archivosDeLaReunion"/>, leido de la base). Lo demas es enlace, y como
    /// enlace pasa por <c>NormalizarEnlace</c>, que solo acepta http/https absolutas.</para>
    /// </summary>
    private async Task<List<ReunionAdjuntoInput>> ConvertirAdjuntosAsync(
        List<AdjuntoFormInput> filas, SeccionAdjuntoReunion seccion,
        Dictionary<string, (string? Nombre, long Tamano)> archivosDeLaReunion,
        CancellationToken ct)
    {
        var salida = new List<ReunionAdjuntoInput>();

        foreach (var fila in filas)
        {
            if (await GuardarDocumentoAsync(fila.Archivo, ct) is { } subido)
            {
                fila.Clase         = ClaseAdjuntoReunion.Archivo;
                fila.Url           = subido.url;
                fila.NombreArchivo = subido.nombre;
                fila.Tamano        = subido.tamano;
                if (string.IsNullOrWhiteSpace(fila.Titulo)) fila.Titulo = subido.nombre;
                archivosDeLaReunion[subido.url] = (subido.nombre, subido.tamano);
            }

            if (string.IsNullOrWhiteSpace(fila.Url)) continue;

            var esArchivo = archivosDeLaReunion.TryGetValue(fila.Url, out var guardado);

            // Una fila que dice ser archivo sin serlo se descarta en vez de degradarse a enlace:
            // una ruta de disco no es una direccion http y rebotaria en el dominio con un mensaje
            // que no le explica nada a quien esta capturando.
            if (!esArchivo && fila.Clase == ClaseAdjuntoReunion.Archivo) continue;

            fila.Clase = esArchivo ? ClaseAdjuntoReunion.Archivo : ClaseAdjuntoReunion.Enlace;
            if (esArchivo)
            {
                fila.NombreArchivo = guardado.Nombre;
                fila.Tamano        = guardado.Tamano;
            }

            salida.Add(new ReunionAdjuntoInput
            {
                Seccion       = seccion,
                Clase         = fila.Clase,
                Titulo        = fila.Titulo ?? string.Empty,
                Descripcion   = fila.Descripcion,
                NombreArchivo = esArchivo ? fila.NombreArchivo : null,
                Url           = fila.Url,
                Tamano        = esArchivo ? fila.Tamano : 0
            });
        }

        return salida;
    }

    /// <summary>Rutas de archivo que esta reunion <b>ya tiene guardadas</b>, leidas de la base y no
    /// del formulario, con el nombre y el tamano con que se guardaron. Es la lista blanca contra la
    /// que se valida una fila que dice ser archivo. En una reunion nueva esta vacia: ahi lo unico
    /// que puede ser archivo es lo que se acaba de subir.</summary>
    private async Task<Dictionary<string, (string? Nombre, long Tamano)>> ArchivosYaGuardadosAsync(
        int? id, CancellationToken ct)
    {
        var mapa = new Dictionary<string, (string? Nombre, long Tamano)>(StringComparer.Ordinal);
        if (id is null) return mapa;

        try
        {
            var datos = (await sender.Send(new GetReunionByIdQuery(id.Value), ct)).Datos;
            foreach (var a in datos.Adjuntos)
                if (a.Clase == ClaseAdjuntoReunion.Archivo && !string.IsNullOrWhiteSpace(a.Url))
                    mapa[a.Url!] = (a.NombreArchivo, a.Tamano);
        }
        catch (NotFoundException)
        {
            // Fuera de alcance: no hay archivo propio que reconocer, y el comando de guardado
            // fallara por su cuenta un momento despues.
        }

        return mapa;
    }

    public string? Error { get; set; }

    public string[] Modalidades => ["Presencial", "Virtual", "Híbrida", "Otro"];
    public string[] Tipos =>
        ["Taller", "Seminario", "Charla", "Curso", "Reunión técnica", "Capacitación", "Inducción", "Conferencia", "Otro"];

    public async Task<IActionResult> OnGetAsync(int? id, DateOnly? fecha, CancellationToken ct)
    {
        if (!EsAdmin)
            return Forbid();

        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        ContactosDirectorio = await sender.Send(new GetContactosQuery(), ct);

        if (id is null)
        {
            if (fecha is { } f) Datos.Fecha = f;   // pre-llenado desde el calendario
            return Page();
        }

        try
        {
            var d = await sender.Send(new GetReunionByIdQuery(id.Value), ct);
            ReunionId  = d.Id;
            Datos      = d.Datos;
            Asistentes = d.Asistentes;
            Acuerdos   = d.Acuerdos;
            RepartirAdjuntos(d.Datos);
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        if (!EsAdmin)
            return Forbid();
        ReunionId = id;
        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        ContactosDirectorio = await sender.Send(new GetContactosQuery(), ct);

        if (string.IsNullOrWhiteSpace(Datos.Titulo))
        {
            Error = "El título de la reunión es obligatorio.";
            return Page();
        }

        try
        {
            await GuardarAsync(id, ct);
            TempData["SuccessMsg"] = id is null ? "Reunión creada." : "Reunión actualizada.";
            return RedirectToPage("/Reuniones/Index");
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    /// <summary>Autoguardado del paso "Generales": crea o actualiza la reunión con lo capturado
    /// hasta ahora y recarga la misma página, donde ya queda disponible el enlace/QR de
    /// auto-registro (requiere que la reunión exista para tener un token).</summary>
    public async Task<IActionResult> OnPostGuardarContinuarAsync(int? id, [FromForm] int paso, CancellationToken ct)
    {
        ReunionId = id;
        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        ContactosDirectorio = await sender.Send(new GetContactosQuery(), ct);

        if (string.IsNullOrWhiteSpace(Datos.Titulo))
        {
            Error = "El título de la reunión es obligatorio.";
            return Page();
        }

        try
        {
            var reunionId = await GuardarAsync(id, ct);
            TempData["SuccessMsg"] = "Cambios guardados.";
            return RedirectToPage(new { id = reunionId, paso = paso });
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    /// <summary>Guarda las fotos adjuntas y crea/actualiza la reunión. Devuelve el Id resultante.</summary>
    private async Task<int> GuardarAsync(int? id, CancellationToken ct)
    {
        var f1 = await GuardarFotoAsync(Foto1File, ct); if (f1 is not null) Datos.Foto1Url = f1;
        var f2 = await GuardarFotoAsync(Foto2File, ct); if (f2 is not null) Datos.Foto2Url = f2;

        var archivos = await ArchivosYaGuardadosAsync(id, ct);

        Datos.Adjuntos =
        [
            .. await ConvertirAdjuntosAsync(AdjuntosDoc, SeccionAdjuntoReunion.Documento, archivos, ct),
            .. await ConvertirAdjuntosAsync(AdjuntosEvi, SeccionAdjuntoReunion.Evidencia, archivos, ct)
        ];

        if (id is null)
            return await sender.Send(new CrearReunionCommand(Datos, Asistentes, Acuerdos), ct);

        await sender.Send(new ActualizarReunionCommand(id.Value, Datos, Asistentes, Acuerdos), ct);
        return id.Value;
    }

    /// <summary>Reparte los adjuntos que trae la reunión en las dos tarjetas del cierre, que es
    /// como se editan. Se hace acá y no en el mapper porque la separación es de pantalla: en la
    /// base es una sola tabla distinguida por <c>Seccion</c>.</summary>
    private void RepartirAdjuntos(ReunionFormDto datos)
    {
        AdjuntosDoc = Filas(datos, SeccionAdjuntoReunion.Documento);
        AdjuntosEvi = Filas(datos, SeccionAdjuntoReunion.Evidencia);

        static List<AdjuntoFormInput> Filas(ReunionFormDto d, SeccionAdjuntoReunion s) =>
            d.Adjuntos.Where(a => a.Seccion == s).Select(a => new AdjuntoFormInput
            {
                Clase         = a.Clase,
                Titulo        = a.Titulo,
                Descripcion   = a.Descripcion,
                NombreArchivo = a.NombreArchivo,
                Url           = a.Url,
                Tamano        = a.Tamano
            }).ToList();
    }
}

/// <summary>Una fila de las listas de documentos o de evidencia, tal como viaja en el formulario.
/// Es la versión Web de <see cref="ReunionAdjuntoInput"/>: igual, más el archivo que se está subiendo —que
/// no puede vivir en la capa Application— y sin <c>Seccion</c>, que lo determina en qué lista
/// llegó.</summary>
public sealed class AdjuntoFormInput
{
    public ClaseAdjuntoReunion Clase { get; set; } = ClaseAdjuntoReunion.Enlace;

    public string? Titulo        { get; set; }
    public string? Descripcion   { get; set; }
    public string? NombreArchivo { get; set; }
    public string? Url           { get; set; }
    public long    Tamano        { get; set; }

    /// <summary>Archivo recién elegido. <c>null</c> en una fila que ya tenía su archivo subido o
    /// que es un enlace.</summary>
    public IFormFile? Archivo { get; set; }
}

/// <summary>Lo que necesita la parcial <c>_AdjuntoFila</c> para pintar una fila: en qué lista va
/// (<paramref name="Prefijo"/> = <c>AdjuntosDoc</c> o <c>AdjuntosEvi</c>), con qué índice, y los
/// formatos que acepta el selector de archivos.</summary>
public sealed record AdjuntoFilaVm(string Prefijo, int Indice, AdjuntoFormInput Fila, string Accept);
