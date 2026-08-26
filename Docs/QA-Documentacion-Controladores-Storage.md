# Documentación QA — Controladores de Almacenamiento

**Proyecto:** miniDriveBackend
**Módulo:** Storage (Files / Folders / Storage Quota)
**Framework:** ASP.NET Core (.NET 10)
**Archivos cubiertos:** `FilesController.cs`, `FoldersController.cs`, `StorageController.cs`, `ClaimsPrincipalExtensions.cs`

---

## 1. Contexto general

Todos los endpoints de estos tres controladores están protegidos con `[Authorize]`. El `tenantId` y el `userId` **no** se reciben por parámetro ni por body: se extraen del JWT del usuario autenticado mediante `ClaimsPrincipalExtensions`.

| Claim en el JWT | Método de extensión | Uso |
|---|---|---|
| `ClaimTypes.NameIdentifier` (`sub`) | `User.GetUserId()` | Identifica al usuario que ejecuta la acción |
| `tenantId` (custom claim) | `User.GetTenantId()` | Aísla los datos por tenant (multi-tenant) |

**Casos QA transversales a validar en TODOS los endpoints:**

- **401 Unauthorized** si no se envía token, o el token está vencido/mal formado.
- **401/500** si el token es válido pero no trae el claim `tenantId` o `sub` — `GetUserId()`/`GetTenantId()` lanzan `InvalidOperationException` si el claim falta o no es un GUID parseable. *Punto de atención: hoy esto no está capturado como un 401/400 controlado, así que probablemente el resultado observado sea un 500. Reportar como bug si se espera un 401.*
- **Aislamiento entre tenants:** un usuario del tenant A nunca debe poder leer/modificar recursos (archivos, carpetas, cuota) del tenant B, aunque adivine el GUID correcto.
- **CancellationToken:** cancelar la petición desde el cliente (cerrar la conexión) debería cortar la operación sin dejar estados inconsistentes — difícil de probar por UI, pero vale la pena si hay test de integración.

---

## 2. FilesController — `api/files`

Basado en `IFileService`. Maneja el ciclo de vida de archivos: listado, subida vía presigned URL, confirmación, descarga, renombrado, movimiento y borrado lógico.

### 2.1 `GET /api/files`
Lista archivos del tenant actual, paginado y filtrado por `FileQueryParameters` (query string).

- **Auth:** requerida.
- **Respuesta 200:** `PagedResult<FileResponse>`.
- **QA — casos a probar:**
  - Tenant sin archivos → debe devolver lista vacía con 200, no 404.
  - Filtros de `FileQueryParameters` (nombre, carpeta, tipo, fecha, etc. — validar contra el DTO real): combinaciones válidas e inválidas.
  - Paginación: página fuera de rango, `pageSize` negativo o excesivo, `pageSize = 0`.
  - Archivos borrados lógicamente (soft-deleted) **no** deberían aparecer en el listado — confirmar regla de negocio con el equipo.
  - Solo deben listarse archivos del tenant autenticado, nunca de otro tenant.

### 2.2 `GET /api/files/{fileId}`
Obtiene el detalle de un archivo.

- **Respuesta 200:** `FileResponse`.
- **Respuesta 404:** si `fileId` no existe o pertenece a otro tenant (el servicio filtra por `tenantId`, así que un archivo ajeno debe dar 404, no 403 — confirmar que sea el comportamiento esperado y no una fuga de información).
- **QA:**
  - `fileId` con formato inválido (no-GUID) → 400 automático por model binding (`{fileId:guid}`).
  - `fileId` válido pero inexistente → 404.
  - `fileId` de un archivo de otro tenant → 404 (verificar que NO sea 200 con datos ajenos).

### 2.3 `POST /api/files/upload-url`
Solicita una URL prefirmada de S3 para subir un archivo nuevo. **Body:** `UploadUrlRequest`.

- **Respuesta 200:** `UploadUrlResponse` (incluye la URL prefirmada y probablemente el `s3Key` y expiración).
- **QA:**
  - Nombre de archivo con caracteres especiales, unicode, muy largo, vacío.
  - `contentType`/tamaño no soportados o que excedan límites del tenant.
  - **Cuota:** si el tenant está en el límite de almacenamiento, ¿se debe rechazar aquí (antes de subir) o solo en `confirm-upload`? Este endpoint no llama explícitamente a `IStorageService.CheckQuotaAvailableAsync` en la interfaz — validar si `IFileService` lo hace internamente. Si no lo hace, es un hueco a reportar: se podría generar una presigned URL aunque no haya cupo.
  - Expiración de la URL: confirmar tiempo de vida y que expire correctamente (probar subir después de vencida).
  - Solicitar múltiples upload-urls seguidas sin confirmar ninguna (huérfanas) — ver si el sistema las limpia.

### 2.4 `POST /api/files/confirm-upload`
Confirma que el archivo ya se subió a S3 y lo registra en la base de datos. **Body:** `ConfirmUploadRequest`.

- **Respuesta 201 Created**, con header `Location` apuntando a `GET /api/files/{fileId}` (vía `CreatedAtAction`).
- **QA:**
  - Confirmar un upload cuyo archivo **no** llegó realmente a S3 (el cliente miente sobre el éxito) → debería fallar la validación (`IS3Service.ObjectExistsAsync` es candidato para esa verificación interna; confirmar que se use).
  - Confirmar dos veces la misma subida (doble llamada / doble clic) → debe ser idempotente o rechazar la segunda con un error claro, nunca duplicar el registro.
  - Confirmar con cuota ya excedida al momento de confirmar (carrera entre reservar cupo y confirmar).
  - Verificar que el `Location` del header 201 apunte a una URL que efectivamente devuelva el archivo recién creado.

### 2.5 `GET /api/files/{fileId}/download-url`
Genera una URL prefirmada de descarga.

- **Respuesta 200:** `DownloadUrlResponse`.
- **QA:**
  - Archivo soft-deleted → ¿debe seguir permitiendo descarga o debe dar 404? Confirmar regla de negocio.
  - Expiración de la URL de descarga y comportamiento tras vencerse.
  - Archivo de otro tenant → 404, no la URL real.

### 2.6 `PUT /api/files/{fileId}/rename`
Body: `RenameFileRequest`.

- **Respuesta 200:** `FileResponse` actualizado.
- **QA:**
  - Nombre vacío, solo espacios, con separadores de ruta (`/`, `\`), caracteres no válidos en distintos sistemas de archivos.
  - Nombre duplicado dentro de la misma carpeta — ¿se permite o se rechaza? Confirmar regla.
  - Renombrar a un nombre idéntico al actual (no-op) — no debería fallar.
  - Longitud máxima del nombre.

### 2.7 `PUT /api/files/{fileId}/move`
Body: `MoveFileRequest` (probablemente incluye `targetFolderId`).

- **Respuesta 200:** `FileResponse` actualizado.
- **QA:**
  - Mover a una carpeta inexistente o de otro tenant → debe fallar con error controlado, no 500.
  - Mover a la misma carpeta donde ya está (no-op).
  - Mover a `null`/raíz si el DTO lo permite.
  - Colisión de nombre en la carpeta destino (¿mismo archivo ya existe allá?).

### 2.8 `DELETE /api/files/{fileId}`
Soft delete.

- **Respuesta 204 No Content** si se borró, **404** si no existía.
- **QA:**
  - Borrar dos veces el mismo archivo → segunda llamada debe dar 404, no 500 ni "éxito" fantasma.
  - Verificar que tras el soft delete el archivo:
    - Desaparece de `GET /api/files` (si esa es la regla).
    - Ya no aparece contado en `GET /api/files/total-size` ni en `GET /api/storage/usage`.
  - Verificar liberación de cuota tras el borrado (relación con `IStorageService.ReleaseQuotaAsync`).

### 2.9 `GET /api/files/total-size`
Suma total de bytes ocupados por el tenant (contando solo archivos activos, se asume).

- **Respuesta 200:** `long`.
- **QA:**
  - Tenant recién creado sin archivos → debe devolver `0`, no error.
  - Verificar consistencia contra `GET /api/storage/usage` — ambos deberían coincidir o tener una relación clara y documentada; si no coinciden, es un caso a escalar a desarrollo.

---

## 3. FoldersController — `api/folders`

Basado en `IFolderService`. Maneja jerarquía de carpetas.

### 3.1 `POST /api/folders`
Body: `CreateFolderRequest`.

- **Respuesta 201 Created** con header `Location`.
- **QA:**
  - Crear carpeta raíz (`parentFolderId` nulo/omitido si el DTO lo permite).
  - Crear subcarpeta dentro de una carpeta inexistente o de otro tenant → error controlado.
  - Nombre duplicado en el mismo nivel — confirmar si se permite.
  - Profundidad máxima de anidamiento, si existe una regla de negocio al respecto.

### 3.2 `GET /api/folders?parentFolderId=`
Lista carpetas, opcionalmente filtradas por carpeta padre.

- **Respuesta 200:** `IReadOnlyList<FolderResponse>`.
- **QA:**
  - Sin `parentFolderId` → ¿devuelve todas las carpetas del tenant o solo las raíz? Confirmar comportamiento esperado (la interfaz sugiere "todas si no se especifica", pero hay un endpoint dedicado `GET /root` — verificar que no sean redundantes/inconsistentes entre sí).
  - `parentFolderId` de una carpeta inexistente → ¿lista vacía o error?

### 3.3 `GET /api/folders/{folderId}`
- **Respuesta 200/404** igual que archivos.
- **QA:** mismos casos que 2.2 (formato inválido, inexistente, de otro tenant).

### 3.4 `PUT /api/folders/{folderId}/rename`
Body: `RenameFolderRequest`.

- **QA:** mismos casos que 2.6, más:
  - Renombrar la carpeta raíz del tenant, si existe el concepto de carpeta raíz protegida — confirmar si debe bloquearse.

### 3.5 `DELETE /api/folders/{folderId}`
- **Respuesta 204/404**.
- **QA — crítico:**
  - Borrar una carpeta que **contiene archivos y/o subcarpetas** → definir y probar la regla de negocio real: ¿falla con error explícito, borra en cascada, o mueve el contenido? Esto no es evidente desde la interfaz y es el caso de mayor riesgo del módulo.
  - Verificar qué pasa con la cuota de almacenamiento si el borrado de carpeta libera archivos (cascada).
  - Borrar dos veces la misma carpeta → 404 en la segunda.

### 3.6 `GET /api/folders/root`
- **Respuesta 200:** `IReadOnlyList<FolderResponse>`.
- **QA:** tenant nuevo sin carpetas → lista vacía, no error. Comparar consistencia con 3.2 sin filtro.

### 3.7 `GET /api/folders/{parentFolderId}/subfolders`
- **QA:**
  - `parentFolderId` sin subcarpetas → lista vacía.
  - `parentFolderId` de otro tenant → 404 o lista vacía (definir cuál; nunca debe filtrar datos ajenos).

---

## 4. StorageController — `api/storage`

Basado en `IStorageService`. Expone el estado de cuota/uso del tenant. **No** tiene endpoints de escritura expuestos directamente (reservar/liberar cupo es interno, invocado por `FilesController` al confirmar subidas o borrar).

### 4.1 `GET /api/storage/usage`
- **Respuesta 200:** `long` (bytes usados).
- **QA:** comparar contra `GET /api/files/total-size` (ver 2.9).

### 4.2 `GET /api/storage/quota`
- **Respuesta 200:** `long` (bytes asignados al tenant).
- **QA:** validar que refleje el plan/tier real del tenant (si aplica) y que un tenant recién creado tenga la cuota por defecto correcta.

### 4.3 `GET /api/storage/info`
- **Respuesta 200:** `StorageInfo` — `{ UsedBytes, QuotaBytes, AvailableBytes, UsagePercentage }`.
- **QA:**
  - Verificar la aritmética: `AvailableBytes == QuotaBytes - UsedBytes` y `UsagePercentage == UsedBytes / QuotaBytes * 100` (con redondeo consistente).
  - Caso borde: `QuotaBytes = 0` — ¿división por cero al calcular `UsagePercentage`? Es un candidato claro a bug si no está protegido.
  - Caso `UsedBytes > QuotaBytes` (tenant sobre-cupo por cambio de plan) — `AvailableBytes` negativo, ¿se maneja bien en el front consumidor?

### 4.4 `GET /api/storage/check?requestedBytes=`
- **Respuesta 200:** `bool`.
- **QA:**
  - `requestedBytes` negativo → comportamiento indefinido, probar qué devuelve.
  - `requestedBytes = 0` → debería ser siempre `true`.
  - `requestedBytes` exactamente igual al espacio disponible (límite exacto) → confirmar si es inclusive (`<=`) o exclusive (`<`).
  - Parámetro ausente → 400 (es obligatorio, sin valor por defecto en la firma).

---

## 5. Matriz resumen de endpoints

| Método | Ruta | Controller | Auth | Success | Not Found |
|---|---|---|---|---|---|
| GET | `/api/files` | Files | ✔ | 200 | — |
| GET | `/api/files/{fileId}` | Files | ✔ | 200 | 404 |
| POST | `/api/files/upload-url` | Files | ✔ | 200 | — |
| POST | `/api/files/confirm-upload` | Files | ✔ | 201 | — |
| GET | `/api/files/{fileId}/download-url` | Files | ✔ | 200 | — |
| PUT | `/api/files/{fileId}/rename` | Files | ✔ | 200 | — |
| PUT | `/api/files/{fileId}/move` | Files | ✔ | 200 | — |
| DELETE | `/api/files/{fileId}` | Files | ✔ | 204 | 404 |
| GET | `/api/files/total-size` | Files | ✔ | 200 | — |
| POST | `/api/folders` | Folders | ✔ | 201 | — |
| GET | `/api/folders` | Folders | ✔ | 200 | — |
| GET | `/api/folders/{folderId}` | Folders | ✔ | 200 | 404 |
| PUT | `/api/folders/{folderId}/rename` | Folders | ✔ | 200 | — |
| DELETE | `/api/folders/{folderId}` | Folders | ✔ | 204 | 404 |
| GET | `/api/folders/root` | Folders | ✔ | 200 | — |
| GET | `/api/folders/{parentFolderId}/subfolders` | Folders | ✔ | 200 | — |
| GET | `/api/storage/usage` | Storage | ✔ | 200 | — |
| GET | `/api/storage/quota` | Storage | ✔ | 200 | — |
| GET | `/api/storage/info` | Storage | ✔ | 200 | — |
| GET | `/api/storage/check` | Storage | ✔ | 200 | — |

---

## 6. Riesgos y huecos detectados para escalar a desarrollo

Estos puntos **no están resueltos por el código de los controladores** porque dependen de la implementación de los servicios (`FileService`, `FolderService`, `StorageService`), que no estaba disponible al momento de generar esta documentación. Se recomienda confirmarlos antes de certificar el módulo:

1. Manejo de errores de claims faltantes/malformados (`ClaimsPrincipalExtensions`) no está capturado por un middleware de excepciones visible — riesgo de 500 en vez de 401.
2. Regla de borrado de carpetas con contenido (cascada vs. bloqueo) — no está definida en la interfaz.
3. Validación de cuota en `upload-url` vs. `confirm-upload` — posible ventana para exceder cupo.
4. Consistencia entre `GET /api/files/total-size` y `GET /api/storage/usage`.
5. División por cero en `StorageInfo.UsagePercentage` cuando `QuotaBytes = 0`.
6. Falta de un mecanismo visible de manejo global de excepciones (no se ve un `ExceptionFilter`/middleware en el alcance revisado) — validar que errores de negocio (`InvalidOperationException`, etc.) no terminen como 500 genérico sin mensaje útil.

---

*Documento generado a partir de la revisión directa del código fuente de `FilesController.cs`, `FoldersController.cs`, `StorageController.cs` y `ClaimsPrincipalExtensions.cs`, y de las interfaces `IFileService`, `IFolderService`, `IStorageService`. Los DTOs referenzados (`FileQueryParameters`, `UploadUrlRequest`, etc.) no estaban disponibles al momento de escribir este documento — los campos exactos de cada request/response deben confirmarse contra el código real de `Business.DTOs` antes de diseñar los casos de prueba definitivos.*
