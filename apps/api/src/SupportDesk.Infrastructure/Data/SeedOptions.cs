namespace SupportDesk.Infrastructure.Data;

/// <summary>
/// Settings for the development seeder, bound from the <c>Seed</c> section of configuration.
/// Nothing here is in source: set it locally with <c>dotnet user-secrets</c> or the
/// <c>Seed__AgentPassword</c> environment variable. See README.md.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// The password every seeded agent gets, hashed before it is stored. Left empty, seeded
    /// agents get no login.
    /// </summary>
    public string AgentPassword { get; set; } = string.Empty;
}
