namespace Diger.TramitesEstado.Infrastructure.Persistence.Repositories;

public sealed class ExpedienteRepository(AppDbContext ctx) : IExpedienteRepository
{
    public Task<Expediente?> GetByIdAsync(int id, CancellationToken ct = default) =>
        ctx.Expedientes.FindAsync([id], ct).AsTask();

    public Task<Expediente?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default) =>
        ctx.Expedientes
            .Include(e => e.Tramites)
            .Include(e => e.Requisitos)
            .Include(e => e.Flujos)
            .Include(e => e.Legal)
            .Include(e => e.DocsSolicitados)
            .Include(e => e.DocsInternos)
            .Include(e => e.Perfiles)
            .Include(e => e.Condiciones)
            .Include(e => e.ChecklistInfra)
            .Include(e => e.Secciones)
            .AsSplitQuery()  // evita la explosión cartesiana de 10 colecciones (una query por colección)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Expediente>> GetAllAsync(CancellationToken ct = default) =>
        await ctx.Expedientes.AsNoTracking().ToListAsync(ct);

    // Unicidad/secuencia del código: independiente del alcance del usuario.
    public Task<int> CountByInstitucionPrefixAsync(string prefijo, CancellationToken ct = default) =>
        ctx.Expedientes.IgnoreQueryFilters().CountAsync(e => e.Codigo.StartsWith(prefijo), ct);

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct = default) =>
        ctx.Expedientes.IgnoreQueryFilters().AnyAsync(e => e.Codigo == codigo, ct);

    public async Task<HashSet<string>> GetOrigenExternoIdsAsync(CancellationToken ct = default) =>
        (await ctx.Expedientes.IgnoreQueryFilters()
            .Where(e => e.OrigenExternoId != null)
            .Select(e => e.OrigenExternoId!)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task AddAsync(Expediente expediente, CancellationToken ct = default) =>
        await ctx.Expedientes.AddAsync(expediente, ct);

    public void Update(Expediente expediente) => ctx.Expedientes.Update(expediente);
    public void Delete(Expediente expediente) => ctx.Expedientes.Remove(expediente);
}

public sealed class InstitucionRepository(AppDbContext ctx) : IInstitucionRepository
{
    public async Task<IReadOnlyList<Institucion>> GetAllActivasAsync(CancellationToken ct = default) =>
        await ctx.Instituciones
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Institucion>> GetAllAsync(CancellationToken ct = default) =>
        await ctx.Instituciones
            .OrderBy(i => i.Nombre)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task<Institucion?> GetByIdAsync(int id, CancellationToken ct = default) =>
        ctx.Instituciones.FindAsync([id], ct).AsTask();

    public Task<Institucion?> GetByIdWithTramitesAsync(int id, CancellationToken ct = default) =>
        ctx.Instituciones
            .Include(i => i.Tramites)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<Institucion?> GetByNombreAsync(string nombre, CancellationToken ct = default)
    {
        var norm = nombre.Trim().ToUpper();
        return ctx.Instituciones.FirstOrDefaultAsync(i => i.Nombre == norm, ct);
    }

    public async Task<IReadOnlyList<TramiteDefinicion>> GetTramitesAsync(int institucionId, CancellationToken ct = default) =>
        await ctx.TramitesDefinicion
            .Where(t => t.InstitucionId == institucionId)
            .OrderBy(t => t.Orden)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TramiteDefinicion>> GetAllTramitesAsync(CancellationToken ct = default) =>
        await ctx.TramitesDefinicion
            .OrderBy(t => t.InstitucionId).ThenBy(t => t.Orden)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task<bool> ExisteNombreAsync(string nombre, int? exceptoId = null, CancellationToken ct = default)
    {
        var norm = nombre.Trim().ToUpper();
        return ctx.Instituciones.AnyAsync(
            i => i.Nombre == norm && (exceptoId == null || i.Id != exceptoId), ct);
    }

    public Task<bool> TieneExpedientesAsync(int institucionId, CancellationToken ct = default) =>
        ctx.Expedientes.AnyAsync(e => e.InstitucionId == institucionId, ct);

    public async Task AddAsync(Institucion institucion, CancellationToken ct = default) =>
        await ctx.Instituciones.AddAsync(institucion, ct);

    public void Update(Institucion institucion) => ctx.Instituciones.Update(institucion);
    public void Delete(Institucion institucion) => ctx.Instituciones.Remove(institucion);
}

public sealed class ContactoRepository(AppDbContext ctx) : IContactoRepository
{
    public Task<Contacto?> GetByIdAsync(int id, CancellationToken ct = default) =>
        ctx.Contactos.FindAsync([id], ct).AsTask();

    public Task<bool> ExisteCorreoAsync(string correo, CancellationToken ct = default)
    {
        var norm = correo.Trim().ToLower();
        return ctx.Contactos.IgnoreQueryFilters().AnyAsync(c => c.Correo == norm, ct);
    }

    public async Task AddAsync(Contacto contacto, CancellationToken ct = default) =>
        await ctx.Contactos.AddAsync(contacto, ct);

    public void Update(Contacto contacto) => ctx.Contactos.Update(contacto);
    public void Delete(Contacto contacto) => ctx.Contactos.Remove(contacto);
}

public sealed class ReunionRepository(AppDbContext ctx) : IReunionRepository
{
    public Task<Reunion?> GetByIdAsync(int id, CancellationToken ct = default) =>
        ctx.Reuniones.FindAsync([id], ct).AsTask();

    public Task<Reunion?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default) =>
        ctx.Reuniones
            .Include(r => r.Asistentes)
            .Include(r => r.Acuerdos)
            .AsSplitQuery()  // evita el producto cartesiano Asistentes × Acuerdos
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Reunion?> GetByTokenWithAsistentesAsync(Guid token, CancellationToken ct = default) =>
        ctx.Reuniones
            .IgnoreQueryFilters()           // el auto-registro es anónimo (sin alcance institucional)
            .Include(r => r.Asistentes)
            .FirstOrDefaultAsync(r => r.RegistroToken == token, ct);

    public async Task<HashSet<string>> GetOrigenExternoIdsAsync(CancellationToken ct = default) =>
        (await ctx.Reuniones.IgnoreQueryFilters()
            .Where(r => r.OrigenExternoId != null)
            .Select(r => r.OrigenExternoId!)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task AddAsync(Reunion reunion, CancellationToken ct = default) =>
        await ctx.Reuniones.AddAsync(reunion, ct);

    public void Update(Reunion reunion) => ctx.Reuniones.Update(reunion);
    public void Delete(Reunion reunion) => ctx.Reuniones.Remove(reunion);
}

public sealed class TicketRepository(AppDbContext ctx) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default) =>
        ctx.Tickets.FindAsync([id], ct).AsTask();

    public Task<Ticket?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default) =>
        ctx.Tickets
            .Include(t => t.Comentarios)
            .Include(t => t.Tramites)
            .Include(t => t.Adjuntos)
            .Include(t => t.TemaRef)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    // Unicidad del número: debe ver TODOS los tickets, sin importar el alcance del usuario.
    public Task<int> CountByNumeroPrefixAsync(string prefijo, CancellationToken ct = default) =>
        ctx.Tickets.IgnoreQueryFilters().CountAsync(t => t.Numero.StartsWith(prefijo), ct);

    public Task<bool> NumeroExisteAsync(string numero, CancellationToken ct = default) =>
        ctx.Tickets.IgnoreQueryFilters().AnyAsync(t => t.Numero == numero, ct);

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default) =>
        await ctx.Tickets.AddAsync(ticket, ct);

    public void Update(Ticket ticket) => ctx.Tickets.Update(ticket);
    public void Delete(Ticket ticket) => ctx.Tickets.Remove(ticket);
}

public sealed class UsuarioRepository(AppDbContext ctx) : IUsuarioRepository
{
    public Task<Usuario?> GetByIdAsync(int id, CancellationToken ct = default) =>
        ctx.Usuarios.FindAsync([id], ct).AsTask();

    public Task<Usuario?> GetByCorreoAsync(string correo, CancellationToken ct = default) =>
        ctx.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo, ct);

    public async Task<IReadOnlyList<Usuario>> GetByRolAsync(
        RolUsuario rol, bool soloActivos = true, CancellationToken ct = default) =>
        await ctx.Usuarios
            .Where(u => u.Rol == rol && (!soloActivos || u.Activo))
            .OrderBy(u => u.Nombre)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Usuario>> GetAllAsync(CancellationToken ct = default) =>
        await ctx.Usuarios.OrderBy(u => u.Nombre).AsNoTracking().ToListAsync(ct);

    public Task<bool> ExisteCorreoAsync(string correo, int? exceptoId = null, CancellationToken ct = default)
    {
        var norm = correo.Trim().ToLowerInvariant();
        return ctx.Usuarios.AnyAsync(u => u.Correo == norm && (exceptoId == null || u.Id != exceptoId), ct);
    }

    public async Task AddAsync(Usuario usuario, CancellationToken ct = default) =>
        await ctx.Usuarios.AddAsync(usuario, ct);

    public void Update(Usuario usuario) => ctx.Usuarios.Update(usuario);

    public async Task<IReadOnlyList<int>> GetInstitucionIdsAsync(int usuarioId, CancellationToken ct = default) =>
        await ctx.UsuarioInstituciones
            .Where(x => x.UsuarioId == usuarioId)
            .Select(x => x.InstitucionId)
            .ToListAsync(ct);

    public async Task ReemplazarInstitucionesAsync(int usuarioId, IEnumerable<int> institucionIds, CancellationToken ct = default)
    {
        var actuales = await ctx.UsuarioInstituciones.Where(x => x.UsuarioId == usuarioId).ToListAsync(ct);
        ctx.UsuarioInstituciones.RemoveRange(actuales);
        var nuevos = institucionIds.Distinct().Select(id => UsuarioInstitucion.Crear(usuarioId, id));
        await ctx.UsuarioInstituciones.AddRangeAsync(nuevos, ct);
    }

    public async Task<IReadOnlyList<int>> GetTemaIdsAsync(int usuarioId, CancellationToken ct = default) =>
        await ctx.UsuarioTemas
            .Where(x => x.UsuarioId == usuarioId)
            .Select(x => x.TemaId)
            .ToListAsync(ct);

    public async Task ReemplazarTemasAsync(int usuarioId, IEnumerable<int> temaIds, CancellationToken ct = default)
    {
        var actuales = await ctx.UsuarioTemas.Where(x => x.UsuarioId == usuarioId).ToListAsync(ct);
        ctx.UsuarioTemas.RemoveRange(actuales);
        var nuevos = temaIds.Distinct().Select(id => UsuarioTema.Crear(usuarioId, id));
        await ctx.UsuarioTemas.AddRangeAsync(nuevos, ct);
    }
}
