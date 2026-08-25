# Documento de Trabajo y Debate: Ecosistema Mini Drive Multi-Tenant

**Objetivo:** Este documento sirve como base de discusión y alineación para la reunión con los líderes de equipo (Backend, Web, Mobile, Cloud, DevOps, D.A., QA, Documentación y Desbloqueo). El propósito es debatir, aprobar el alcance y definir los acuerdos técnicos antes de redactar las Historias de Usuario (HU) definitivas en Jira.

**Restricción de Tiempo:** 5 días hábiles de trabajo (Lunes a Viernes). El Viernes se realiza el cierre y la presentación final del producto ante evaluadores.

---

## 1. Modelo de Usuarios, Roles y Flujos de Creación

### A. Tipos de Cuenta

* **Modo Empresarial (Multi-Tenant):** Una organización registra su empresa (Tenant) y administra a sus colaboradores. Cada empresa tiene aislamiento total de datos y cuota de almacenamiento independiente.
* **Modo Personal:** Usuarios independientes con almacenamiento individual (hasta 1 GB) sin pertenecer a una organización corporativa.

### B. Jerarquía de Roles

* **Super Admin (Global):** Administrador de la plataforma completa. Visualiza todas las empresas registradas, consumo global de S3 y métricas del sistema.
* **Admin Corporativo (Tenant Admin):** Administra los usuarios de su empresa, revisa el consumo de almacenamiento institucional (1 GB base) y audita la actividad interna.
* **Usuario Final (Colaborador / Personal):** Sube, descarga, organiza, previsualiza y comparte archivos según sus permisos.

### C. Flujos Principales de la Aplicación

#### Flujo 1: Registro y Creación de Cuenta (Onboarding)

1. **Registro Corporativo:** Ingreso de datos de la empresa (Nombre de la marca/tenant, subdominio o slug, email del administrador y contraseña). Se crea el tenant en base de datos y su prefijo aislado en AWS S3.
2. **Registro Personal:** Formulario directo de usuario (Nombre, email, contraseña). Se le asigna un tenant individual o partición personal de 1 GB.
3. **Invitación/Alta de Usuarios Corporativos:** El Admin Corporativo da de alta cuentas asociadas a su `tenant_id`.

#### Flujo 2: Autenticación y Acceso

1. El usuario ingresa credenciales (email y contraseña).
2. El sistema valida y emite un token JWT que incluye: `user_id`, `tenant_id` y `role`.
3. La interfaz (Web o Móvil) adapta su vista e identidad visual según el rol y la empresa del usuario.

#### Flujo 3: Carga de Archivos (Web y Móvil)

1. El usuario arrastra un archivo al área Drag & Drop (Web) o lo selecciona desde el explorador/cámara (Móvil).
2. El cliente solicita a la API una URL prefirmada de subida (`Presigned URL`) enviando nombre, peso y tipo MIME.
3. El backend valida que el archivo no supere el límite restante de la cuota de 1 GB.
4. El cliente transfiere el binario directamente a AWS S3 mediante la URL prefirmada, mostrando una barra de progreso real.
5. Al finalizar la subida a S3, el cliente notifica a la API para registrar los metadatos en la base de datos y refrescar la vista.

#### Flujo 4: Consulta, Previsualización y Descarga

1. El usuario visualiza la lista o cuadrícula de archivos filtrada por su `tenant_id`.
2. Para previsualizar (PDFs, imágenes, audio/video), la API genera una URL temporal de lectura segura de S3.
3. Para descargar, se invoca la URL de descarga directa desde S3.

#### Flujo 5: Compartición y Seguridad

1. **Compartir Archivo:** Generación de un enlace público o interno con expiración y permisos (solo lectura).
2. **Caja Fuerte / Carpeta Fuerte:** Sección protegida dentro del Drive que solicita una clave de seguridad o PIN secundario para listar y abrir archivos confidenciales.

---

## 2. Clasificación de Requerimientos: MVP vs Deseables

Para debate en la reunión: Definir qué entra en el corte estricto del Viernes y qué queda como deseable (Fase 2).

### A. Requerimientos Críticos (Obligatorios para el MVP - Días 1 a 4)

* **Aislamiento Multi-Tenant Estricto:** Segregación total en base de datos y rutas de S3 (`/tenants/{tenant_id}/...`). Cero fugas de información.
* **Autenticación y Control de Roles:** Login con JWT, roles Super Admin, Admin Corporativo y Usuario.
* **CRUD de Archivos:** Crear/subir, listar, renombrar, mover y eliminación lógica.
* **Carga Drag & Drop (Web):** Arrastrar archivos con barra de progreso asíncrona visible.
* **Carga y Consulta Móvil:** Inicio de sesión, listado nativo y subida desde cámara/galería.
* **Almacenamiento Directo en AWS S3:** Uso exclusivo de Presigned URLs (ningún binario pasa por el disco del servidor API).
* **Control de Cuota de 1 GB:** Validación en backend para bloquear subidas que excedan el límite contratado.
* **Descarga de Archivos:** Generación de enlaces seguros de descarga.
* **Sincronización:** Reflejo de cambios entre frentes Web y Móvil.
* **Infraestructura y Despliegue:** Contenedores Docker y despliegue en ambiente Staging.

### B. Requerimientos Deseables (Fase 2 / Sujetos a velocidad del equipo)

* **Tablero Power BI (Data & Analytics):** Panel de control conectado a la base de datos con consumo global y por empresa.
* **Visualizador y Reproductor Multimedia:** Player de audio/video y visor de PDFs incrustado sin descarga previa.
* **Carga de Carpetas Enteras:** Parseo recursivo de estructuras de carpetas desde el explorador del sistema operativo.
* **Caja Fuerte / Carpeta Fuerte:** Doble factor o PIN secundario para archivos encriptados/ocultos.
* **Compartir con Enlaces Temporales:** Generación de links con fecha de expiración para usuarios externos.
* **Papelera de Reciclaje y Restauración:** Recuperación de archivos eliminados durante un periodo de gracia.

---

## 3. Catálogo de Endpoints Principales (Lista Base para Backend)

Esta lista se presenta al equipo de Backend y a los frentes Web/Móvil para validar el contrato inicial:

### Módulo: Autenticación y Tenants

* `POST /api/v1/auth/register-tenant` -> Registro de empresa y creación de administrador.
* `POST /api/v1/auth/register-user` -> Registro de usuario individual o invitación de colaborador.
* `POST /api/v1/auth/login` -> Autenticación y emisión de JWT (`tenant_id`, `role`, `user_id`).
* `GET /api/v1/auth/me` -> Perfil del usuario autenticado y datos del tenant.

### Módulo: Almacenamiento y Cuotas

* `GET /api/v1/tenants/usage` -> Consulta de espacio ocupado vs límite (1 GB).

### Módulo: Archivos y Carpetas

* `GET /api/v1/files` -> Listar archivos y carpetas del tenant (soporte de paginación y búsqueda).
* `POST /api/v1/files/upload-url` -> Solicitar Presigned URL de subida a S3 (valida cuota de 1 GB).
* `POST /api/v1/files/confirm` -> Notificar subida exitosa a S3 y registrar metadatos en BD.
* `GET /api/v1/files/{id}/download-url` -> Obtener Presigned URL temporal de descarga o visualización.
* `PATCH /api/v1/files/{id}` -> Renombrar o mover archivo.
* `DELETE /api/v1/files/{id}` -> Eliminación lógica de archivo.
* `POST /api/v1/folders` -> Crear nueva carpeta en la estructura lógica.

### Módulo: Compartir y Caja Fuerte (Deseables)

* `POST /api/v1/files/{id}/share` -> Crear enlace de compartición con fecha de expiración.
* `POST /api/v1/vault/verify` -> Validar PIN o contraseña secundaria de la Caja Fuerte.
* `GET /api/v1/vault/files` -> Listar archivos protegidos dentro de la Caja Fuerte.

### Módulo: Analítica (Para Dashboard)

* `GET /api/v1/analytics/overview` -> Métricas agregadas de almacenamiento, usuarios y tipos de archivo.

---

## 4. Matriz de Entregables por Equipo (Semana de 5 Días)

| Equipo                      | Entregable Crítico para el Viernes                                      | Criterio de Aceptación Técnico                                              |
| :-------------------------- | :----------------------------------------------------------------------- | :---------------------------------------------------------------------------- |
| **Backend & DB**      | API REST documentada en Swagger con lógica multi-tenant y SDK de AWS S3 | Endpoints funcionales con JWT, validación de cuota de 1 GB y Presigned URLs. |
| **Cloud & AWS**       | Bucket de S3 configurado con CORS, políticas IAM y prefijos por tenant  | Acceso seguro mediante Presigned URLs sin exponer credenciales maestras.      |
| **Frontend Web**      | Aplicación Web con vista de archivos, Drag & Drop y barra de progreso   | Subida asíncrona a S3, listado dinámico y adaptación visual por tenant.    |
| **Mobile Apps**       | App móvil con Login, listado y subida desde cámara/galería            | Navegación fluida, consumo de API central y descarga de archivos.            |
| **DevOps & CI/CD**    | Docker Compose para desarrollo y ambiente Staging desplegado             | Pipeline de integración continua activo y servidor de pruebas disponible.    |
| **Data & Analytics**  | Tablero en Power BI conectado a la base de datos                         | Visualización de consumo global y segmentado por inquilino.                  |
| **QA (Calidad)**      | Matriz de pruebas ejecutada y certificación de NO fuga multi-tenant     | Reporte de bugs y validación de seguridad de datos entre empresas.           |
| **Documentación**    | Diagrama de arquitectura C4, especificación Swagger y manual de usuario | Documentación técnica lista para la presentación final.                    |
| **Equipo Desbloqueo** | Resolución inmediata de trabas técnicas (CORS, IAM, contratos de API)  | SLA de atención rápida para no detener el avance de ningún frente.         |

---

## 5. Cronograma Oficial de 5 Días (Lunes a Viernes)

```
[ LUNES ]
- Mañana: Reunión de líderes, debate de este documento y aprobación de alcance.
- Tarde: Publicación del Swagger con Mocks (Backend) y configuración de S3/IAM (AWS).

[ MARTES ]
- Desarrollo en paralelo: Frontends Web y Mobile maquetan con Mock Server.
- Backend implementa modelos de BD, Auth JWT y lógica de aislamiento.
- DevOps levanta entorno Staging y pipelines de CI/CD.

[ MIÉRCOLES ]
- Backend integra AWS S3 Presigned URLs y validación de cuota de 1 GB.
- Frontends Web y Mobile conectan la API real (se reemplazan mocks).
- Data & Analytics conecta Power BI a la base de datos.

[ JUEVES ]
- Integración de punta a punta: Drag & Drop en Web + Subida en Mobile + S3.
- QA ejecuta pruebas intensivas de fuga multi-tenant y pruebas de carga.
- Equipo de Desbloqueo y Devs corrigen bugs de alta prioridad.

[ VIERNES ]
- Mañana (Code Freeze): Congelamiento de código, ajustes menores y cierre de documentación.
- Tarde: Ensayo general y Presentación Oficial del Producto (Demo en vivo).
```

---

## 6. Puntos Clave para Debatir en la Reunión

Preguntas directas para definir con los líderes técnicos:

1. **Manejo de Carpetas:** ¿Se implementa estructura de carpetas anidadas en BD desde el inicio o se parte de una vista plana con tags/categorías para asegurar la entrega del Viernes?
2. **Alcance de la Caja Fuerte:** ¿Se incluye en el MVP o se deja como demostración conceptual / Fase 2 para no sobrecargar a Backend y Seguridad?
3. **Estrategia de Mocks para Frontends:** ¿Backend entregará el Mock Server el Lunes en la tarde para que Web y Mobile avancen sin demoras el Martes en la mañana?
4. **Validación de Cuota de 1 GB:** ¿El bloqueo se hace previo a la emisión de la Presigned URL en Backend o se valida también en Frontend?
