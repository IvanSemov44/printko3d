using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Storefront.Api.Auth;
using Storefront.Api.Requests;
using Storefront.Application.Auth;
using Storefront.Application.Exceptions;

namespace Storefront.Api.Controllers;

/// <summary>Sign-in with an email and a password. The only address here that needs no sign-in is the sign-in itself.</summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAdminAuthService auth, IOptions<AdminLoginOptions> options) : ControllerBase
{
    /// <summary>Signs the administrator in and sets the session cookie. A wrong email, a wrong password and a locked account give the same answer.</summary>
    /// <remarks>Errors: 400 validation.failed, 401 auth.invalid_credentials, 404 when the login is turned off, 429 when there are too many attempts.</remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AdminAuthSetup.LoginRateLimit)]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AdminUserDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var user = await auth.SignInAsync(request.Email, request.Password, cancellationToken)
            ?? throw new InvalidCredentialsException();
        return Ok(user);
    }

    /// <summary>Ends the session on the server.</summary>
    /// <remarks>Errors: 401 when there is no session.</remarks>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>The signed-in administrator. The admin panel asks this instead of remembering the user itself.</summary>
    /// <remarks>Errors: 401 when there is no session, 403 when the account has no Admin role.</remarks>
    [HttpGet("me")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminUserDto>> Me(CancellationToken cancellationToken)
    {
        var user = await auth.GetCurrentAsync(cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }
}
