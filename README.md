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
| Se comunica con | API Gateway (JWT), device-service (REST: ¿el lugar tiene dispositivo activo?), eventos RabbitMQ |

## Qué hace
- HU-009: crear un lugar; el primero queda por defecto.
- HU-010: listar los lugares del dueño y cambiar el seleccionado (`is_default`).
- HU-011: editar y eliminar un lugar (soft delete), bloqueando la eliminación si tiene un dispositivo activo.
- Entrega 3: miembros e invitaciones (`place_members`, `invitations`).

## Qué no hace
- Autenticación → auth-service (`security`).
- Vincular dispositivos → device-service (`devices`).
- Consumo, tarifas y metas → consumption-service.
- Log de actividad → se publica un evento; no escribe en `security.activity_log`.

## Cómo correr localmente
```powershell
# 1. Database: ms-iam-db and ms-places-db applied (docker compose up in each)
# 2. Service (inside ms-places):
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
