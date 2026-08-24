# Data Layer Documentation

## Overview

This folder documents the Entity Framework Core data layer for miniDriveBackend, a multi-tenant file storage application using PostgreSQL.

## Files

| File | Description |
|------|-------------|
| [database-schema.md](database-schema.md) | Complete PostgreSQL schema from `scripts/schema.sql` |
| [entity-models.md](entity-models.md) | C# entity classes mapping to each table |
| [dbcontext-configuration.md](dbcontext-configuration.md) | `AppDbContext` Fluent API configuration details |

## Quick Reference

### Entity → Table Mapping

| Entity | Table | Key Features |
|--------|-------|--------------|
| `Tenant` | `tenants` | Unified companies + personal accounts (`is_personal` flag) |
| `User` | `users` | Role enum, nullable `TenantId` for super_admin |
| `Folder` | `folders` | Flat structure (MVP), no nesting |
| `FileEntity` | `files` | Soft delete, S3 metadata, global query filter |

### Critical Conventions

- **All PKs**: UUID with `gen_random_uuid()` default
- **Timestamps**: `created_at`/`updated_at` with `now()` default; `updated_at` maintained by DB trigger
- **Columns**: snake_case in DB, PascalCase in C#
- **Enums**: Stored as TEXT via `HasConversion<string>()`
- **Soft delete**: `FileEntity.IsDeleted` filtered globally via `HasQueryFilter`
- **Multi-tenant**: DB triggers enforce isolation; app layer should also validate

### Registration

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
```

Connection string from `appsettings.json` → `DefaultConnection`.

### Build Status

✅ Compiles successfully (`dotnet build` passes)

### Next Steps

1. Install EF Core tools: `dotnet tool install --global dotnet-ef`
2. Create migration: `dotnet ef migrations add InitialCreate`
3. Apply to database: `dotnet ef database update`
4. Verify schema matches `scripts/schema.sql`