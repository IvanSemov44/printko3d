namespace Storefront.Application.Auth;

/// <summary>The signed-in administrator, as the admin panel sees them.</summary>
public sealed record AdminUserDto(string Email);
