using Diger.TramitesEstado.Application.AI;
using Diger.TramitesEstado.Application.Chat;
using Diger.TramitesEstado.Application.Informes;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Reuniones.Import;
using Diger.TramitesEstado.Infrastructure.AI;
using Diger.TramitesEstado.Infrastructure.Chat;
using Diger.TramitesEstado.Infrastructure.Import;
using Diger.TramitesEstado.Infrastructure.Notifications;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using Diger.TramitesEstado.Infrastructure.Reports;
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

        // Informes (PDF + Excel)
        services.AddScoped<IInformeService, InformeService>();

        // Notificaciones
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddHostedService<RecordatorioBackgroundService>();

        // Chat de soporte
        services.AddScoped<IChatService, ChatService>();

        // Agente IA (asistente virtual en cola de chat)
        services.Configure<AgenteOptions>(configuration.GetSection("Ai"));
        services.AddHttpClient<IAgenteService, AgenteService>(client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com");
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Seguridad / identidad
        services.AddHttpContextAccessor();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.TryAddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
