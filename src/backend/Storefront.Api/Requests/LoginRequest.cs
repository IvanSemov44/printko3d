using System.ComponentModel.DataAnnotations;

namespace Storefront.Api.Requests;

/// <summary>The body of the sign-in request.</summary>
public sealed record LoginRequest(
    [Required, StringLength(256)] string Email,
    [Required, StringLength(256)] string Password);
