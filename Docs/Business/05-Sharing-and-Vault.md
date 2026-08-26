# Sharing y Boveda - Fase 2

Este documento describe el estado actual y el trabajo necesario para convertir
los controladores de Sharing y Boveda en funcionalidades operativas. La Fase 2
se mantiene separada del modulo Drive para no cambiar la logica existente de
archivos, carpetas, cuotas o S3.

## 1. Estado actual

Existen dos controladores en `Controllers/`:

| Archivo | Ruta base | Endpoints |
|---|---|---|
| `SharesController.cs` | `/api/v1/files/{id}/share` | `POST` |
| `VaultController.cs` | `/api/v1/vault` | `POST /verify`, `GET /files` |

Los tres endpoints:

- estan incluidos como endpoints MVC porque `Program.cs` usa
  `AddControllers()` y `MapControllers()`;
- requieren la politica `User`, por lo que necesitan un JWT valido;
- devuelven `501 Not Implemented` hasta que existan los servicios de negocio;
- no consultan la base de datos ni S3;
- no modifican la logica actual de Drive.

El tipo `VerifyPinRequest` existe de forma local en `VaultController.cs` para
mantener disponible el contrato HTTP mientras se define el DTO definitivo.

## 2. Responsabilidad de cada capa

Los controladores deben permanecer delgados. Su responsabilidad es recibir la
peticion HTTP, obtener la identidad autenticada, llamar al servicio de negocio
y traducir el resultado a un codigo HTTP.

```text
SharesController
  -> IShareService
  -> ShareService
  -> IFileRepository / ShareRepository
  -> IS3Service si el enlace usa S3

VaultController
  -> IVaultService
  -> VaultService
  -> VaultRepository y FileRepository
```

### Controller

- Define rutas, verbos HTTP y autorizacion.
- Recibe DTOs y `CancellationToken`.
- Obtiene `UserId` y `TenantId` desde claims o `ITenantContext`.
- No valida contrasenas, permisos de archivos, expiraciones ni acceso a S3.
- Devuelve `401`, `403`, `404`, `409` o `400` cuando el servicio lance las
  excepciones de negocio correspondientes.

### Business

Debe contener las reglas de seguridad y orquestacion:

- validar que el archivo pertenece al tenant del usuario;
- comprobar que el usuario puede compartir ese archivo;
- establecer expiracion y permisos del enlace;
- generar o invalidar la sesion temporal de Boveda;
- validar el PIN usando un hash, nunca texto plano;
- comprobar que un usuario desbloqueo la Boveda antes de listar archivos.

### Data

Debe guardar solamente datos persistentes. No debe recibir `HttpContext` ni
generar respuestas HTTP.

### S3

Los enlaces de archivos deben seguir usando `IS3Service`. Las credenciales no
deben devolverse al cliente ni debe pasar el binario por la API.

## 3. Componentes pendientes

Estos componentes son necesarios para la implementacion completa y deben
crearse en sus capas correspondientes cuando se habilite la Fase 2:

### Sharing

1. `IShareService` en `Business/Interfaces/`.
2. `ShareService` en `Business/Services/`.
3. DTOs para crear y responder un enlace, por ejemplo:
   - `CreateShareRequest` con expiracion y permisos;
   - `ShareLinkResponse` con token, URL, fecha de expiracion y permiso.
4. Entidad `Share` y tabla `shares`.
5. `IShareRepository` y `ShareRepository`.
6. Registro de `IShareService` y `IShareRepository` en las extensiones de DI.
7. Una ruta publica de resolucion del token, si el enlace debe ser accesible
   sin autenticacion.

Una posible tabla `shares` debe incluir como minimo:

| Campo | Proposito |
|---|---|
| `id` | Identificador del enlace |
| `file_id` | Archivo compartido |
| `tenant_id` | Aislamiento del tenant |
| `created_by` | Usuario que creo el enlace |
| `token_hash` | Token almacenado de forma segura |
| `permission` | Por ejemplo `read` |
| `expires_at` | Fecha limite |
| `revoked_at` | Revocacion opcional |

El token crudo solo debe entregarse al crear el enlace. Para consultarlo,
debe almacenarse un hash y compararse de forma segura.

### Boveda

1. `IVaultService` en `Business/Interfaces/`.
2. `VaultService` en `Business/Services/`.
3. DTO definitivo `VerifyPinRequest`, con validacion de longitud y formato.
4. DTO de respuesta para verificacion, sin devolver el PIN.
5. Entidad o mecanismo de configuracion para el PIN de cada usuario o tenant.
6. `VaultRepository` si la Boveda requiere tablas propias.
7. Forma de marcar un archivo como protegido, por ejemplo `is_vaulted` o una
   relacion `vault_files`.
8. Sesion temporal de Boveda, preferiblemente un claim o registro de corta
   duracion, para no aceptar un simple booleano enviado por el cliente.
9. Registro de `IVaultService` en DI.

## 4. Flujo de Sharing

### Crear enlace

`POST /api/v1/files/{id}/share`

1. JWT middleware autentica al usuario.
2. `SharesController` obtiene `UserId` y `TenantId` del contexto autenticado.
3. El controlador envia `id`, la solicitud y el token de cancelacion a
   `IShareService.CreateShareLinkAsync`.
4. `ShareService` obtiene el archivo con filtro por tenant.
5. Comprueba que el archivo existe, no esta eliminado y el usuario tiene
   permiso para compartirlo.
6. Valida la fecha de expiracion y el permiso solicitado.
7. Genera un token aleatorio y guarda solamente su hash.
8. Devuelve la URL temporal y sus metadatos.

Respuesta recomendada: `201 Created`.

Errores esperados:

- `401 Unauthorized`: no hay usuario autenticado;
- `403 Forbidden`: el usuario no puede compartir el archivo;
- `404 Not Found`: el archivo no pertenece al tenant o no existe;
- `400 Bad Request`: expiracion o permiso invalido.

## 5. Flujo de Boveda

### Verificar PIN

`POST /api/v1/vault/verify`

1. El usuario envia el PIN por HTTPS.
2. `VaultController` pasa el DTO al servicio sin inspeccionar el PIN.
3. `VaultService` obtiene el usuario o tenant desde el contexto autenticado.
4. Compara el PIN contra un hash usando un algoritmo como BCrypt o Argon2.
5. Registra intentos fallidos y aplica limite de intentos si corresponde.
6. Crea una autorizacion temporal de Boveda.

Respuesta recomendada: `200 OK` con la fecha de expiracion de la autorizacion,
sin devolver informacion sensible.

### Listar archivos protegidos

`GET /api/v1/vault/files`

1. El usuario debe estar autenticado.
2. `VaultService` verifica que la autorizacion temporal de Boveda sigue activa.
3. Consulta solo archivos del tenant actual marcados como protegidos.
4. Aplica el filtro de soft delete ya usado por `FileEntity`.
5. Devuelve DTOs, no entidades EF ni secretos de almacenamiento.

Respuesta recomendada: `200 OK` con una lista paginada.

## 6. Cambios que deben hacerse despues en los controladores

Los comentarios de integracion dentro de `SharesController.cs` y
`VaultController.cs` marcan los puntos exactos donde debe inyectarse la capa
Business. El cambio previsto es equivalente a este patron:

```csharp
private readonly IShareService _shareService;

public SharesController(IShareService shareService)
{
    _shareService = shareService;
}

[HttpPost]
public async Task<ActionResult<ShareLinkResponse>> CreateShareLinkAsync(
    Guid id,
    [FromBody] CreateShareRequest request,
    CancellationToken cancellationToken)
{
    // UserId y TenantId deben venir del contexto autenticado.
    var response = await _shareService.CreateShareLinkAsync(
        id,
        request,
        cancellationToken);

    return StatusCode(StatusCodes.Status201Created, response);
}
```

El controlador de Boveda debe seguir el mismo patron con `IVaultService`. La
implementacion concreta de permisos, PIN, expiracion, tenant y persistencia no
debe trasladarse a estos archivos.

## 7. Pruebas de sandbox

### Estado actual

Con un JWT valido y el backend ejecutandose:

```bash
curl -i -X POST http://localhost:5104/api/v1/files/00000000-0000-0000-0000-000000000001/share \
  -H "Authorization: Bearer TU_TOKEN"
```

Resultado actual esperado: `501 Not Implemented`.

```bash
curl -i -X POST http://localhost:5104/api/v1/vault/verify \
  -H "Authorization: Bearer TU_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"pin":"1234"}'
```

Resultado actual esperado: `501 Not Implemented`.

```bash
curl -i http://localhost:5104/api/v1/vault/files \
  -H "Authorization: Bearer TU_TOKEN"
```

Resultado actual esperado: `501 Not Implemented`.

Sin JWT valido, los tres endpoints deben devolver `401 Unauthorized`.

### Criterios despues de implementar Business

- Un usuario de Tenant A no puede crear enlaces para archivos de Tenant B.
- Un enlace expirado o revocado no permite descargar el archivo.
- El token almacenado en la base de datos no es recuperable en texto plano.
- Un PIN incorrecto no desbloquea la Boveda.
- La autorizacion de Boveda expira y no puede falsificarse desde el cliente.
- Los archivos eliminados no aparecen en la lista de Boveda.
- Un usuario de otro tenant no puede consultar archivos protegidos.
- Los errores se convierten mediante `GlobalExceptionMiddleware` a
  `ProblemDetails`.

## 8. Limites de esta entrega

Esta documentacion y los comentarios de los controladores no implementan
Sharing ni Boveda. No crean tablas, servicios, repositorios, paquetes,
configuracion ni migraciones. El objetivo actual es dejar definido el contrato
y el punto de integracion para que la implementacion futura no altere el
modulo Drive.