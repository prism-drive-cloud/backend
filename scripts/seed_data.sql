-- =========================================================
-- MINI DRIVE MULTI-TENANT — DATOS DE PRUEBA
-- Uso: SOLO para desarrollo/demo. No ejecutar en producción.
-- Requiere haber corrido schema.sql antes.
-- =========================================================
-- Contraseñas: los password_hash de abajo son FICTICIOS
-- (no corresponden a "123456" real). El backend debe generar
-- hashes reales con bcrypt/argon2 en el flujo de registro.
-- =========================================================

-- ---------------------------------------------------------
-- TENANTS: 2 empresas + 1 cuenta personal
-- ---------------------------------------------------------
INSERT INTO tenants (id, name, slug, is_personal, storage_quota_bytes) VALUES
('a1000000-0000-0000-0000-000000000001', 'Acme Corp',            'acme',            FALSE, 1073741824),
('a1000000-0000-0000-0000-000000000002', 'Beta Industries',      'beta',            FALSE, 1073741824),
('a1000000-0000-0000-0000-000000000003', 'Juan Pérez (Personal)','juan-perez',      TRUE,  1073741824);

-- ---------------------------------------------------------
-- USERS: super_admin (sin tenant) + admins/usuarios por tenant
-- ---------------------------------------------------------
INSERT INTO users (id, tenant_id, email, password_hash, full_name, role) VALUES
('b1000000-0000-0000-0000-000000000001', NULL,
    'superadmin@minidrive.com', '$2b$10$ficticio.hash.super.admin', 'Super Admin', 'super_admin'),

('b1000000-0000-0000-0000-000000000002', 'a1000000-0000-0000-0000-000000000001',
    'admin@acme.com', '$2b$10$ficticio.hash.acme.admin', 'Ana (Admin Acme)', 'tenant_admin'),
('b1000000-0000-0000-0000-000000000003', 'a1000000-0000-0000-0000-000000000001',
    'carlos@acme.com', '$2b$10$ficticio.hash.acme.user', 'Carlos (Usuario Acme)', 'user'),

('b1000000-0000-0000-0000-000000000004', 'a1000000-0000-0000-0000-000000000002',
    'admin@beta.com', '$2b$10$ficticio.hash.beta.admin', 'Beatriz (Admin Beta)', 'tenant_admin'),
('b1000000-0000-0000-0000-000000000005', 'a1000000-0000-0000-0000-000000000002',
    'bruno@beta.com', '$2b$10$ficticio.hash.beta.user', 'Bruno (Usuario Beta)', 'user'),

('b1000000-0000-0000-0000-000000000006', 'a1000000-0000-0000-0000-000000000003',
    'juan.perez@email.com', '$2b$10$ficticio.hash.juan', 'Juan Pérez', 'user');

-- ---------------------------------------------------------
-- FOLDERS: una carpeta por tenant corporativo
-- ---------------------------------------------------------
INSERT INTO folders (id, tenant_id, owner_id, name) VALUES
('c1000000-0000-0000-0000-000000000001', 'a1000000-0000-0000-0000-000000000001',
    'b1000000-0000-0000-0000-000000000002', 'Contratos'),
('c1000000-0000-0000-0000-000000000002', 'a1000000-0000-0000-0000-000000000002',
    'b1000000-0000-0000-0000-000000000004', 'Facturas');

-- ---------------------------------------------------------
-- FILES: archivos en raíz y dentro de carpeta, por tenant
-- ---------------------------------------------------------
INSERT INTO files (tenant_id, owner_id, folder_id, original_name, mime_type, size_bytes, s3_key) VALUES
-- Acme
('a1000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000003', NULL,
    'foto_equipo.jpg', 'image/jpeg', 2500000,
    'tenants/a1000000-0000-0000-0000-000000000001/foto_equipo.jpg'),
('a1000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000002',
    'c1000000-0000-0000-0000-000000000001',
    'contrato_2026.pdf', 'application/pdf', 850000,
    'tenants/a1000000-0000-0000-0000-000000000001/contrato_2026.pdf'),
-- Beta
('a1000000-0000-0000-0000-000000000002', 'b1000000-0000-0000-0000-000000000005',
    'c1000000-0000-0000-0000-000000000002',
    'factura_agosto.pdf', 'application/pdf', 120000,
    'tenants/a1000000-0000-0000-0000-000000000002/factura_agosto.pdf'),
-- Personal
('a1000000-0000-0000-0000-000000000003', 'b1000000-0000-0000-0000-000000000006', NULL,
    'cv_juan.pdf', 'application/pdf', 340000,
    'tenants/a1000000-0000-0000-0000-000000000003/cv_juan.pdf');

-- =========================================================
-- PRUEBA DE FUGA (debe FALLAR con el trigger de aislamiento)
-- Descomenta este bloque para verificarlo manualmente:
-- =========================================================
-- INSERT INTO files (tenant_id, owner_id, folder_id, original_name, mime_type, size_bytes, s3_key)
-- VALUES (
--     'a1000000-0000-0000-0000-000000000001',  -- tenant: Acme
--     'b1000000-0000-0000-0000-000000000004',  -- owner: Admin de BETA (¡cruzado!)
--     NULL, 'archivo_fuga.pdf', 'application/pdf', 1000,
--     'tenants/a1000000-0000-0000-0000-000000000001/fuga.pdf'
-- );
-- Resultado esperado: ERROR - Aislamiento violado: owner_id ... pertenece al tenant ...
