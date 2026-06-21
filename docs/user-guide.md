# User Guide

This guide walks through the ContainerTrack web application from signup through daily operations.

---

## 1. Creating Your Account

1. Open **http://localhost:5000/register** (or your deployed URL).
2. Fill in your name, work email, organization name, and a password (min 8 characters).
3. Tick the Terms & Conditions checkbox and click **Create Account — Free**.
4. You are automatically logged in and land on the Dashboard.

Your account starts on the **Free tier** (5 containers, 2 users, 6-hour polling). You can upgrade later via Settings → Subscription.

---

## 2. Dashboard

The Dashboard is your command centre. It shows:

- **KPI cards** — total containers, in-transit count, active shipments, unread alerts
- **Container Status Chart** — breakdown by current status
- **Live Map Widget** — container and vessel positions (click any marker for detail)
- **Recent Events** — latest tracking events across all containers
- **Tier Usage** — how close you are to your plan limits

Widgets can be rearranged by dragging (on Business tier and above).

---

## 3. Containers

### Adding a Container

1. Navigate to **Containers** in the sidebar.
2. Click **+ New Container**.
3. Enter the container number (ISO format, e.g. `MSCU1234567`), size/type, and optionally link it to a shipment.
4. Enable **Public Tracking Link** if you want to share a customer-facing URL without requiring a login.
5. Click **Create Container**.

### Manual Events

If carrier tracking isn't configured, or you want to record a specific milestone:

1. Open the container detail page (click any container row).
2. Click **+ Add Event**.
3. Select the event type (Gate In, Loaded, Departed, etc.), add a description and location, then save.

### Container Statuses

| Status | Meaning |
|--------|---------|
| `Unknown` | Not yet tracked |
| `Booked` | Booking confirmed, awaiting gate-in |
| `GateIn` | Container entered terminal |
| `OnVessel` | Loaded aboard vessel |
| `InTransit` | Vessel underway |
| `Transshipment` | Connecting port |
| `AtDestinationPort` | Vessel arrived at discharge port |
| `Discharged` | Container off-loaded |
| `GateOut` | Left the terminal |
| `Delivered` | Final delivery complete |
| `Empty` | Empty container returned |
| `Held` | Customs or inspection hold |
| `Delayed` | Delay detected |

### Public Tracking Link

Containers with a public link can be shared with customers via `/track/{token}`. No login is required to view this page — it shows the current status, location, and recent events.

---

## 4. Shipments

A Shipment groups multiple containers on the same voyage.

### Creating a Shipment

1. Go to **Shipments** → **+ New Shipment**.
2. Fill in the reference number, carrier, route (origin/destination ports), voyage number, and ETD/ETA.
3. Click **Create Shipment**.

### Adding Containers to a Shipment

From the shipment detail page, click **+ Add Container** and enter the container number. You can also link existing containers via their edit page.

### Importing via CSV

On Business tier and above, you can bulk-import containers into a shipment:

1. Open the shipment detail page.
2. Click **Import CSV** and upload a `.csv` file.

**CSV format:**
```csv
containerNumber,sizeType,cargoDescription
MSCU1234567,40ft Dry,Electronics
CMAU7654321,20ft Dry,Apparel
```

The first row must be a header. The `containerNumber` column is required; others are optional.

---

## 5. Live Map

Navigate to **Live Map** to see all container and vessel positions on an interactive maritime chart.

- **📦 Container markers** — click to see container number, status, and last event time.
- **🚢 Vessel markers** — click to see vessel name, speed, heading, and AIS timestamp.
- Use the layer selector (top right) to show only containers, only vessels, or both.
- The map uses **OpenStreetMap** tiles with **OpenSeaMap** nautical overlays (no API key required).

> **Note:** Container positions are updated only when a carrier API or manual event reports a location. AIS positions update every 5 minutes for vessels with active shipments.

---

## 6. Alerts & Notifications

### Creating an Alert Rule

1. Go to **Alerts** → **+ New Alert Rule**.
2. Name the rule (e.g. "Delay on arrival").
3. Choose a **Trigger**:
   - `Delay Detected` — fires when a shipment delay is flagged
   - `Status Change` — any status transition
   - `ETA Changed` — when the estimated arrival shifts
   - `Port Arrival` — vessel arrives at a port
   - `Port Departure` — vessel departs
4. Set **Severity** (Info / Warning / Critical).
5. Choose **Notification Channels**:
   - **In-App** — bell icon in the top bar
   - **Email** — enter comma-separated recipient addresses
   - **Webhook** — paste a Slack/Teams/custom webhook URL
6. Click **Create Alert**.

### Notification Bell

The 🔔 icon in the top-right shows your unread notification count. Click it to see recent alerts and mark them as read.

### Cooldown

Each alert has a **cooldown** (default 60 minutes). If the same alert fires again within the cooldown window, it is silently suppressed to avoid notification flooding.

---

## 7. Exports

1. Navigate to **Exports**.
2. Choose a **Data Type** (Containers, Shipments, Events, etc.) and **Format** (CSV, Excel, PDF).
3. Optionally set a date range.
4. Click **Queue Export**.

Exports are processed in the background. Refresh the page — when the status shows `Completed`, a **Download** link appears. Each export link can be downloaded up to 5 times within 24 hours.

> PDF exports require **Business tier** or above.

---

## 8. Bills of Lading

1. Navigate to **Bills of Lading** in the sidebar.
2. Click **+ New B/L** and enter the B/L number, carrier, shipper/consignee details, and port codes.
3. The B/L detail page shows all linked tracking events from carriers that support B/L-level querying.

---

## 9. Settings

### Organization Profile

**Settings → Organization**

- Update your org name, contact email, country, and branding.
- Find your **Webhook Ingest Token** here — give this to 3PLs or ERPs to push tracking events directly into your account.

### API Keys

**Settings → API Keys**

Create API keys for programmatic access:

1. Click **+ New API Key**.
2. Name it and select the required scopes (`containers:read`, `shipments:read`, `write`, `admin`).
3. **Copy the key immediately** — it is shown only once.

Use the key in any HTTP client:
```
X-API-Key: ct_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

## 10. Inviting Team Members

1. Go to **Settings → Team**.
2. Click **Invite User** and enter their email.
3. Assign a role:
   - **Viewer** — read-only access
   - **Logistics Manager** — create/update containers, shipments, B/Ls, add manual events
   - **Organization Admin** — full access including settings, API keys, and user management

---

## 11. Roles Summary

| Role | View | Create/Update | Delete | Settings | Admin |
|------|------|--------------|--------|----------|-------|
| Viewer | ✓ | | | | |
| Logistics Manager | ✓ | ✓ | | | |
| Organization Admin | ✓ | ✓ | ✓ | ✓ | |
| Platform Admin | ✓ | ✓ | ✓ | ✓ | ✓ |

---

## 12. Tracking Provider Setup

To enable automatic polling from carrier APIs, a platform admin must configure credentials per organization.

**Supported carriers:**

| Carrier | Provider Code | API Documentation |
|---------|--------------|-------------------|
| Maersk | `maersk` | developer.maersk.com |
| MSC | `msc` | *(stub, requires custom integration)* |
| CMA CGM | `cmacgm` | *(stub, requires custom integration)* |
| Hapag-Lloyd | `hapag` | *(stub, requires custom integration)* |

**AIS (vessel positions):**

1. Sign up at [aisstream.io](https://aisstream.io) for a free API key.
2. Set `AisStream__ApiKey` in your environment or `appsettings.json`.

The polling frequency is determined by your subscription tier (Free = 6h, Business = 15m, Enterprise = 5m).

---

## 13. Understanding Tracking Data Sources

ContainerTrack combines multiple data sources:

| Source | What it tracks | Latency |
|--------|---------------|---------|
| Carrier APIs | Container milestones (gate-in, load, discharge, delivery) | Minutes–hours |
| AIS | Vessel GPS position while at sea | Seconds–minutes |
| Port APIs | Vessel arrivals/departures, berth assignments | Hours |
| Manual entry | Any event entered by your team | Immediate |
| CSV import | Bulk historical events | Batch |
| Inbound webhooks | Push from 3PLs, ERPs, customs systems | Real-time |

**Important:** AIS tracks *vessels*, not individual containers. Once a container is discharged from a vessel, AIS can no longer locate it. Use carrier milestone events or manual updates for last-mile tracking.
