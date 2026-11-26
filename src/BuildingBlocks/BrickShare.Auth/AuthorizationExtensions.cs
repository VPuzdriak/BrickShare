using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Auth;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddBrickShareAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole("Admin"))
            .AddPolicy(Policies.EmployeeOrAdmin, policy => policy.RequireRole("Employee", "Admin"));

        return services;
    }
}