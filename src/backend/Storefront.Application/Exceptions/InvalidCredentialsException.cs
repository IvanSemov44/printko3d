namespace Storefront.Application.Exceptions;

/// <summary>
/// A wrong email, a wrong password or a locked account. It never says which one, on purpose,
/// so the answer does not help someone who is guessing.
/// </summary>
public sealed class InvalidCredentialsException() : Exception("The email or the password is wrong.");
