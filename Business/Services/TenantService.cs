using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using miniDriveBackend.Business.DTOs;
using miniDriveBackend.Business.Exceptions;
using miniDriveBackend.Business.Interfaces;
using miniDriveBackend.Data.Interfaces;
using miniDriveBackend.Models;

namespace miniDriveBackend.Business.Services
{
    public class TenantService : ITenantService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly ILogger<TenantService> _logger;

        public TenantService(ITenantRepository tenantRepository, ILogger<TenantService> logger)
        {
            _tenantRepository = tenantRepository;
            _logger = logger;
        }

        public async Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Tenant name is required.", nameof(request));

            if (request.StorageQuotaBytes <= 0)
                throw new ArgumentException("Storage quota must be greater than zero.", nameof(request));

            var slug = SlugNormalizer.Normalize(request.Slug);
            if (!SlugNormalizer.IsValid(slug))
                throw new ArgumentException("Slug must contain only lowercase letters, numbers and hyphens.", nameof(request));

            if (await _tenantRepository.ExistsBySlugAsync(slug, cancellationToken))
                throw new DuplicateResourceException("Tenant", "slug", slug);

            var tenant = await _tenantRepository.CreateAsync(new Tenant
            {
                Name = request.Name.Trim(),
                Slug = slug,
                IsPersonal = request.IsPersonal,
                StorageQuotaBytes = request.StorageQuotaBytes
            }, cancellationToken);

            _logger.LogInformation("Tenant created {TenantId} ({Slug})", tenant.Id, tenant.Slug);
            return MapToResponse(tenant);
        }

        public async Task<TenantResponse> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
                ?? throw new TenantNotFoundException(tenantId);
            return MapToResponse(tenant);
        }

        public async Task<TenantResponse> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            var normalized = SlugNormalizer.Normalize(slug);
            var tenant = await _tenantRepository.GetBySlugAsync(normalized, cancellationToken)
                ?? throw new TenantNotFoundException(normalized);
            return MapToResponse(tenant);
        }

        public async Task<TenantUsageResponse> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
                ?? throw new TenantNotFoundException(tenantId);

            var summary = await _tenantRepository.GetUsageSummaryAsync(tenantId, cancellationToken);

            var quota = tenant.StorageQuotaBytes;
            var used = summary.UsedBytes;
            var available = Math.Max(0L, quota - used);
            var percentage = quota > 0 ? Math.Round((double)used / quota * 100d, 2) : 0d;

            return new TenantUsageResponse(
                tenant.Id,
                tenant.Name,
                used,
                quota,
                available,
                percentage,
                summary.FileCount,
                summary.FolderCount);
        }

        public async Task<bool> ValidateQuotaAsync(Guid tenantId, long requestedBytes, CancellationToken cancellationToken = default)
        {
            if (requestedBytes < 0)
                throw new ArgumentException("Requested bytes cannot be negative.", nameof(requestedBytes));

            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
                ?? throw new TenantNotFoundException(tenantId);

            var used = await _tenantRepository.GetUsageAsync(tenantId, cancellationToken);
            var available = Math.Max(0L, tenant.StorageQuotaBytes - used);

            if (used + requestedBytes > tenant.StorageQuotaBytes)
                throw new QuotaExceededException(requestedBytes, available, tenant.StorageQuotaBytes);

            return true;
        }

        public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            return await _tenantRepository.ExistsBySlugAsync(SlugNormalizer.Normalize(slug), cancellationToken);
        }

        private static TenantResponse MapToResponse(Tenant tenant) =>
            new(tenant.Id, tenant.Name, tenant.Slug, tenant.IsPersonal, tenant.StorageQuotaBytes, tenant.CreatedAt);
    }
}
