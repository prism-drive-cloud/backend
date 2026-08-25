using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using miniDriveBackend.Business.Configuration;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Business.Security;
using miniDriveBackend.Business.Services;

namespace miniDriveBackend.Business
{
    public static class BusinessRegistration
    {
        public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

            services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
            services.AddSingleton<ITokenService, TokenService>();

            services.AddHttpContextAccessor();
            services.AddScoped<ITenantContext, TenantContext>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITenantService, TenantService>();

            return services;
        }
    }
}
