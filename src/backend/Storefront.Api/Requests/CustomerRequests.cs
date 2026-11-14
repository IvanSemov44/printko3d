using System.ComponentModel.DataAnnotations;

namespace Storefront.Api.Requests;

/// <summary>The body of the registration of a buyer.</summary>
public sealed record RegisterCustomerRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(256)] string Password,
    [Required, StringLength(200)] string Name);

/// <summary>The body of the sign-in of a buyer.</summary>
public sealed record CustomerLoginRequest(
    [Required, StringLength(254)] string Email,
    [Required, StringLength(256)] string Password);

/// <summary>The body of a change of the password of the signed-in buyer.</summary>
public sealed record ChangeCustomerPasswordRequest(
    [Required, StringLength(256)] string CurrentPassword,
    [Required, StringLength(256)] string NewPassword);
