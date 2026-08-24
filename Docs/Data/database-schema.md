# Database Schema Overview

Source: `scripts/schema.sql` (PostgreSQL 15+)

## Tables

### tenants
| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK, DEFAULT gen_random_uuid() |
| name | TEXT | NOT NULL |
| slug | TEXT | NOT NULL, UNIQUE |
| is_personal | BOOLEAN | NOT NULL, DEFAULT false |
| storage_quota_bytes | BIGINT | NOT NULL, DEFAULT 1073741824 (1 GB), CHECK > 0 |
| created_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| updated_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |

**Indexes**: `uq_tenants_slug` (unique on slug)
**Trigger**: `trg_tenants_updated_at` (auto-updates updated_at)

---

### users
| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK, DEFAULT gen_random_uuid() |
| tenant_id | UUID | FK → tenants(id) ON DELETE CASCADE, NULL allowed |
| email | TEXT | NOT NULL, UNIQUE |
| password_hash | TEXT | NOT NULL |
| full_name | TEXT | NOT NULL |
| role | TEXT | NOT NULL, CHECK IN ('super_admin', 'tenant_admin', 'user') |
| is_active | BOOLEAN | NOT NULL, DEFAULT true |
| created_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| updated_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |

**Constraints**:
- `uq_users_email` (unique email)
- `chk_users_role` (valid role)
- `chk_users_super_admin_no_tenant`: super_admin must have NULL tenant_id, others must have tenant_id

**Indexes**: `idx_users_tenant_id`
**Trigger**: `trg_users_updated_at`

---

### folders (MVP: flat structure, no nesting)
| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK, DEFAULT gen_random_uuid() |
| tenant_id | UUID | NOT NULL, FK → tenants(id) ON DELETE CASCADE |
| owner_id | UUID | NOT NULL, FK → users(id) ON DELETE RESTRICT |
| name | TEXT | NOT NULL |
| created_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| updated_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |

**Indexes**: `idx_folders_tenant_id`, `idx_folders_owner_id`
**Trigger**: `trg_folders_updated_at`
**Trigger**: `trg_folders_validate_tenant` (validates owner belongs to same tenant)

---

### files (metadata only, binary in S3)
| Column | Type | Constraints |
|--------|------|-------------|
| id | UUID | PK, DEFAULT gen_random_uuid() |
| tenant_id | UUID | NOT NULL, FK → tenants(id) ON DELETE CASCADE |
| owner_id | UUID | NOT NULL, FK → users(id) ON DELETE RESTRICT |
| folder_id | UUID | NULL, FK → folders(id) ON DELETE SET NULL |
| original_name | TEXT | NOT NULL |
| mime_type | TEXT | NOT NULL |
| size_bytes | BIGINT | NOT NULL, CHECK >= 0 |
| s3_key | TEXT | NOT NULL, UNIQUE |
| is_deleted | BOOLEAN | NOT NULL, DEFAULT false |
| deleted_at | TIMESTAMPTZ | NULL |
| created_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |
| updated_at | TIMESTAMPTZ | NOT NULL, DEFAULT now() |

**Constraints**:
- `uq_files_s3_key` (unique S3 key)
- `chk_files_size_positive` (size >= 0)
- `chk_files_deleted_at` (is_deleted = false → deleted_at NULL; is_deleted = true → deleted_at NOT NULL)

**Indexes**:
- `idx_files_tenant_id`
- `idx_files_owner_id`
- `idx_files_folder_id`
- `idx_files_tenant_active` (partial: WHERE is_deleted = false)

**Trigger**: `trg_files_updated_at`
**Trigger**: `trg_files_validate_tenant` (validates owner and folder belong to same tenant)

---

## Multi-Tenant Isolation Triggers (Defense in Depth)

1. **folders**: `fn_validate_folder_tenant_consistency()` - ensures folder.owner_id belongs to folder.tenant_id
2. **files**: `fn_validate_file_tenant_consistency()` - ensures file.owner_id AND file.folder_id (if set) belong to file.tenant_id

These run BEFORE INSERT/UPDATE and raise exceptions on violations.