using Application.Interfaces;
using Application.Services;
using Infrastructure.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Services;
using Infrastructure.SignalR.Abstractions;
using Infrastructure.SignalR.Services;
using Infrastructure.Interfaces;
using Infrastructure.Configuration;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BattleTanksDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(BattleTanksDbContext).Assembly.FullName)));

        // Obtener la cadena de conexión de Redis (si está configurada) antes de registrar los servicios
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration.GetSection("RedisOptions").GetValue<string>("ConnectionString");

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IPlayerRepository, EfPlayerRepository>();
        services.AddScoped<IGameSessionRepository, EfGameSessionRepository>();
        services.AddScoped<IScoreRepository, EfScoreRepository>();
        services.AddScoped<IChatRepository, EfChatRepository>();

        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IJwtService, JwtService>();

        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });

        // Condicionalmente registrar el rastreador de conexiones. Si se configura Redis, use
        // RedisConnectionTracker; de lo contrario, mantenga la versión en memoria. Esto permite
        // escalar a múltiples instancias compartiendo el estado de conexiones.
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<IConnectionTracker, RedisConnectionTracker>();
        }
        else
        {
            services.AddSingleton<IConnectionTracker, InMemoryConnectionTracker>();
        }

        services.AddSingleton<IRoomRegistry, InMemoryRoomRegistry>();
        services.AddSingleton<IGameNotificationService, NoOpNotificationService>();

        // MQTT and Redis configuration
        services.Configure<MqttOptions>(configuration.GetSection("Mqtt"));
        services.Configure<RedisOptions>(configuration.GetSection("RedisOptions"));

        // Registrar un único multiplexor de conexión Redis utilizando la cadena ya calculada.
        // Esto se realiza después de evaluar la cadena para el rastreador de conexiones.
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));
        }

        services.AddSingleton<IMqttService, MqttService>();
        services.AddSingleton<IEventHistoryService, EventHistoryService>();

        return services;
    }
}
