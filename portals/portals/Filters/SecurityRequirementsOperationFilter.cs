namespace portals.Filters;

using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

public class SecurityRequirementsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authAttributes = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>();
    
        var allowAnonymous = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<AllowAnonymousAttribute>().Any();

        if (authAttributes.Any() && !allowAnonymous)
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();

            var securityRequirement = new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>()
            };

            operation.Security.Add(securityRequirement);
        }
    }
}