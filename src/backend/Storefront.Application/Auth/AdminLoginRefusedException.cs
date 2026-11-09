namespace Storefront.Application.Auth;

/// <summary>Administrator login (or creating an administrator) was refused because of the environment.</summary>
public sealed class AdminLoginRefusedException(string message) : Exception(message);
