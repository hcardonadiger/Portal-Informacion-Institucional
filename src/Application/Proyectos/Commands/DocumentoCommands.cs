namespace Diger.TramitesEstado.Application.Proyectos.Commands;

/// <summary>
/// Cosas que los cuatro comandos del repositorio necesitan por igual.
/// </summary>
internal static class DocumentosProyecto
{
    /// <summary>
    /// Trae el proyecto por la consulta normal —y por lo tanto por su filtro de alcance— y rechaza
    /// el trabajo sobre uno cerrado.
    ///
    /// <para>Cargar el proyecto no es un trámite: es <b>la</b> comprobación de autorización. Sin
    /// esto, alguien podría adjuntar documentación a un proyecto que no puede ni abrir pasando su
    /// Id en el formulario.</para>
    /// </summary>
    public static async Task<Proyecto> ProyectoAbiertoAsync(
        IApplicationDbContext ctx, int proyectoId, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == proyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), proyectoId);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException(
                $"El proyecto está «{proyecto.Estado}»: su documentación queda como está.");

        return proyecto;
    }

    /// <summary>
    /// El documento, resuelto contra su proyecto.
    ///
    /// <para>Se pide el <c>proyectoId</c> además del Id del documento y se comprueba que
    /// coincidan. El filtro ya impide tocar un documento ajeno, pero esto ataja algo distinto: un
    /// formulario viejo o de otra pestaña que mande el documento de OTRO proyecto que la persona
    /// sí puede ver. Sin la comprobación, la bitácora quedaría escrita en el proyecto equivocado.</para>
    /// </summary>
    public static async Task<DocumentoProyecto> DelProyectoAsync(
        IApplicationDbContext ctx, int proyectoId, int documentoId, bool conVersiones, CancellationToken ct)
    {
        var consulta = ctx.ProyectoDocumentos.AsQueryable();
        if (conVersiones) consulta = consulta.Include(d => d.Versiones);

        var documento = await consulta.FirstOrDefaultAsync(d => d.Id == documentoId, ct)
            ?? throw new NotFoundException(nameof(DocumentoProyecto), documentoId);

        if (documento.ProyectoId != proyectoId)
            throw new DomainException("Ese documento no pertenece a este proyecto.");

        return documento;
    }

    public static async Task ExigirCategoriaActivaAsync(
        IApplicationDbContext ctx, int categoriaId, CancellationToken ct)
    {
        var categoria = await ctx.CategoriasDocumento.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoriaId, ct)
            ?? throw new DomainException("La categoría indicada no existe.");

        if (!categoria.Activa)
            throw new DomainException(
                $"La categoría «{categoria.Nombre}» está desactivada y no se puede asignar a un documento nuevo.");
    }

    public static void Auditar(IApplicationDbContext ctx, int proyectoId, string detalle, string actor) =>
        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyectoId, TipoEventoProyecto.Documentacion, detalle, actor));
}

// ── Subir un documento nuevo ──────────────────────────────────────────────
/// <summary>
/// Da de alta un documento con su primera versión.
///
/// <para>El archivo ya está en disco cuando llega acá: la capa web lo guarda y calcula su huella,
/// igual que hace con la evidencia de la bitácora. Este comando registra el hecho.</para>
/// </summary>
public sealed record SubirDocumentoCommand(
    int     ProyectoId,
    int     CategoriaId,
    string  Titulo,
    string? Descripcion,
    string  ArchivoNombre,
    string  ArchivoUrl,
    long    ArchivoTamano,
    string  Sha256) : IRequest<int>;

public sealed class SubirDocumentoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<SubirDocumentoCommand, int>
{
    public async Task<int> Handle(SubirDocumentoCommand cmd, CancellationToken ct)
    {
        await DocumentosProyecto.ProyectoAbiertoAsync(ctx, cmd.ProyectoId, ct);
        await DocumentosProyecto.ExigirCategoriaActivaAsync(ctx, cmd.CategoriaId, ct);

        var actor = currentUser.Nombre ?? "—";

        var documento = DocumentoProyecto.Crear(cmd.ProyectoId, cmd.CategoriaId, cmd.Titulo, cmd.Descripcion);
        documento.AgregarVersion(
            cmd.ArchivoNombre, cmd.ArchivoUrl, cmd.ArchivoTamano, cmd.Sha256, actor);

        ctx.ProyectoDocumentos.Add(documento);

        DocumentosProyecto.Auditar(ctx, cmd.ProyectoId,
            $"documento agregado: «{documento.Titulo}» ({cmd.ArchivoNombre})", actor);

        await ctx.SaveChangesAsync(ct);
        return documento.Id;
    }
}

// ── Subir una versión nueva ───────────────────────────────────────────────
/// <summary>
/// Agrega una versión a un documento que ya existe. La anterior no se toca.
/// </summary>
/// <param name="Notas">Qué cambió. Se pide en el formulario porque un historial de versiones sin
/// motivo no explica nada seis meses después.</param>
public sealed record SubirVersionDocumentoCommand(
    int     ProyectoId,
    int     DocumentoId,
    string  ArchivoNombre,
    string  ArchivoUrl,
    long    ArchivoTamano,
    string  Sha256,
    string? Notas) : IRequest<int>;

public sealed class SubirVersionDocumentoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<SubirVersionDocumentoCommand, int>
{
    public async Task<int> Handle(SubirVersionDocumentoCommand cmd, CancellationToken ct)
    {
        await DocumentosProyecto.ProyectoAbiertoAsync(ctx, cmd.ProyectoId, ct);

        // Con versiones: AgregarVersion numera a partir de las que ya hay, y con la colección sin
        // cargar volvería a empezar en 1 y chocaría contra el índice único.
        var documento = await DocumentosProyecto.DelProyectoAsync(
            ctx, cmd.ProyectoId, cmd.DocumentoId, conVersiones: true, ct);

        var actor   = currentUser.Nombre ?? "—";
        var version = documento.AgregarVersion(
            cmd.ArchivoNombre, cmd.ArchivoUrl, cmd.ArchivoTamano, cmd.Sha256, actor, cmd.Notas);

        DocumentosProyecto.Auditar(ctx, cmd.ProyectoId,
            $"«{documento.Titulo}»: versión {version.Numero} ({cmd.ArchivoNombre})", actor);

        await ctx.SaveChangesAsync(ct);
        return version.Numero;
    }
}

// ── Editar la ficha del documento ─────────────────────────────────────────
/// <summary>Cambia título, descripción o categoría. El archivo no se toca: para eso se sube una
/// versión.</summary>
public sealed record ActualizarDocumentoCommand(
    int     ProyectoId,
    int     DocumentoId,
    int     CategoriaId,
    string  Titulo,
    string? Descripcion) : IRequest<Unit>;

public sealed class ActualizarDocumentoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarDocumentoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarDocumentoCommand cmd, CancellationToken ct)
    {
        await DocumentosProyecto.ProyectoAbiertoAsync(ctx, cmd.ProyectoId, ct);

        var documento = await DocumentosProyecto.DelProyectoAsync(
            ctx, cmd.ProyectoId, cmd.DocumentoId, conVersiones: false, ct);

        // La categoría solo se exige activa si CAMBIA: un documento clasificado con una categoría
        // que después se desactivó se tiene que poder seguir editando, o corregirle una errata
        // obligaría a reclasificarlo.
        if (documento.CategoriaId != cmd.CategoriaId)
            await DocumentosProyecto.ExigirCategoriaActivaAsync(ctx, cmd.CategoriaId, ct);

        var tituloAnterior = documento.Titulo;
        var cambio = documento.Definir(cmd.Titulo, cmd.Descripcion);

        if (documento.CategoriaId != cmd.CategoriaId)
        {
            documento.CategoriaId = cmd.CategoriaId;
            cambio = true;
        }

        // Un guardado sin cambios no ensucia la auditoría, mismo criterio que la ficha.
        if (cambio)
            DocumentosProyecto.Auditar(ctx, cmd.ProyectoId,
                $"documento actualizado: «{tituloAnterior}»", currentUser.Nombre ?? "—");

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Dar de baja ───────────────────────────────────────────────────────────
/// <summary>
/// Borrado lógico del documento, con su historial.
///
/// <para>No se ofrece borrar una versión suelta: el historial es el valor del repositorio. Lo que
/// se archiva es el documento entero, y sigue en la base.</para>
/// </summary>
public sealed record EliminarDocumentoCommand(int ProyectoId, int DocumentoId) : IRequest<Unit>;

public sealed class EliminarDocumentoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<EliminarDocumentoCommand, Unit>
{
    public async Task<Unit> Handle(EliminarDocumentoCommand cmd, CancellationToken ct)
    {
        await DocumentosProyecto.ProyectoAbiertoAsync(ctx, cmd.ProyectoId, ct);

        var documento = await DocumentosProyecto.DelProyectoAsync(
            ctx, cmd.ProyectoId, cmd.DocumentoId, conVersiones: false, ct);

        documento.IsDeleted = true;

        DocumentosProyecto.Auditar(ctx, cmd.ProyectoId,
            $"documento archivado: «{documento.Titulo}»", currentUser.Nombre ?? "—");

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
