using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Reuniones.Import;
using Diger.TramitesEstado.Infrastructure.Import;
using Diger.TramitesEstado.Infrastructure.Notifications;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using Diger.TramitesEstado.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diger.TramitesEstado.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork>(sp          => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IExpedienteRepository,    ExpedienteRepository>();
        services.AddScoped<IInstitucionRepository,   InstitucionRepository>();
        services.AddScoped<IAreaRepository,          AreaRepository>();
        services.AddScoped<IUnidadRepository,        UnidadRepository>();
        services.AddScoped<IContactoRepository,      ContactoRepository>();
        services.AddScoped<IReunionRepository,       ReunionRepository>();
        services.AddScoped<ITicketRepository,        TicketRepository>();
        services.AddScoped<IUsuarioRepository,       UsuarioRepository>();

        // Importación de reuniones desde el portal demo (Supabase)
        services.AddHttpClient<IReunionImportSource, SupabaseReunionImportSource>();

        // Notificaciones
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddHostedService<RecordatorioBackgroundService>();

        // Seguridad / identidad
        services.AddHttpContextAccessor();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.TryAddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
