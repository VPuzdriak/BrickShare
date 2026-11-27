using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Scalar.AspNetCore;

namespace BrickShare.Catalog.Api.Infrastructure.Docs;

public static class ApiDocsExtensions
{
    public static IServiceCollection AddApiDocs(this IServiceCollection services, IConfiguration configuration)
    {
        var (authority, audience) = ReadStaffSettings(configuration);

        return services.AddOpenApi(options =>
            options.AddDocumentTransformer((document, _, _) =>
                StaffSignInDocument.Apply(document, authority, audience)));
    }

    public static WebApplication MapApiDocs(this WebApplication app)
    {
        app.MapOpenApi();

        if (app.Environment.IsProduction())
        {
            return app;
        }

        var (_, audience) = ReadStaffSettings(app.Configuration);
        var clientId = app.Configuration["Scalar:StaffClientId"]
            ?? throw new InvalidOperationException("The setting 'Scalar:StaffClientId' is missing.");

        app.MapScalarApiReference(options => options
            .AddPreferredSecuritySchemes(StaffSignInDocument.SchemeName)
            .AddAuthorizationCodeFlow(StaffSignInDocument.SchemeName, flow =>
            {
                flow.ClientId = clientId;
                flow.Pkce = Pkce.Sha256;
                flow.SelectedScopes = [StaffSignInDocument.ApiScope(audience)];
            }));

        return app;
    }

    private static (string Authority, string Audience) ReadStaffSettings(IConfiguration configuration)
    {
        var authority = configuration["Authentication:Staff:Authority"]
            ?? throw new InvalidOperationException("The setting 'Authentication:Staff:Authority' is missing.");
        var audience = configuration["Authentication:Staff:Audience"]
            ?? throw new InvalidOperationException("The setting 'Authentication:Staff:Audience' is missing.");

        return (authority, audience);
    }
}