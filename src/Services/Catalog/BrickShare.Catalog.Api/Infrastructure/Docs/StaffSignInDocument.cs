using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BrickShare.Catalog.Api.Infrastructure.Docs;

internal static class StaffSignInDocument
{
    public const string SchemeName = "oauth2";

    public static string ApiScope(string audience) => $"api://{audience}/api.access";

    public static Task Apply(OpenApiDocument document, string authority, string audience)
    {
        var endpoints = authority.Replace("/v2.0", "/oauth2/v2.0", StringComparison.Ordinal);
        var scope = ApiScope(audience);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"{endpoints}/authorize"),
                    TokenUrl = new Uri($"{endpoints}/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        [scope] = "Use the BrickShare API as the signed-in staff member."
                    }
                }
            }
        };

        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName)] = [scope]
            }
        ];

        document.SetReferenceHostDocument();

        return Task.CompletedTask;
    }
}