# place-service

## Responsabilidad
Gestiona los lugares de cada usuario (crear, listar, cambiar de lugar, editar y eliminar) y es el dueño autoritativo del schema `places`.

## Ubicación en la arquitectura

| Campo | Valor |
|-------|-------|
| Bounded context | BC-02 Place Management |
| Puerto | 3002 |
| Stack | .NET 10 (LTS) + ASP.NET Core + EF Core 10, arquitectura hexagonal. Repo `ms-places` |
| Base de datos | SQL Server `sy-water-db`, schema `places` |
| Repo de migraciones | `ms-places-db` (Liquibase) |
| Se comunica con | API Gateway (JWT), device-service (REST: ¿el lugar tiene dispositivo activo?), ms-iam (REST interno: contactos de usuarios y correo de invitación), eventos RabbitMQ |

## Qué hace
- HU-009: crear un lugar; el primero queda por defecto.
- HU-010: listar los lugares del dueño y cambiar el seleccionado (`is_default`).
- HU-011: editar y eliminar un lugar (soft delete), bloqueando la eliminación si tiene un dispositivo activo.
- HU-039 a HU-041: enlace de invitado (`guest_links`). Quien tiene el token ve solo el consumo del lugar, sin sesión y sin poder escribir. Desactivar o regenerar el enlace corta el acceso de inmediato.
- HU-042 y HU-043: invitaciones por correo (`invitations`, vigencia de 7 días). Solo la cuenta con ese correo puede aceptarla o rechazarla; el correo lo envía ms-iam.
- HU-044 a HU-046: colaboradores (`place_members`) con permisos `VIEW_CONSUMPTION` (siempre incluido), `VIEW_DEVICE_STATUS`, `CONTROL_VALVE`, `VIEW_ALERTS` y `VIEW_BILLS`. El dueño los cambia o los revoca y rige de inmediato.
- ms-places es quien decide el acceso: `GET /api/places/{id}/access` devuelve el rol y los permisos del usuario (404 si no tiene acceso). `GET /api/places/{id}` sigue siendo solo del dueño; cada servicio decide por endpoint si acepta colaboradores.
- Internos (`X-Internal-Key`): `GET /internal/guest-links/{token}` (qué lugar abre un token) y `GET /internal/places/{id}/recipients?permission=` (a quién avisar, HU-047).

## Qué no hace
- Autenticación → auth-service (`security`).
- Vincular dispositivos → device-service (`devices`).
- Consumo, tarifas y metas → consumption-service.
- Log de actividad → se publica un evento; no escribe en `security.activity_log`.

## Cómo correr localmente
```powershell
# 1. Database: ms-iam-db and ms-places-db applied (docker compose up in each)
# 2. Service (inside ms-places). Services:IamBaseUrl and Internal:ApiKey (same value as in ms-iam, 24+ chars) enable invitations; without them sharing answers 503.
#    Services:NotificationsBaseUrl (ms-notification) puts the invitation in the notification center of the invited person (and WhatsApp / SMS) and tells the owner the answer; without it invitations go only by e-mail:
dotnet user-secrets set "ConnectionStrings:Places" "Server=localhost,1433;Database=sy-water-db;User Id=places_app;Password=<PLACES_APP_PASSWORD>;TrustServerCertificate=True" --project src/SyWater.Places.Api
Copy-Item <ms-iam>/keys/public.pem src/SyWater.Places.Api/keys/iam-public.pem   # ms-iam PUBLIC key
dotnet test
dotnet run --project src/SyWater.Places.Api    # http://localhost:3002/health
```
Guía completa: `claude/ms-places-backend-guia.md`.

## Documentos relacionados
- `data-model.md` — modelo de datos
- `decisions.md` — decisiones técnicas
- `events.md` — pendiente (PlaceCreated, PlaceUpdated, PlaceDeleted)
- `runbook.md` — pendiente, antes del primer deploy a staging
- `07-api/contracts/openapi/place-service.yaml` — pendiente (API-first)
