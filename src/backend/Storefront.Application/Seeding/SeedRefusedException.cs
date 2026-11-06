namespace Storefront.Application.Seeding;

/// <summary>The seed command was refused before it changed anything (wrong environment or missing schema).</summary>
public sealed class SeedRefusedException(string message) : Exception(message);
