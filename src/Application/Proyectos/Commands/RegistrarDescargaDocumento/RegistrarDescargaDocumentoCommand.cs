namespace Diger.TramitesEstado.Application.Proyectos.Commands.RegistrarDescargaDocumento;

/// <summary>
/// Deja constancia de que alguien descargó una versión de documento.
///
/// <para><b>Registra, no autoriza.</b> Quien decide si la descarga procede es el
/// <c>[Permission("Proyectos.Documentos", Ver)]</c> del handler que sirve el archivo; este comando
/// corre después, cuando ya se resolvió que sí. Por eso no vuelve a comprobar permisos: hacerlo
/// daría la falsa impresión de que el control vive acá.</para>
///
/// <para><b>No revienta si la versión no existe.</b> El único efecto sería impedir una descarga que
/// el usuario ya tiene derecho a hacer —o peor, romperla a mitad de camino—, y eso es peor
/// resultado que una línea de bitácora que no se escribe. Devuelve <c>false</c> y sigue.</para>
/// </summary>
public sealed record RegistrarDescargaDocumentoCommand(int VersionId) : IRequest<bool>;

public sealed class RegistrarDescargaDocumentoCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService usuario)
    : IRequestHandler<RegistrarDescargaDocumentoCommand, bool>
{
    public async Task<bool> Handle(RegistrarDescargaDocumentoCommand cmd, CancellationToken ct)
    {
        if (usuario.UserId is not { } uid) return false;

        // La versión se busca con el filtro de alcance puesto: si el usuario no la alcanza, no hay
        // nada que registrar —y tampoco debería estar descargándola.
        var existe = await ctx.ProyectoDocumentoVersiones
            .AsNoTracking()
            .AnyAsync(v => v.Id == cmd.VersionId, ct);

        if (!existe) return false;

        ctx.ProyectoDocumentoDescargas.Add(
            DescargaDocumento.Registrar(cmd.VersionId, uid, usuario.Nombre));

        await ctx.SaveChangesAsync(ct);
        return true;
    }
}
