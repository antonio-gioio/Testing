# ContainerTrack — Codebase Guide for Claude

## Project Overview

Multi-tenant SaaS container tracking platform. ASP.NET Core 8 API + Blazor WASM frontend + PostgreSQL/PostGIS + Redis + SignalR.

## Solution Layout

```
src/ContainerTracking.Core/          # Domain only — no framework deps
src/ContainerTracking.Infrastructure/ # EF Core, providers, services, workers
src/ContainerTracking.Api/           # Controllers, middleware, SignalR hub
src/ContainerTracking.Web/           # Blazor WebAssembly client
```

## Key Patterns

### Multi-tenancy
Every entity inherits `OrganizationScopedEntity` which has `OrganizationId`. `AppDbContext` has EF global query filters that automatically add `WHERE org_id = ?` on every query. `ICurrentOrganizationContext` resolves the org from the JWT `org_id` claim.

Never query cross-org data unless you are in a PlatformAdmin context.

### API key authentication
`ApiKeyAuthMiddleware` extracts `X-API-Key` header, looks up the first 8 chars (KeyPrefix) in the DB, then verifies with PBKDF2 constant-time comparison. On success it synthesises a ClaimsPrincipal identical to JWT auth.

### Tier enforcement
`ITierEnforcementService` exposes `CanAdd*Async` methods that return `TierCheckResult`. All controller endpoints that create resources must call the appropriate check. A denied check maps to `402 Payment Required`.

### SignalR
Hub at `/hubs/tracking`. JWT passed as `?access_token=` query param. Groups: `org-{orgId}`, `container-{id}`, `shipment-{id}`. Redis backplane required for multi-pod deployments.

### Background workers
- `TrackingPollingWorker` — polls carrier/AIS APIs per tier interval
- `VesselPositionWorker` — AIS vessel position refresh every 5 minutes
- `AlertProcessingWorker` — evaluates alert rules on unprocessed tracking events (every 30s)

All workers use `IServiceScopeFactory` to create scoped DB contexts.

### Soft deletes
All entities have `IsDeleted`. EF global query filters exclude them. Never hard-delete — always set `IsDeleted = true`.

## Entity Field Names (common gotchas)

| Entity | Field | NOT |
|--------|-------|-----|
| `TrackingEvent` | `EventTime` | `OccurredAt` |
| `TrackingEvent` | `RawData` (Dictionary) | string |
| `NormalizedTrackingEvent` | `EventTime` | `OccurredAt` |
| `NormalizedTrackingEvent` | `ExternalId` | `ExternalEventId` |
| `NormalizedTrackingEvent` | `StatusAfter` | `ContainerStatusAfter` |
| `Container` | `SizeType` | `SizeCode`, `ContainerType` |
| `Vessel` | `Imo` | `ImoNumber` |
| `Vessel` | `Type` | `VesselType` |
| `Vessel` | `LastPositionUpdate` | `LastPositionAt` |
| `BillOfLading` | `OriginPort` / `DestinationPort` | `PortOfLoading` / `PortOfDischarge` |
| `BillOfLading` | `IssueDate` | `IssuedDate` |

## Common Commands

```bash
# Run API (dev)
cd src/ContainerTracking.Api && dotnet run

# Run web (dev)
cd src/ContainerTracking.Web && dotnet run

# Docker Compose (all services)
docker compose -f deploy/docker-compose.yml up -d

# Add EF migration
dotnet ef migrations add <Name> --project src/ContainerTracking.Infrastructure --startup-project src/ContainerTracking.Api
```

## Documentation
- `docs/architecture.md` — system design
- `docs/api-reference.md` — REST API reference
- `docs/user-guide.md` — end-user guide
- `docs/deployment.md` — Docker, K8s, bare-metal
