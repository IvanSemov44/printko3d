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

/// <summary>The body of the confirmation of an address: the two values of the link in the email.</summary>
public sealed record ConfirmEmailRequest(
    [Required, StringLength(254)] string Email,
    [Required, StringLength(2048)] string Token);

/// <summary>The body of the request for a new password.</summary>
public sealed record ForgotPasswordRequest([Required, StringLength(254)] string Email);

/// <summary>The body of the new password: the two values of the link in the email and the password.</summary>
public sealed record ResetPasswordRequest(
    [Required, StringLength(254)] string Email,
    [Required, StringLength(2048)] string Token,
    [Required, StringLength(256)] string NewPassword);

/// <summary>The body of a change of the password of the signed-in buyer.</summary>
public sealed record ChangeCustomerPasswordRequest(
    [Required, StringLength(256)] string CurrentPassword,
    [Required, StringLength(256)] string NewPassword);
