using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Applies pending migrations and seeds the demo data, each only when
    /// <see cref="DatabaseOptions"/> switches it on. A convenience for local runs and the
    /// container - a real deployment would run migrations as a separate, deliberate step.
    /// </summary>
    public static async Task PrepareDatabaseAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (!options.MigrateOnStartup && !options.SeedDemoData)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();

        if (options.MigrateOnStartup)
        {
            app.Logger.LogInformation("Applying pending database migrations.");
            await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.MigrateAsync();
        }

        if (options.SeedDemoData)
        {
            await scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
        }
    }
}
