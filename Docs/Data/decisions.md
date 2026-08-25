# Architecture Decisions — Mini Drive Multi-Tenant Database

This document records the design decisions made during the modeling session, the problem each one avoids, and how it affects the Backend team. It serves as a reference to defend the design to the team and evaluators.

---

## 1. Personal Accounts Modeled as Tenants

**Decision:** A "Personal Mode" account is just another `tenant`, with `is_personal = TRUE` and a single owner user. There is no separate table for personal accounts.

**Why:** The entire system (files, folders, quotas, isolation) already revolves around `tenant_id`. If personal accounts lived in a separate table, every query on `files`, `folders`, and `usage` would have to ask "is it a tenant or is it personal?" and duplicate the isolation logic in two distinct paths.

**Problem Avoided:** Security logic duplication. With two isolation paths, the risk that one is implemented incorrectly (a leak) doubles.

**Backend Impact:** The backend simply always filters by `tenant_id` regardless of whether it's a company or personal account. The only place `is_personal` matters is in the UI (to hide team admin features from personal users) and optionally in future business rules.

**Note:** Easily reversible if separation is needed later — it's just a boolean, not a deep structural decision.

---

## 2. Folders: FLAT Structure for MVP (No Nesting)

**Decision (recorded change):** `folders` does NOT have `parent_folder_id`. All folders in a tenant are at the same level. A file may or may not belong to a folder (`folder_id` nullable), but a folder cannot contain another folder.

**Why:** The MVP endpoint catalog only includes `POST /api/v1/folders` (create) — there is no endpoint to move/nest folders. Implementing a folder tree implies: recursive paths, moving a folder with all its children, and recursive queries (`WITH RECURSIVE`) that consume development time not budgeted in the 5-day window.

**Problem Avoided:** Delaying Friday delivery by over-engineering a feature that doesn't even have a defined endpoint yet.

**Backend Impact:** The backend does NOT need recursive logic to list or move subfolders. `GET /files` simply filters by `tenant_id` + `folder_id` (or `folder_id IS NULL` for root).

**Future Migration Path (if nesting is revisited):**
```sql
ALTER TABLE folders ADD COLUMN parent_folder_id UUID
    REFERENCES folders(id) ON DELETE CASCADE;
CREATE INDEX idx_folders_parent_id ON folders(parent_folder_id);
```
This is an additive change (doesn't break existing), which is why the trade-off was accepted.

---

## 3. `tenant_id` Nullable in `users`, Only for `super_admin`

**Decision:** `users.tenant_id` accepts `NULL`, but a `CHECK` constraint enforces that **only** rows with `role = 'super_admin'` can have that null value; `tenant_admin` and `user` must always have a valid `tenant_id`.

**Why (Full Reasoning):**

The Super Admin, per the requirements document itself, "views all registered companies, global S3 consumption, and system metrics." That is, conceptually they **operate above the tenant layer**, not within one. Forcing them to belong to a specific tenant would mis-model the domain: either we invent a fictitious "special tenant" to house them (polluting the `tenants` table with a row that is neither a real company nor a personal account), or we create a separate `super_admins` table separate from `users`.

A separate table was discarded because the `POST /api/v1/auth/login` endpoint needs a single source of truth for email + password + role, regardless of whether it's a super admin, corporate admin, or end user. Splitting authentication into two tables would force the backend to do two lookups (or a join) on every login, just to resolve a case that typically involves 1–2 platform accounts.

**Problems Avoided:**
- Avoids polluting `tenants` with a fictitious "Platform" row that is neither company nor personal account.
- Avoids splitting authentication logic into two tables.
- The `CHECK` constraint prevents the real risk: a normal user (`user` or `tenant_admin`) ending up with `tenant_id = NULL` due to a backend bug, which would be a severe isolation bug (a "floating" user without a tenant could leak through any query assuming `tenant_id NOT NULL`).

**Backend Impact:** When generating the JWT, if `role = super_admin`, the token's `tenant_id` field is simply empty/null, and the backend must treat it as "no tenant filter" (global access) only for super admin endpoints. For all other roles, the backend can confidently assume `tenant_id` ALWAYS comes populated — no need to validate that case because the DB already guarantees it.

---

## 4. Quota Calculation: Live (Option A), No Cached Counter

**Decision:** `GET /tenants/usage` calculates consumption with `SUM(size_bytes) WHERE tenant_id = X AND is_deleted = FALSE` at query time. There is no cached `storage_used_bytes` column in `tenants`.

**Why:** A cached counter requires updating it on every `INSERT` and `DELETE` of `files`, and if a single case fails (a partial rollback, a bug, a load that never reaches `confirm`), the counter becomes out of sync with reality — and that kind of bug is hard to detect until someone audits.

**Problem Avoided:** Data inconsistency due to counter desynchronization. With live `SUM()`, the number is always exact because it's calculated directly on the source of truth.

**Backend Impact:** Slightly more expensive on reads at large scale, but for a 5-day MVP volume it's irrelevant — and a partial index (`idx_files_tenant_active`) was created specifically to make this calculation fast even so.

---

## 5. Cross-Validation Trigger: tenant–folder–file (Isolation)

**Decision:** Two `BEFORE INSERT OR UPDATE` triggers (one on `folders`, one on `files`) verify that the `owner_id` (and for files, also the `folder_id`) belong to the **same** `tenant_id` as the row being inserted/updated.

**Why:** A simple `FOREIGN KEY` only guarantees that the referenced `folder_id` *exists* — it does NOT guarantee it belongs to the correct tenant. Without this trigger, a backend bug (e.g., dragging the wrong `tenant_id` from the session) could insert a file from Tenant A pointing to a folder from Tenant B, and the Tenant B user would see (partially) foreign content when listing their folder.

**Problem Avoided:** Multi-tenant isolation leak — your #1 priority — even if the backend has a logic error. It's an independent defense layer from the application code.

**Sandbox Tested:** Validated with real Postgres that:
- Inserting a file with `owner_id` from another tenant → `ERROR` (blocked).
- Inserting a file with `folder_id` from another tenant → `ERROR` (blocked).
- Inserting a `super_admin` with non-null `tenant_id` → `ERROR` (blocked by CHECK).
- Quota calculation (`SUM` grouped) returns correct totals per tenant.

**Backend Impact:** The backend must catch and translate the trigger's error message to a readable `400`/`403` for the client (e.g., "You don't have permission on this resource"), rather than letting a generic Postgres `500` bubble up.

---

## 6. Corporate User Onboarding: Direct (No Invitations Table)

**Decision:** The Corporate Admin creates the account directly (email + temporary password), and the backend handles sending the notification email. There is no `invitations` table or `pending_invitation` state in `users`.

**Why:** The requirements document describes the flow as "the Corporate Admin creates accounts associated with their tenant_id" — it does not mention an invitation flow with acceptance by the invitee. Adding that table would be solving a requirement that wasn't asked for.

**Problem Avoided:** Unnecessary complexity (intermediate states, invitation expiration, re-sending invitations) for a flow that wasn't requested.

**Backend Impact:** The corporate user creation endpoint simply inserts into `users` with `tenant_id` already known (from the admin making the request) and triggers email sending as a side effect, without touching the data model.

---

## Out of Scope for MVP (Phase 2 — Consciously Deferred)

These features are in the docs as "Desirable" and **do not** have tables in this schema, to avoid overloading the 5-day MVP:

- Vault / Strong Folder (secondary PIN)
- Sharing with temporary links
- Recycle bin and restore (beyond the soft-delete already in `files`)
- Nested folders (see point 2 above)
- Power BI Dashboard (consumes this DB, doesn't require its own tables)

If the team decides to advance any of these before Friday, each is additive on the current schema (doesn't require redesigning what's already built).

---

## Backend Credentials

The backend **only** needs the standard Postgres connection — this project does NOT use Supabase Auth or Supabase Storage (real storage is S3), so **no** need to share `SUPABASE_ANON_KEY` or `SUPABASE_SERVICE_ROLE_KEY`.

What must be delivered (via secure channel, never chat/Git):
1. `DATABASE_URL` (or separate `DB_HOST/PORT/NAME/USER/PASSWORD`) — obtained in Supabase dashboard: **Project Settings → Database**. Prefer the "Connection Pooling" URL if backend runs serverless.
2. AWS S3 credentials (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`, `AWS_S3_BUCKET`) — generated by Cloud & AWS team, not this document.
3. A `JWT_SECRET` generated for signing tokens (not stored in DB).

See `.env.example` for exact format of each variable.