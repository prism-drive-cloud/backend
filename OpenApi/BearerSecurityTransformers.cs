using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace miniDriveBackend.OpenApi
{
    // Adds the Bearer (JWT) security scheme to the OpenAPI document's components,
    // but only if the existing JWT bearer authentication scheme is registered.
    internal sealed class BearerSecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
    {
        private readonly IAuthenticationSchemeProvider _schemeProvider;

        public BearerSecuritySchemeDocumentTransformer(IAuthenticationSchemeProvider schemeProvider)
        {
            _schemeProvider = schemeProvider;
        }

        public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            var schemes = await _schemeProvider.GetAllSchemesAsync();
            if (schemes.All(s => s.Name != JwtBearerDefaults.AuthenticationScheme))
                return;

            var bearerScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "JWT access token issued by /api/auth/login or /api/auth/refresh."
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = bearerScheme;
        }
    }

    // Applies the Bearer security requirement only to operations that require authorization
    // (i.e. have [Authorize] metadata and are not [AllowAnonymous]). Public endpoints such as
    // login, register-tenant and refresh remain callable without authentication.
    internal sealed class BearerSecurityRequirementOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
            var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();

            if (requiresAuthorization && !allowsAnonymous)
            {
                var reference = new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, context.Document);

                operation.Security ??= new List<OpenApiSecurityRequirement>();
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [reference] = new List<string>()
                });
            }

            return Task.CompletedTask;
        }
    }
}
