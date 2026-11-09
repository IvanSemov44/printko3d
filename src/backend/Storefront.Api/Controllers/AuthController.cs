using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Storefront.Application.Auth;

namespace Storefront.Api.Controllers;

/// <summary>Sign-in with an email and a password. The only address here that needs no sign-in is the sign-in itself.</summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAdminAuthService auth, IOptions<AdminLoginOptions> options) : ControllerBase
{
    /// <summary>Signs the administrator in and sets the session cookie. A wrong email, a wrong password and a locked account give the same answer.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AdminAuthSetup.LoginRateLimit)]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AdminUserDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var user = await auth.SignInAsync(request.Email, request.Password, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Sign-in failed",
                Detail = "The email or the password is wrong.",
                Extensions = { ["code"] = "auth.invalid_credentials" },
            });
        }

        return user;
    }

    /// <summary>Ends the session on the server.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<NoContentResult> Logout(CancellationToken cancellationToken)
    {
        await auth.SignOutAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>The signed-in administrator. The admin panel asks this instead of remembering the user itself.</summary>
    [HttpGet("me")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminUserDto>> Me(CancellationToken cancellationToken)
    {
        var user = await auth.GetCurrentAsync(cancellationToken);
        return user is null ? Unauthorized() : user;
    }
}

public sealed record LoginRequest(
    [Required, StringLength(256)] string Email,
    [Required, StringLength(256)] string Password);
