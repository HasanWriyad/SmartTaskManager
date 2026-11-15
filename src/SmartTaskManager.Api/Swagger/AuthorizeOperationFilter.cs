using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SmartTaskManager.Api.Swagger;

/// <summary>
/// Adds the bearer security requirement and 401/403 responses to operations that require authorization.
/// Every endpoint does, because of the fallback policy, unless it is marked [AllowAnonymous].
/// </summary>
internal sealed class AuthorizeOperationFilter : IOperationFilter
{
    public const string SchemeId = "Bearer";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        operation.Responses ??= [];
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Authentication is required." });
        if (metadata.OfType<IAuthorizeData>().Any(a => !string.IsNullOrEmpty(a.Policy) || !string.IsNullOrEmpty(a.Roles)))
        {
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "The caller's role is not permitted." });
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, context.Document)] = [],
        });
    }
}
