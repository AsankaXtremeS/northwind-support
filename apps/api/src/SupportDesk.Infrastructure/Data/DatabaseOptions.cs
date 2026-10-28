namespace SupportDesk.Infrastructure.Data;

/// <summary>
/// What the API does to its database when it starts, bound from the <c>Database</c> section of
/// configuration. Both are off unless switched on: appsettings.Development.json turns them on for
/// local runs, and docker-compose.yml for the container.
/// </summary>
/// <remarks>
/// Kept apart from the environment name on purpose. A real deployment runs migrations as its
/// own step (a migration bundle or an idempotent script) and never seeds demo data, whatever
/// the environment is called. See README.md.
/// </remarks>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Applies pending EF Core migrations at startup, creating the database if needed.</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Seeds the demo data at startup, unless tickets already exist.</summary>
    public bool SeedDemoData { get; set; }
}
