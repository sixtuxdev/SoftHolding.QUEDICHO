using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftHolding.QUEDICHO.Application.Persistence;
using SoftHolding.QUEDICHO.Infrastructure.Persistence;

namespace SoftHolding.QUEDICHO.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseOptions = new DatabaseOptions
        {
            FileName = configuration[$"{DatabaseOptions.SectionName}:FileName"] ?? "quedicho.db"
        };
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftHolding",
            "QUEDICHO",
            "Data");
        Directory.CreateDirectory(dataDirectory);
        var databasePath = Path.Combine(dataDirectory, databaseOptions.FileName);

        services.AddDbContextFactory<TranscriberDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath};Cache=Shared"));
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<DatabaseInitializer>();
        return services;
    }
}
