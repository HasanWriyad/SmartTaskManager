using SmartTaskManager.Application.Features.Users;

namespace SmartTaskManager.Application.Features.Auth;

public sealed record RegisterRequest(string Email, string FullName, string Password);

public sealed record LoginRequest(string Email, string Password);

/// <summary>Body returned by login and refresh. The refresh token is never in the body; it is set as a cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserDto User);

/// <summary>Result of login/refresh: the response body plus the refresh token the API layer puts in a cookie.</summary>
public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
