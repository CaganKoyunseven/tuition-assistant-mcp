using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TuitionAssistant.Api.Infrastructure;

/// <summary>Swagger/OpenAPI document (v1) with XML comments and the JWT bearer security scheme.</summary>
public static class SwaggerSetup
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddTuitionSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Tuition API",
                Version = "v1",
                Description = "University tuition REST API (SE 4458 Group 1). All public routes are served through the Kong API gateway. "
                    + "Get a token from POST /api/v1/auth/token, then click Authorize and paste it.",
            });

            var xmlFile = Path.Combine(AppContext.BaseDirectory, "TuitionAssistant.Api.xml");
            if (File.Exists(xmlFile))
            {
                options.IncludeXmlComments(xmlFile);
            }

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "RS256 JWT from POST /api/v1/auth/token (paste the token only, without 'Bearer ').",
            });
            options.OperationFilter<AuthorizeOperationFilter>();
        });
        return services;
    }

    public static WebApplication UseTuitionSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tuition API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Tuition API";
        });
        return app;
    }

    /// <summary>Marks operations that require a token (and only those) with the bearer security requirement.</summary>
    private sealed class AuthorizeOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
            var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
            if (!requiresAuth)
            {
                return;
            }

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
            });
        }
    }
}
