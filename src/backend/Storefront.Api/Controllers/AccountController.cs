using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Storefront.Api.Auth;
using Storefront.Api.Requests;
using Storefront.Application.Customers;
using Storefront.Application.Exceptions;
using Storefront.Infrastructure.Identity;

namespace Storefront.Api.Controllers;

/// <summary>
/// The account of a buyer (decision 0039), with its own cookie `__Host-printko-customer`. It is not the administrator's sign-in:
/// the one does not open the addresses of the other. Changes need the `X-Requested-With` header (403 csrf.header_required without it).
/// </summary>
[ApiController]
[Route("api/v1/account")]
public sealed class AccountController(ICustomerAccountService accounts) : ControllerBase
{
    /// <summary>
    /// Makes an account. It does not sign the buyer in. An address that already has an account gets the same answer, so the call does
    /// not tell who is registered.
    /// </summary>
    /// <remarks>Errors: 400 validation.failed (a field is wrong or the password is too short), 403 csrf.header_required, 429.</remarks>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(CustomerAuthSetup.RateLimit)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Register(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        await accounts.RegisterAsync(request.Email, request.Password, request.Name, cancellationToken);
        return Accepted();
    }

    /// <summary>Confirms the address with the two values of the link in the email.</summary>
    /// <remarks>Errors: 400 validation.failed, 403 csrf.header_required, 422 account.token_invalid (wrong, expired or for another address), 429.</remarks>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(CustomerAuthSetup.RateLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        await accounts.ConfirmEmailAsync(request.Email, request.Token, cancellationToken);
        return NoContent();
    }

    /// <summary>Sends the confirmation email again to the signed-in buyer. Nothing is sent when the address is already confirmed.</summary>
    /// <remarks>Errors: 401 when there is no session, 403 csrf.header_required.</remarks>
    [HttpPost("resend-confirmation")]
    [Authorize(AuthenticationSchemes = CustomerAuthentication.Scheme)]
    [EnableRateLimiting(CustomerAuthSetup.RateLimit)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ResendConfirmation(CancellationToken cancellationToken)
    {
        await accounts.ResendConfirmationAsync(cancellationToken);
        return Accepted();
    }

    /// <summary>Sends the link for a new password. The answer is the same for an address that has an account and one that has not.</summary>
    /// <remarks>Errors: 400 validation.failed, 403 csrf.header_required, 429.</remarks>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(CustomerAuthSetup.RateLimit)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await accounts.ForgotPasswordAsync(request.Email, cancellationToken);
        return Accepted();
    }

    /// <summary>
    /// Sets a new password with the two values of the link in the email. Every session ends, a lockout is lifted and the address is confirmed.
    /// The link works once. It does not sign the buyer in.
    /// </summary>
    /// <remarks>Errors: 400 validation.failed (the password is not allowed), 403 csrf.header_required, 422 account.token_invalid, 429.</remarks>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(CustomerAuthSetup.RateLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await accounts.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken);
        return NoContent();
    }

    /// <summary>Signs the buyer in and sets the cookie. A wrong email, a wrong password and a locked account give the same answer.</summary>
    /// <remarks>Errors: 400 validation.failed, 401 auth.invalid_credentials, 403 csrf.header_required, 429.</remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(CustomerAuthSetup.RateLimit)]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CustomerDto>> Login(CustomerLoginRequest request, CancellationToken cancellationToken) =>
        Ok(await accounts.SignInAsync(request.Email, request.Password, cancellationToken)
            ?? throw new InvalidCredentialsException());

    /// <summary>Ends the session on the server.</summary>
    /// <remarks>Errors: 401 when there is no session, 403 csrf.header_required.</remarks>
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = CustomerAuthentication.Scheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        await accounts.SignOutAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>The signed-in buyer. The shop asks this instead of remembering the buyer itself.</summary>
    /// <remarks>Errors: 401 when there is no session.</remarks>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = CustomerAuthentication.Scheme)]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerDto>> Me(CancellationToken cancellationToken)
    {
        var customer = await accounts.GetCurrentAsync(cancellationToken);
        return customer is null ? Unauthorized() : Ok(customer);
    }

    /// <summary>Sets a new password after the current one. The other sessions end; this one stays.</summary>
    /// <remarks>
    /// Errors: 400 validation.failed (the new password is not allowed), 401 when there is no session, 403 csrf.header_required,
    /// 422 account.current_password_wrong.
    /// </remarks>
    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = CustomerAuthentication.Scheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangePassword(ChangeCustomerPasswordRequest request, CancellationToken cancellationToken)
    {
        await accounts.ChangePasswordAsync(request.CurrentPassword, request.NewPassword, cancellationToken);
        return NoContent();
    }
}
