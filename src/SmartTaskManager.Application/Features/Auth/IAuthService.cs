using SmartTaskManager.Application.Features.Users;

namespace SmartTaskManager.Application.Features.Auth;

public interface IAuthService
{
    /// <summary>Creates a new account. Public registration always creates a TeamMember.</summary>
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>Verifies credentials and starts a new session (refresh token family).</summary>
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Rotates the refresh token and issues a new access token.</summary>
    Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    /// <summary>Ends the session by revoking the token's family. Unknown or missing tokens are ignored.</summary>
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
}
