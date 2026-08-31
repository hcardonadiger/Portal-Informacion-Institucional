namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Categoría del repositorio documental: Acta, Convenio, Informe, Contrato…
///
/// <para><b>Catálogo administrable, no un string suelto.</b> <see cref="Recurso.Categoria"/> es
/// texto libre con los valores sugeridos en un comentario del código: no se puede filtrar con eso
/// ni agregar una categoría sin recompilar. Es la misma deuda que ya se saldó convirtiendo el enum
/// de roles en tabla, y no conviene repetirla.</para>
///
/// <para><b>Global y no por proyecto</b>, a propósito: la biblioteca centralizada filtra por
/// categoría a través de todo el portafolio, y un catálogo por proyecto haría imposible esa
/// consulta —«todos los convenios firmados» no se podría responder si cada proyecto inventara su
/// propia palabra para «convenio»—.</para>
///
/// <para>No se borra: se desactiva. Una categoría en uso tiene documentos apuntándole, y darla de
/// baja de verdad los dejaría sin clasificar. Mismo criterio que <see cref="Rol.Activo"/>.</para>
/// </summary>
public sealed class CategoriaDocumento : BaseAuditableEntity
{
    public string  Nombre      { get; private set; } = default!;
    public string? Descripcion { get; private set; }

    /// <summary>Orden en que se muestra y se agrupa. No es el Id: las categorías se reordenan.</summary>
    public int Orden { get; set; }

    /// <summary>Desactivada: no se ofrece al clasificar un documento nuevo, pero los que ya la
    /// tienen la conservan y se siguen viendo.</summary>
    public bool Activa { get; private set; } = true;

    private CategoriaDocumento() { }   // EF

    public static CategoriaDocumento Crear(string nombre, int orden, string? descripcion = null)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("La categoría necesita un nombre.");
        if (limpio.Length > MaxNombre)
            throw new DomainException($"El nombre de la categoría no puede superar los {MaxNombre} caracteres.");

        return new CategoriaDocumento
        {
            Nombre      = limpio,
            Orden       = orden,
            Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim()
        };
    }

    /// <returns><c>true</c> si algo cambió.</returns>
    public bool Definir(string nombre, string? descripcion)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("La categoría necesita un nombre.");
        if (limpio.Length > MaxNombre)
            throw new DomainException($"El nombre de la categoría no puede superar los {MaxNombre} caracteres.");

        var desc = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        var cambio = Nombre != limpio || Descripcion != desc;

        Nombre      = limpio;
        Descripcion = desc;
        return cambio;
    }

    public bool CambiarActiva(bool activa)
    {
        if (Activa == activa) return false;
        Activa = activa;
        return true;
    }

    public const int MaxNombre      = 120;
    public const int MaxDescripcion = 500;
}

/// <summary>
/// Un documento del repositorio de un proyecto: el convenio, el acta de entrega, el informe final.
///
/// <para><b>No es evidencia de avance.</b> La bitácora (<see cref="AvanceProyecto"/>) guarda el
/// archivo que respalda un reporte concreto —«el acta con la que se cerró esta actividad»—. Esto
/// es otra cosa: la documentación del proyecto, que existe con independencia de qué se reportó y
/// cuándo. Por eso son dos tablas y no una, y por eso este documento no cuelga de un entregable ni
/// de una actividad.</para>
///
/// <para><b>Quién lo ve no se decide acá.</b> El documento pertenece a un proyecto y hereda su
/// visibilidad completa: alcance global, el responsable, cualquier interesado, o quien caiga
/// dentro de su institución, área o unidad. Es el filtro que ya vive en <c>AppDbContext</c> para
/// <see cref="Proyecto"/>, y no hay un nivel de confidencialidad por documento —decisión explícita:
/// el que puede abrir el proyecto puede leer su documentación—.</para>
///
/// <para><b>Tabla independiente, no navegación del agregado</b>, igual que la bitácora y los
/// riesgos: lo que cuelga de <see cref="Proyecto"/> se arrastra en sus operaciones de colección,
/// y eso ya costó una vez perder imputaciones al reeditar la estructura.</para>
/// </summary>
public sealed class DocumentoProyecto : BaseAuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public int ProyectoId  { get; private set; }
    public int CategoriaId { get; set; }

    public string  Titulo      { get; private set; } = default!;
    public string? Descripcion { get; private set; }

    private readonly List<VersionDocumento> _versiones = [];
    public IReadOnlyCollection<VersionDocumento> Versiones => _versiones.AsReadOnly();

    private DocumentoProyecto() { }   // EF

    public static DocumentoProyecto Crear(int proyectoId, int categoriaId, string titulo, string? descripcion = null)
    {
        if (proyectoId <= 0)  throw new DomainException("El documento tiene que pertenecer a un proyecto.");
        if (categoriaId <= 0) throw new DomainException("El documento necesita una categoría.");

        var doc = new DocumentoProyecto { ProyectoId = proyectoId, CategoriaId = categoriaId };
        doc.Definir(titulo, descripcion);
        return doc;
    }

    /// <returns><c>true</c> si algo cambió.</returns>
    public bool Definir(string titulo, string? descripcion)
    {
        var limpio = (titulo ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("El documento necesita un título.");
        if (limpio.Length > MaxTitulo)
            throw new DomainException($"El título no puede superar los {MaxTitulo} caracteres.");

        var desc = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        var cambio = Titulo != limpio || Descripcion != desc;

        Titulo      = limpio;
        Descripcion = desc;
        return cambio;
    }

    /// <summary>
    /// Agrega una versión nueva y la deja como vigente.
    ///
    /// <para>El número se calcula acá y no lo elige quien sube: es lo que garantiza que la
    /// secuencia no tenga huecos ni repetidos aunque dos personas suban a la vez —la restricción
    /// única en la base es la red de abajo—.</para>
    ///
    /// <para>Necesita las versiones cargadas. Con la colección sin traer, la numeración
    /// arrancaría de 1 otra vez y chocaría contra el índice único, que es preferible a
    /// sobrescribir en silencio la versión anterior.</para>
    /// </summary>
    public VersionDocumento AgregarVersion(
        string archivoNombre,
        string archivoUrl,
        long   archivoTamano,
        string sha256,
        string subidoPor,
        string? notas = null)
    {
        var numero = _versiones.Count == 0 ? 1 : _versiones.Max(v => v.Numero) + 1;

        var version = VersionDocumento.Crear(
            numero, archivoNombre, archivoUrl, archivoTamano, sha256, subidoPor, notas);

        _versiones.Add(version);
        return version;
    }

    /// <summary>
    /// La versión que rige: la de mayor número.
    ///
    /// <para>Se calcula en vez de guardarse en una columna «VersionVigenteId». Un puntero habría
    /// que mantenerlo sincronizado en cada alta y quedaría mal en cuanto alguien insertara una fila
    /// por SQL; el máximo no puede desincronizarse de nada.</para>
    /// </summary>
    public VersionDocumento? Vigente => _versiones.OrderByDescending(v => v.Numero).FirstOrDefault();

    public int TotalVersiones => _versiones.Count;

    public const int MaxTitulo      = 300;
    public const int MaxDescripcion = 2000;
}

/// <summary>
/// Una versión concreta del archivo de un documento.
///
/// <para><b>Append-only.</b> Ninguna versión se borra ni se pisa: reemplazar un acta firmada por
/// otra sin dejar rastro es exactamente lo que un repositorio institucional no debe permitir. Dar
/// de baja el documento entero sí se puede —borrado lógico—, y entonces se va con su historia.</para>
/// </summary>
public sealed class VersionDocumento : BaseEntity
{
    public int DocumentoId { get; set; }

    /// <summary>1, 2, 3… La vigente es la mayor. Lo asigna
    /// <see cref="DocumentoProyecto.AgregarVersion"/>, nunca quien sube.</summary>
    public int Numero { get; private set; }

    public string ArchivoNombre { get; private set; } = default!;

    /// <summary>Ruta relativa bajo <c>App_Data/uploads</c>. <b>Nunca se expone en un DTO</b>: la
    /// descarga pasa por el handler autenticado, igual que la evidencia de la bitácora. Ver
    /// <c>ArchivosProtegidos</c>.</summary>
    public string ArchivoUrl { get; private set; } = default!;

    public long ArchivoTamano { get; private set; }

    /// <summary>Huella del contenido. Sirve para dos cosas concretas: avisar que alguien está
    /// volviendo a subir el mismo archivo con otro nombre, y poder verificar años después que lo
    /// archivado es lo que se archivó.</summary>
    public string Sha256 { get; private set; } = default!;

    /// <summary>Qué cambió respecto de la anterior. Vacío en la primera.</summary>
    public string? Notas { get; private set; }

    public string   SubidoPor { get; private set; } = default!;
    public DateTime SubidoEn  { get; private set; }

    private VersionDocumento() { }   // EF

    internal static VersionDocumento Crear(
        int     numero,
        string  archivoNombre,
        string  archivoUrl,
        long    archivoTamano,
        string  sha256,
        string  subidoPor,
        string? notas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivoNombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivoUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        if (archivoTamano <= 0)
            throw new DomainException("El archivo llegó vacío.");

        return new VersionDocumento
        {
            Numero        = numero,
            ArchivoNombre = archivoNombre.Trim(),
            ArchivoUrl    = archivoUrl.Trim(),
            ArchivoTamano = archivoTamano,
            Sha256        = sha256.Trim().ToLowerInvariant(),
            SubidoPor     = string.IsNullOrWhiteSpace(subidoPor) ? "—" : subidoPor.Trim(),
            Notas         = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim(),
            SubidoEn      = DateTime.UtcNow
        };
    }

    public const int MaxNombre = 300;
    public const int MaxNotas  = 1000;
}

/// <summary>
/// Una descarga de una versión de documento: quién la bajó y cuándo.
///
/// <para><b>Es una bitácora, no un contador.</b> <see cref="Recurso.DescargasCount"/> resuelve el
/// caso de la biblioteca pública con un entero que se incrementa, y ahí alcanza: interesa cuánto se
/// usa una plantilla, no quién la usó. Acá la pregunta es otra —«quién se llevó el convenio antes
/// de que se firmara»— y esa no se responde con un número.</para>
///
/// <para><b>Apunta a la versión, no al documento.</b> Un documento vive; sus versiones son lo que
/// efectivamente se entrega. Saber que alguien descargó «Convenio marco» no dice nada si no consta
/// que descargó la v1 y no la v2 ya corregida.</para>
///
/// <para>Se guarda el <see cref="UsuarioId"/> y además el <see cref="Usuario"/> como copia del
/// nombre, mismo criterio que el resto del portal: el registro tiene que seguir siendo legible
/// dentro de tres años, cuando la persona ya no esté o se llame distinto.</para>
///
/// <para><b>No se registra la dirección IP.</b> El portal no la guarda en ninguna otra parte y
/// añadirla acá convertiría una traza de uso en un rastro de ubicación, que es otra decisión y
/// tendría que tomarse aparte.</para>
///
/// <para>Tabla independiente, como el resto de los hijos de <see cref="Proyecto"/>: lo que cuelga
/// del agregado se arrastra en sus operaciones de colección, y una bitácora que crece sin techo no
/// tiene por qué cargarse cada vez que se abre un documento.</para>
///
/// <para><b>Nunca se edita ni se borra.</b> Un registro de acceso que se puede corregir no sirve
/// para lo único que sirve un registro de acceso.</para>
/// </summary>
public sealed class DescargaDocumento : BaseEntity
{
    /// <summary>Versión concreta que se descargó.</summary>
    public int VersionId { get; private set; }

    /// <summary>Usuario que la descargó. Toda descarga pasa por un handler autenticado, así que
    /// nunca es anónima.</summary>
    public Guid UsuarioId { get; private set; }

    /// <summary>Copia del nombre al momento de la descarga.</summary>
    public string Usuario { get; private set; } = default!;

    public DateTime FechaHora { get; private set; }

    private DescargaDocumento() { }   // EF

    public static DescargaDocumento Registrar(int versionId, Guid usuarioId, string? usuario)
    {
        if (versionId <= 0)
            throw new DomainException("La descarga tiene que apuntar a una versión.");
        if (usuarioId == Guid.Empty)
            throw new DomainException("La descarga tiene que quedar imputada a un usuario.");

        return new DescargaDocumento
        {
            VersionId = versionId,
            UsuarioId = usuarioId,
            Usuario   = string.IsNullOrWhiteSpace(usuario) ? "—" : usuario.Trim(),
            FechaHora = DateTime.UtcNow
        };
    }

    public const int MaxUsuario = 200;
}
