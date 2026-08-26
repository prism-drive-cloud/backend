-- =========================================================
-- MINI DRIVE MULTI-TENANT — SCRIPT DE ESQUEMA (MVP)
-- Motor: PostgreSQL 15+ (probado en Supabase / Postgres 16)
-- =========================================================
-- Orden de creación: tenants -> users -> folders -> files -> triggers
-- Ver DECISIONES.md para el porqué de cada elección de diseño.
-- =========================================================

-- ---------------------------------------------------------
-- 0. EXTENSIONES
-- ---------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS "pgcrypto"; -- necesaria para gen_random_uuid()

-- ---------------------------------------------------------
-- 1. FUNCIÓN GENÉRICA: mantener updated_at al día
-- ---------------------------------------------------------
CREATE OR REPLACE FUNCTION trigger_set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ---------------------------------------------------------
-- 2. TABLA: tenants
--    Unifica empresas (Modo Empresarial) y cuentas personales
--    (Modo Personal) bajo el mismo modelo. Decisión #1.
-- ---------------------------------------------------------
CREATE TABLE tenants (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    slug                TEXT NOT NULL,
    is_personal         BOOLEAN NOT NULL DEFAULT FALSE,
    storage_quota_bytes BIGINT NOT NULL DEFAULT 1073741824, -- 1 GB
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT uq_tenants_slug UNIQUE (slug),
    CONSTRAINT chk_tenants_quota_positive CHECK (storage_quota_bytes > 0)
);

CREATE TRIGGER trg_tenants_updated_at
BEFORE UPDATE ON tenants
FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

COMMENT ON TABLE tenants IS 'Empresas y cuentas personales unificadas. is_personal distingue el caso.';
COMMENT ON COLUMN tenants.slug IS 'Identificador único usado como subdominio o handle.';
COMMENT ON COLUMN tenants.storage_quota_bytes IS 'Cuota de almacenamiento (1 GB por defecto, aplica a ambos modos).';

-- ---------------------------------------------------------
-- 3. TABLA: users
--    tenant_id es NULL únicamente para super_admin. Decisión #3.
-- ---------------------------------------------------------
CREATE TABLE users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     UUID NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email         TEXT NOT NULL,
    password_hash TEXT NOT NULL,
    full_name     TEXT NOT NULL,
    role          TEXT NOT NULL,
    is_active     BOOLEAN NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT uq_users_email UNIQUE (email),
    CONSTRAINT chk_users_role CHECK (role IN ('super_admin', 'tenant_admin', 'user')),
    CONSTRAINT chk_users_super_admin_no_tenant CHECK (
        (role = 'super_admin' AND tenant_id IS NULL)
        OR
        (role <> 'super_admin' AND tenant_id IS NOT NULL)
    )
);

CREATE INDEX idx_users_tenant_id ON users(tenant_id);

CREATE TRIGGER trg_users_updated_at
BEFORE UPDATE ON users
FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

COMMENT ON COLUMN users.tenant_id IS 'NULL solo para super_admin. Ver DECISIONES.md punto 3.';
COMMENT ON COLUMN users.password_hash IS 'Hash (bcrypt/argon2) generado por el backend. Nunca texto plano.';

-- ---------------------------------------------------------
-- 4. TABLA: folders
--    Estructura PLANA para el MVP (sin parent_folder_id).
--    Decisión #2 — ver DECISIONES.md para el cambio y su motivo.
-- ---------------------------------------------------------
CREATE TABLE folders (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id  UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    owner_id   UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    name       TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_folders_tenant_id ON folders(tenant_id);
CREATE INDEX idx_folders_owner_id  ON folders(owner_id);

CREATE TRIGGER trg_folders_updated_at
BEFORE UPDATE ON folders
FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

COMMENT ON TABLE folders IS 'MVP: sin anidamiento. Ver DECISIONES.md punto 2 para el plan de migración a árbol.';

-- ---------------------------------------------------------
-- 5. TABLA: files
--    Metadatos únicamente. El binario vive en S3 (s3_key referencia la ruta).
-- ---------------------------------------------------------
CREATE TABLE files (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    owner_id      UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    folder_id     UUID NULL REFERENCES folders(id) ON DELETE SET NULL,
    original_name TEXT NOT NULL,
    mime_type     TEXT NOT NULL,
    size_bytes    BIGINT NOT NULL,
    s3_key        TEXT NOT NULL,
    is_deleted    BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at    TIMESTAMPTZ NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT uq_files_s3_key UNIQUE (s3_key),
    CONSTRAINT chk_files_size_positive CHECK (size_bytes >= 0),
    CONSTRAINT chk_files_deleted_at CHECK (
        (is_deleted = FALSE AND deleted_at IS NULL)
        OR
        (is_deleted = TRUE AND deleted_at IS NOT NULL)
    )
);

CREATE INDEX idx_files_tenant_id     ON files(tenant_id);
CREATE INDEX idx_files_owner_id      ON files(owner_id);
CREATE INDEX idx_files_folder_id     ON files(folder_id);
-- Índice parcial: acelera GET /files y el cálculo de cuota (Decisión #4, Opción A)
CREATE INDEX idx_files_tenant_active ON files(tenant_id) WHERE is_deleted = FALSE;

CREATE TRIGGER trg_files_updated_at
BEFORE UPDATE ON files
FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

COMMENT ON COLUMN files.s3_key IS 'Ruta física en S3: tenants/{tenant_id}/{file_id}/{original_name}';
COMMENT ON COLUMN files.is_deleted IS 'Eliminación lógica (soft delete). El archivo sigue en S3 y en la BD.';

-- =========================================================
-- 6. TRIGGERS DE AISLAMIENTO MULTI-TENANT (Decisión #5)
--    Defensa en profundidad: aunque el backend tenga un bug,
--    la BD nunca permite que un folder/file quede "cruzado"
--    entre tenants distintos.
-- =========================================================

-- 6.1 folders: el owner debe pertenecer al mismo tenant que la carpeta
CREATE OR REPLACE FUNCTION fn_validate_folder_tenant_consistency()
RETURNS TRIGGER AS $$
DECLARE
    v_owner_tenant UUID;
BEGIN
    SELECT tenant_id INTO v_owner_tenant FROM users WHERE id = NEW.owner_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'owner_id % no existe en users', NEW.owner_id;
    END IF;

    IF v_owner_tenant IS DISTINCT FROM NEW.tenant_id THEN
        RAISE EXCEPTION 'Aislamiento violado: owner_id % pertenece al tenant %, no al tenant % de la carpeta',
            NEW.owner_id, v_owner_tenant, NEW.tenant_id;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_folders_validate_tenant
BEFORE INSERT OR UPDATE ON folders
FOR EACH ROW EXECUTE FUNCTION fn_validate_folder_tenant_consistency();

-- 6.2 files: el owner Y la carpeta (si existe) deben ser del mismo tenant que el archivo
CREATE OR REPLACE FUNCTION fn_validate_file_tenant_consistency()
RETURNS TRIGGER AS $$
DECLARE
    v_owner_tenant  UUID;
    v_folder_tenant UUID;
BEGIN
    SELECT tenant_id INTO v_owner_tenant FROM users WHERE id = NEW.owner_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'owner_id % no existe en users', NEW.owner_id;
    END IF;

    IF v_owner_tenant IS DISTINCT FROM NEW.tenant_id THEN
        RAISE EXCEPTION 'Aislamiento violado: owner_id % pertenece al tenant %, no al tenant % del archivo',
            NEW.owner_id, v_owner_tenant, NEW.tenant_id;
    END IF;

    IF NEW.folder_id IS NOT NULL THEN
        SELECT tenant_id INTO v_folder_tenant FROM folders WHERE id = NEW.folder_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'folder_id % no existe en folders', NEW.folder_id;
        END IF;

        IF v_folder_tenant IS DISTINCT FROM NEW.tenant_id THEN
            RAISE EXCEPTION 'Aislamiento violado: folder_id % pertenece al tenant %, no al tenant % del archivo',
                NEW.folder_id, v_folder_tenant, NEW.tenant_id;
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_files_validate_tenant
BEFORE INSERT OR UPDATE ON files
FOR EACH ROW EXECUTE FUNCTION fn_validate_file_tenant_consistency();

-- ---------------------------------------------------------
-- 7. TABLA: refresh_tokens
--    Sesiones de larga duración. Se guarda SOLO el hash del
--    token (SHA-256), nunca el valor en claro. Soporta
--    expiración, revocación y rotación (replaced_by_token_id).
-- ---------------------------------------------------------
CREATE TABLE refresh_tokens (
    id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id              UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash           TEXT NOT NULL,
    expires_at           TIMESTAMPTZ NOT NULL,
    revoked_at           TIMESTAMPTZ NULL,
    replaced_by_token_id UUID NULL REFERENCES refresh_tokens(id) ON DELETE SET NULL,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT uq_refresh_tokens_token_hash UNIQUE (token_hash)
);

CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens(user_id);

CREATE TRIGGER trg_refresh_tokens_updated_at
BEFORE UPDATE ON refresh_tokens
FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

COMMENT ON TABLE refresh_tokens IS 'Refresh tokens con rotación. Se persiste solo el hash (SHA-256), nunca el token en claro.';
COMMENT ON COLUMN refresh_tokens.token_hash IS 'SHA-256 (hex) del refresh token. El valor en claro solo se entrega una vez al cliente.';
COMMENT ON COLUMN refresh_tokens.replaced_by_token_id IS 'Token que reemplazó a éste durante la rotación. NULL si aún vigente o revocado sin reemplazo.';
