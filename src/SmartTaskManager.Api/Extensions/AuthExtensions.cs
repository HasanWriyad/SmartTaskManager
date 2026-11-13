using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SmartTaskManager.Api.Auth;
using SmartTaskManager.Api.Authorization;
using SmartTaskManager.Domain.Enums;
using SmartTaskManager.Infrastructure.Auth;

namespace SmartTaskManager.Api.Extensions;

internal static class AuthExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the short JWT claim names ("sub", "role") instead of mapping them to long URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    // Empty key bytes fail fast via JwtOptions validation at startup, not here.
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey ?? string.Empty)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaimTypes.UserId,
                    RoleClaimType = AppClaimTypes.Role,
                };
                // Role changes and deactivation take effect immediately (see AccessTokenStateValidator).
                options.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenStateValidator.ValidateAsync };
            });

        return services;
    }

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        var anyUser = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireRole(RoleNames(UserRole.Admin, UserRole.ProjectManager, UserRole.TeamMember))
            .Build();

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AnyUser, anyUser)
            .AddPolicy(Policies.AdminOrProjectManager, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(RoleNames(UserRole.Admin, UserRole.ProjectManager)))
            .AddPolicy(Policies.AdminOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(RoleNames(UserRole.Admin)))
            // Secure by default: endpoints without [Authorize]/[AllowAnonymous] require a signed-in user.
            .SetDefaultPolicy(anyUser)
            .SetFallbackPolicy(anyUser);

        return services;
    }

    private static string[] RoleNames(params UserRole[] roles) => [.. roles.Select(r => r.ToString())];
}
