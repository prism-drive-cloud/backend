using Microsoft.Extensions.DependencyInjection;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Data.Repositories;

namespace miniDriveBackend.Data
{
    public static class DAORegistration
    {
        public static IServiceCollection AddDataAccess(this IServiceCollection services)
        {
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IFolderRepository, FolderRepository>();
            services.AddScoped<IFileRepository, FileRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            return services;
        }
    }
}