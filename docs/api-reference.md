# API Reference

**Base URL:** `http://localhost:5001`  
**Interactive docs:** `http://localhost:5001/swagger`

All authenticated endpoints require a `Bearer <token>` header. Tokens are obtained from the `/api/v1/auth/login` endpoint or passed as `?access_token=<token>` for WebSocket connections.

---

## Authentication

### Register

```
POST /api/v1/auth/register
```

Creates a new user and organization. Returns a JWT on success.

**Body:**
```json
{
  "firstName": "Jane",
  "lastName": "Smith",
  "email": "jane@company.com",
  "password": "SecurePass123!",
  "organizationName": "Acme Logistics"
}
```

**Response `200`:**
```json
{
  "token": "eyJ...",
  "expiresAt": "2025-01-20T00:00:00Z",
  "userId": "...",
  "organizationId": "...",
  "role": "OrganizationAdmin"
}
```

---

### Login

```
POST /api/v1/auth/login
```

**Body:**
```json
{
  "email": "jane@company.com",
  "password": "SecurePass123!"
}
```

**Response `200`:** Same structure as register.

---

## Containers

### List Containers

```
GET /api/v1/containers?page=1&pageSize=20&status=InTransit&search=MSCU
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `page` | int | Page number (default 1) |
| `pageSize` | int | Items per page (default 20, max 100) |
| `status` | string | Filter by status (e.g. `InTransit`, `Delivered`) |
| `search` | string | Search by container number |
| `shipmentId` | guid | Filter by shipment |

**Response `200`:**
```json
{
  "items": [
    {
      "id": "...",
      "containerNumber": "MSCU1234567",
      "status": "InTransit",
      "currentLocation": "Suez Canal",
      "etaDestination": "2025-01-20T00:00:00Z",
      "lastEventAt": "2025-01-15T10:30:00Z",
      "shipmentReference": "SHP-2025-001",
      "hasPublicLink": false
    }
  ],
  "total": 42,
  "page": 1,
  "totalPages": 3
}
```

---

### Get Container

```
GET /api/v1/containers/{id}
```

Returns full container detail including recent tracking events.

---

### Create Container

```
POST /api/v1/containers
```

**Required role:** `LogisticsManager` or above.

**Body:**
```json
{
  "containerNumber": "MSCU1234567",
  "sizeType": "40ft Dry",
  "cargoDescription": "Electronics",
  "shipmentId": "...",
  "enablePublicLink": true
}
```

---

### Add Manual Tracking Event

```
POST /api/v1/containers/{id}/events
```

**Body:**
```json
{
  "eventType": "ContainerLoaded",
  "description": "Container loaded onto MSC Oscar",
  "location": "Port of Shanghai",
  "eventTime": "2025-01-10T08:00:00Z"
}
```

**Valid `eventType` values:** `ManualUpdate`, `ContainerGateIn`, `ContainerLoaded`, `VesselDeparted`, `TransshipmentArrived`, `VesselArrived`, `ContainerDischarged`, `ContainerGateOut`, `ContainerDelivered`, `CustomsCleared`

---

### Get Container Events

```
GET /api/v1/containers/{id}/events?page=1&pageSize=50
```

---

### Public Tracking (no auth)

```
GET /track/{publicTrackingToken}
```

Returns limited container status for sharing with customers or partners. No authentication required.

---

### Delete Container

```
DELETE /api/v1/containers/{id}
```

Soft-deletes the container and deactivates tracking.

---

## Shipments

### List Shipments

```
GET /api/v1/shipments?page=1&pageSize=12&status=InTransit
```

---

### Get Shipment

```
GET /api/v1/shipments/{id}
```

---

### Get Shipment Containers

```
GET /api/v1/shipments/{id}/containers
```

---

### Create Shipment

```
POST /api/v1/shipments
```

**Body:**
```json
{
  "reference": "SHP-2025-001",
  "description": "Electronics Q1 import",
  "carrierName": "Maersk",
  "originPort": "Shanghai",
  "originPortCode": "CNSHA",
  "destinationPort": "Rotterdam",
  "destinationPortCode": "NLRTM",
  "estimatedDeparture": "2025-01-05T00:00:00Z",
  "estimatedArrival": "2025-02-10T00:00:00Z",
  "voyageNumber": "123E",
  "incoterms": "CIF"
}
```

---

### Import Containers from CSV

```
POST /api/v1/shipments/{id}/import-csv
Content-Type: multipart/form-data
```

Upload a CSV file with a `containerNumber` column. Max 5 MB. Requires the `CsvImportEnabled` feature on the org's tier.

---

## Bills of Lading

### List B/Ls

```
GET /api/v1/bills-of-lading?page=1&pageSize=20&search=MAEU
```

---

### Get B/L

```
GET /api/v1/bills-of-lading/{id}
```

---

### Get B/L by Number

```
GET /api/v1/bills-of-lading/number/{bolNumber}
```

---

### Create B/L

```
POST /api/v1/bills-of-lading
```

**Body:**
```json
{
  "bolNumber": "MAEU1234567890",
  "carrierName": "Maersk",
  "shipperName": "Acme Corp",
  "consigneeName": "EuroTrade GmbH",
  "notifyPartyName": "Freight Broker Ltd",
  "originPort": "Shanghai",
  "originPortCode": "CNSHA",
  "destinationPort": "Rotterdam",
  "destinationPortCode": "NLRTM",
  "issueDate": "2025-01-05"
}
```

---

## Vessels

### List Vessels

```
GET /api/v1/vessels?page=1&pageSize=20&search=MSC+Oscar
```

---

### Get Vessel by IMO

```
GET /api/v1/vessels/imo/{imoNumber}
```

---

### Get Vessel by MMSI

```
GET /api/v1/vessels/mmsi/{mmsi}
```

---

### Refresh AIS Position

```
POST /api/v1/vessels/{id}/refresh-position
```

Forces an immediate AIS position update for the vessel.

---

## Alerts

### List Alert Rules

```
GET /api/v1/alerts
```

---

### Create Alert Rule

```
POST /api/v1/alerts
```

**Body:**
```json
{
  "name": "Delay Detected",
  "triggerType": "DelayDetected",
  "severity": "Warning",
  "conditions": {},
  "notificationChannels": ["InApp", "Email"],
  "recipientEmails": ["ops@company.com"],
  "webhookUrl": null,
  "cooldownMinutes": 60
}
```

**Valid `triggerType` values:** `StatusChange`, `EtaChanged`, `DelayDetected`, `PortArrival`, `PortDeparture`, `VesselDiverted`

**Valid `severity` values:** `Info`, `Warning`, `Critical`

---

### Toggle Alert

```
PATCH /api/v1/alerts/{id}/toggle
```

**Body:** `{ "isEnabled": false }`

---

### Get Notifications

```
GET /api/v1/alerts/notifications
```

---

### Mark Notification Read

```
POST /api/v1/alerts/notifications/{id}/read
```

---

### Mark All Notifications Read

```
POST /api/v1/alerts/notifications/read-all
```

---

## Dashboard

### Get Summary KPIs

```
GET /api/v1/dashboard/summary
```

Returns container counts by status, active shipments, unread alerts, etc.

---

### Get Map Data

```
GET /api/v1/dashboard/map-data
```

Returns current container positions and vessel positions for the live map.

---

### Get Container Status Breakdown

```
GET /api/v1/dashboard/containers-by-status
```

Returns counts per status for charts.

---

## Tracking Events

### List Events (org-wide)

```
GET /api/v1/tracking-events?limit=50&containerId=...&shipmentId=...&eventType=VesselArrived
```

---

## Exports

### List Export Jobs

```
GET /api/v1/exports
```

---

### Queue Export

```
POST /api/v1/exports
```

**Body:**
```json
{
  "dataType": "Containers",
  "format": "Excel",
  "filters": {
    "dateFrom": "2025-01-01",
    "dateTo": "2025-01-31"
  }
}
```

**Valid `dataType` values:** `Containers`, `Shipments`, `TrackingEvents`, `BillsOfLading`, `Alerts`  
**Valid `format` values:** `Csv`, `Excel`, `Pdf`

Export jobs are processed asynchronously. Poll the list endpoint and download via the `downloadUrl` when `status` is `Completed`.

---

## API Keys

### List API Keys

```
GET /api/v1/api-keys
```

---

### Create API Key

```
POST /api/v1/api-keys
```

**Body:**
```json
{
  "name": "Production Integration",
  "scopes": ["containers:read", "shipments:read"]
}
```

**Response** includes the raw key in `key` — **it is shown once only and cannot be retrieved again.**

---

### Revoke API Key

```
DELETE /api/v1/api-keys/{id}
```

---

## Users

### Get Current User

```
GET /api/v1/users/me
```

---

### Update Profile

```
PUT /api/v1/users/me
```

**Body:** `{ "firstName": "Jane", "lastName": "Smith" }`

---

### Change Password

```
POST /api/v1/users/me/change-password
```

**Body:** `{ "currentPassword": "...", "newPassword": "..." }`

---

### List Org Users

```
GET /api/v1/users
```

Requires `OrgAdmin` role.

---

## Webhooks (Inbound)

Receive tracking updates pushed by 3PLs, ERPs, or carrier systems directly into your org.

### Test Endpoint

```
GET /api/v1/webhooks/{orgWebhookToken}/ping
```

Returns `{ "status": "ok" }` if the token is valid.

### Submit Events

```
POST /api/v1/webhooks/{orgWebhookToken}
X-Source: maersk
Content-Type: application/json
```

The `X-Source` header hints at the data format (`maersk`, `msc`, `hapag`, `carrier`, `port`, or `generic`).

Your org's `webhookIngestToken` is shown in Settings → Organization.

---

## Real-Time (SignalR)

Connect to the SignalR hub at:

```
ws://localhost:5001/hubs/tracking?access_token=<jwt>
```

### Subscribe to container updates

```javascript
connection.invoke("SubscribeToContainer", containerId);
```

### Events

| Event | Payload |
|-------|---------|
| `ContainerUpdated` | `{ containerId, containerNumber, status, location, timestamp }` |
| `AlertNotification` | `{ title, message, severity, alertId }` |
| `VesselPositionUpdated` | `{ imo, name, latitude, longitude, speedKnots, heading, timestamp }` |

---

## Authentication Methods

### Bearer Token (JWT)

```
Authorization: Bearer eyJ...
```

### API Key

```
X-API-Key: ct_abc123...
```

or

```
Authorization: ApiKey ct_abc123...
```

---

## Rate Limits

| Endpoint group | Limit |
|----------------|-------|
| `/api/v1/auth/*` | 10 requests / 5 minutes |
| All other endpoints | 100 requests / minute (Free tier) |

Tier-based limits are also enforced server-side. `402 Payment Required` is returned when a tier limit is exceeded.

---

## Error Responses

All errors follow this structure:

```json
{
  "error": "Human-readable message",
  "detail": "Optional technical detail"
}
```

| Status | Meaning |
|--------|---------|
| `400` | Validation error or bad request |
| `401` | Missing or invalid authentication |
| `402` | Tier limit exceeded |
| `403` | Insufficient role/permission |
| `404` | Resource not found |
| `409` | Conflict (e.g. duplicate container number) |
| `429` | Rate limit exceeded |
| `500` | Internal server error |
| `502` | Upstream provider error (AIS, carrier API) |
