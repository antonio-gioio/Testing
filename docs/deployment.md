# Deployment Guide

---

## Option 1: Docker Compose (Recommended for development / small deployments)

### Prerequisites

- Docker 24+
- Docker Compose v2+

### Steps

```bash
# Clone the repository
git clone https://github.com/antonio-gioio/testing
cd testing

# (Optional) Override environment variables
cp deploy/.env.example deploy/.env
nano deploy/.env

# Start all services
docker compose -f deploy/docker-compose.yml up -d

# Check logs
docker compose -f deploy/docker-compose.yml logs -f ct-api

# Stop all services
docker compose -f deploy/docker-compose.yml down
```

### Services started

| Service | Port | Description |
|---------|------|-------------|
| `ct-api` | 5001 | ASP.NET Core REST API |
| `ct-web` | 5000 | Blazor WebAssembly frontend |
| `postgres` | 5432 | PostgreSQL + PostGIS |
| `redis` | 6379 | Redis cache + SignalR backplane |
| `seq` | 5341 | Structured log viewer |
| `prometheus` | 9090 | Metrics scraping |
| `grafana` | 3000 | Dashboards (admin/admin) |

The API automatically applies migrations and seeds subscription tiers on startup. No manual SQL is required with Docker.

---

## Option 2: Kubernetes (Production)

### Prerequisites

- Kubernetes 1.28+
- `kubectl` configured
- `cert-manager` for TLS (or remove TLS from ingress)
- A PostgreSQL cluster (CloudSQL, RDS, or managed PostgreSQL)
- A Redis cluster or ElastiCache
- A container registry for your images

### Build and push images

```bash
# API
docker build -f src/ContainerTracking.Api/Dockerfile -t your-registry/ct-api:latest .
docker push your-registry/ct-api:latest

# Web
docker build -f src/ContainerTracking.Web/Dockerfile -t your-registry/ct-web:latest .
docker push your-registry/ct-web:latest
```

### Create secrets

```bash
kubectl create secret generic ct-secrets \
  --from-literal=db-password='YourDbPassword' \
  --from-literal=jwt-key='your-32-character-secret-key-here!'
```

### Apply manifests

```bash
kubectl apply -f deploy/k8s/
```

### Manifests overview

| File | Description |
|------|-------------|
| `api-deployment.yaml` | API deployment (3 replicas) + HPA (2–10 pods) |
| `web-deployment.yaml` | Web frontend deployment |
| `ingress.yaml` | nginx ingress with WebSocket support and TLS |
| `configmap.yaml` | Non-secret environment configuration |
| `pvc.yaml` | ReadWriteMany volume for export files |

### Update the domain

Edit `deploy/k8s/ingress.yaml` and replace `yourdomain.com` with your actual domain.

### HPA behaviour

The API pod scales between **2 and 10 replicas** based on:
- CPU > 70%
- Memory > 80%

SignalR uses Redis as the backplane, so all pods share real-time state regardless of which pod handles a given connection.

---

## Option 3: Bare Metal / VM

### 1. Install prerequisites

```bash
# Ubuntu 22.04 example
sudo apt install -y postgresql-15 postgresql-15-postgis-3 redis-server
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 8.0
```

### 2. Create database

```bash
sudo -u postgres psql << EOF
CREATE USER ctuser WITH PASSWORD 'yourpassword';
CREATE DATABASE containertrack OWNER ctuser;
\c containertrack
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
GRANT ALL ON SCHEMA public TO ctuser;
EOF
```

### 3. Apply schema

```bash
psql -U ctuser -d containertrack -f sql/schema.sql
```

### 4. Publish the API

```bash
cd src/ContainerTracking.Api
dotnet publish -c Release -o /opt/ct-api

# Configure environment
cat > /opt/ct-api/appsettings.Production.json << EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=containertrack;Username=ctuser;Password=yourpassword"
  },
  "Redis": "localhost:6379",
  "Jwt": {
    "Key": "your-32-char-key-here!!!!!!!!!!!!!",
    "Issuer": "ContainerTrack",
    "Audience": "ContainerTrack"
  }
}
EOF
```

### 5. Create systemd service

```ini
# /etc/systemd/system/ct-api.service
[Unit]
Description=ContainerTrack API
After=network.target

[Service]
WorkingDirectory=/opt/ct-api
ExecStart=/usr/bin/dotnet ContainerTracking.Api.dll
Restart=on-failure
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5001

[Install]
WantedBy=multi-user.target
```

```bash
systemctl enable ct-api
systemctl start ct-api
```

### 6. Publish the web app

```bash
cd src/ContainerTracking.Web
dotnet publish -c Release -o /opt/ct-web

# Update API URL in wwwroot/appsettings.json
echo '{ "ApiBaseUrl": "https://api.yourdomain.com" }' > /opt/ct-web/wwwroot/appsettings.json
```

### 7. nginx config

```nginx
server {
    listen 80;
    server_name yourdomain.com;
    root /opt/ct-web/wwwroot;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}

server {
    listen 80;
    server_name api.yourdomain.com;

    location / {
        proxy_pass http://localhost:5001;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "Upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

## Production Checklist

### Security

- [ ] Replace the default JWT key with a cryptographically random 32+ character string
- [ ] Set a strong database password
- [ ] Enable HTTPS (TLS certificate via Let's Encrypt or your CA)
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production` to disable Swagger and development error detail
- [ ] Configure a firewall — PostgreSQL and Redis should NOT be publicly accessible
- [ ] Review CORS policy in `Program.cs` — restrict `AllowFrontend` to your actual frontend origin

### Performance

- [ ] Enable Redis for SignalR backplane (set `Redis` connection string)
- [ ] Run at least 2 API pods/instances behind a load balancer
- [ ] Configure PostgreSQL connection pooling (PgBouncer recommended for 100+ concurrent users)
- [ ] Set up automated PostgreSQL backups

### Observability

- [ ] Point `Seq__ServerUrl` to your Seq or ELK endpoint
- [ ] Point `OTLP__Endpoint` to your OpenTelemetry collector
- [ ] Import the Prometheus dashboard into Grafana (JSON in `deploy/grafana/`)
- [ ] Set up alerting in Grafana for API error rate > 1% and p99 latency > 2s

### Data

- [ ] Schedule a regular PostgreSQL backup job
- [ ] Set a data retention policy for `tracking_events` (they grow fast — consider partitioning by month)
- [ ] Configure the export file volume cleanup (files expire after 24h by design)

---

## Environment Variables Reference

```bash
# Database
DB_HOST=postgres
DB_PORT=5432
DB_NAME=containertrack
DB_USER=ctuser
DB_PASS=yourpassword

# Redis
REDIS=redis:6379

# JWT
JWT_KEY=your-secret-key-must-be-32-chars-minimum
JWT_ISSUER=ContainerTrack
JWT_AUDIENCE=ContainerTrack

# AIS provider (optional)
AISSTREAM_KEY=your_aisstream_io_api_key

# Observability (optional)
SEQ_URL=http://seq:5341
OTLP_ENDPOINT=http://otel-collector:4317

# Frontend
ApiBaseUrl=http://localhost:5001
```

---

## Database Migration

EF Core migrations are applied automatically on API startup. To generate a new migration after changing entities:

```bash
cd src/ContainerTracking.Infrastructure
dotnet ef migrations add YourMigrationName \
  --startup-project ../ContainerTracking.Api \
  --output-dir Data/Migrations
```

To apply manually (without restarting the API):

```bash
cd src/ContainerTracking.Infrastructure
dotnet ef database update \
  --startup-project ../ContainerTracking.Api \
  --connection "Host=localhost;..."
```
