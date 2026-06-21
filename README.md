# ContainerTrack — SaaS Container Tracking Platform

A production-ready, multi-tenant SaaS platform for maritime container and shipment tracking. Built with ASP.NET Core 8, Blazor WebAssembly, PostgreSQL + PostGIS, Redis, and SignalR.

---

## Quick Start (Docker)

```bash
git clone https://github.com/antonio-gioio/testing
cd testing

# Start all services (PostgreSQL, Redis, API, Web, Seq, Prometheus, Grafana)
docker compose -f deploy/docker-compose.yml up -d

# Web app:   http://localhost:5000
# API:       http://localhost:5001
# API docs:  http://localhost:5001/swagger
# Seq logs:  http://localhost:5341
# Grafana:   http://localhost:3000  (admin/admin)
# Prometheus: http://localhost:9090
```

Register your first account at **http://localhost:5000/register** — this creates both your user and your organization automatically.

---

## Project Structure

```
ContainerTracking.sln
├── src/
│   ├── ContainerTracking.Core/          # Domain entities, enums, interfaces, models
│   ├── ContainerTracking.Infrastructure/ # EF Core, providers, services, workers
│   ├── ContainerTracking.Api/           # ASP.NET Core REST API + SignalR hub
│   └── ContainerTracking.Web/           # Blazor WebAssembly frontend
├── sql/
│   └── schema.sql                       # PostgreSQL schema (21 tables, PostGIS)
├── deploy/
│   ├── docker-compose.yml
│   └── k8s/                             # Kubernetes manifests + HPA
└── docs/
    ├── architecture.md                  # System design and data flow
    ├── api-reference.md                 # All REST endpoints
    ├── user-guide.md                    # How to use the web app
    └── deployment.md                    # Production deployment guide
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [docs/architecture.md](docs/architecture.md) | System design, tracking strategy, multi-tenancy |
| [docs/api-reference.md](docs/api-reference.md) | Full REST API endpoint reference |
| [docs/user-guide.md](docs/user-guide.md) | Step-by-step guide for using the platform |
| [docs/deployment.md](docs/deployment.md) | Docker, Kubernetes, and production config |

---

## Local Development (without Docker)

### Prerequisites

- .NET 8 SDK
- PostgreSQL 15+ with PostGIS extension
- Redis 7+
- Node.js (optional, for frontend tooling)

### 1. Apply the database schema

```bash
psql -U postgres -d containertrack -f sql/schema.sql
```

### 2. Configure the API

Create `src/ContainerTracking.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=containertrack;Username=postgres;Password=yourpassword"
  },
  "Redis": "localhost:6379",
  "Jwt": {
    "Key": "your-32-character-secret-key-here!",
    "Issuer": "ContainerTrack",
    "Audience": "ContainerTrack"
  },
  "AisStream": {
    "ApiKey": "your-aisstream-io-api-key"
  },
  "Seq": {
    "ServerUrl": "http://localhost:5341"
  }
}
```

### 3. Run the API

```bash
cd src/ContainerTracking.Api
dotnet run
# API available at http://localhost:5001
```

### 4. Run the web app

```bash
cd src/ContainerTracking.Web
dotnet run
# App available at http://localhost:5000
```

---

## Environment Variables (Docker / Production)

| Variable | Default | Description |
|----------|---------|-------------|
| `DB_HOST` | `postgres` | PostgreSQL host |
| `DB_NAME` | `containertrack` | Database name |
| `DB_USER` | `ctuser` | Database user |
| `DB_PASS` | `ctpassword` | Database password |
| `REDIS` | `redis:6379` | Redis connection string |
| `JWT_KEY` | *(required)* | 32+ character JWT signing key |
| `JWT_ISSUER` | `ContainerTrack` | JWT issuer |
| `JWT_AUDIENCE` | `ContainerTrack` | JWT audience |
| `AISSTREAM_KEY` | *(optional)* | aisstream.io API key for AIS data |
| `SEQ_URL` | `http://seq:5341` | Seq structured logging URL |
| `OTLP_ENDPOINT` | *(optional)* | OpenTelemetry collector endpoint |

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| API | ASP.NET Core 8, C# 12 |
| Frontend | Blazor WebAssembly |
| Database | PostgreSQL 16 + PostGIS |
| Cache / Pub-Sub | Redis 7 |
| Real-time | SignalR (Redis backplane) |
| Auth | JWT Bearer + API Keys (PBKDF2) |
| ORM | Entity Framework Core 8 |
| Background Jobs | .NET IHostedService workers |
| Maps | Leaflet.js + OpenStreetMap + OpenSeaMap |
| Observability | OpenTelemetry, Prometheus, Grafana, Serilog, Seq |
| Containers | Docker, Kubernetes + HPA |

---

## Subscription Tiers

| Tier | Containers | Users | Poll Interval | Price |
|------|-----------|-------|--------------|-------|
| Free | 5 | 2 | 6h | $0 |
| Starter | 50 | 5 | 1h | $99/mo |
| Business | 500 | 25 | 15m | $399/mo |
| Enterprise | 10,000 | 200 | 5m | $999/mo |
| Custom | Unlimited | Unlimited | 1m | Custom |
