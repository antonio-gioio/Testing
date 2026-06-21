-- ============================================================
-- Container Tracking SaaS — PostgreSQL Schema
-- Requires: PostGIS extension
-- ============================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS postgis;

-- ─── Subscription Tiers ────────────────────────────────────
CREATE TABLE subscription_tiers (
    id                          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name                        VARCHAR(64) NOT NULL,
    tier_type                   SMALLINT NOT NULL UNIQUE,   -- 0=Free,1=Starter,2=Business,3=Enterprise,4=Custom
    description                 TEXT,
    monthly_price_usd           NUMERIC(10,2) NOT NULL DEFAULT 0,
    annual_price_usd            NUMERIC(10,2) NOT NULL DEFAULT 0,
    max_containers              INT NOT NULL DEFAULT 5,
    max_users                   INT NOT NULL DEFAULT 2,
    max_shipments               INT NOT NULL DEFAULT 3,
    update_interval_minutes     INT NOT NULL DEFAULT 360,
    history_retention_days      INT NOT NULL DEFAULT 7,
    websocket_enabled           BOOLEAN NOT NULL DEFAULT false,
    api_rate_limit_per_hour     INT NOT NULL DEFAULT 0,
    export_enabled              BOOLEAN NOT NULL DEFAULT false,
    advanced_analytics_enabled  BOOLEAN NOT NULL DEFAULT false,
    custom_widgets_enabled      BOOLEAN NOT NULL DEFAULT false,
    max_alerts                  INT NOT NULL DEFAULT 2,
    white_label_enabled         BOOLEAN NOT NULL DEFAULT false,
    sla_level                   VARCHAR(32) NOT NULL DEFAULT 'Community',
    api_access_enabled          BOOLEAN NOT NULL DEFAULT false,
    csv_import_enabled          BOOLEAN NOT NULL DEFAULT false,
    webhook_alerts_enabled      BOOLEAN NOT NULL DEFAULT false,
    customer_tracking_links_enabled BOOLEAN NOT NULL DEFAULT false,
    is_active                   BOOLEAN NOT NULL DEFAULT true,
    is_public                   BOOLEAN NOT NULL DEFAULT true,
    display_order               INT NOT NULL DEFAULT 0,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Seed default tiers
INSERT INTO subscription_tiers (name, tier_type, description, monthly_price_usd, annual_price_usd,
    max_containers, max_users, max_shipments, update_interval_minutes, history_retention_days,
    websocket_enabled, api_rate_limit_per_hour, export_enabled, advanced_analytics_enabled,
    custom_widgets_enabled, max_alerts, api_access_enabled, csv_import_enabled,
    webhook_alerts_enabled, customer_tracking_links_enabled, sla_level, is_public, display_order)
VALUES
    ('Free',       0, 'Get started with basic tracking',    0,      0,      5,   2,  3,  360, 7,  false, 0,    false, false, false, 2,  false, false, false, false, 'Community', true, 0),
    ('Starter',    1, 'For growing logistics teams',        29,     290,    25,  5,  10, 60,  30, true,  500,  true,  false, false, 10, true,  true,  false, true,  'Email',     true, 1),
    ('Business',   2, 'Advanced tracking for your business',99,     990,    100, 15, 50, 15,  90, true,  2000, true,  true,  true,  50, true,  true,  true,  true,  'Priority',  true, 2),
    ('Enterprise', 3, 'Unlimited scale, custom contracts',  399,    3990,   999, 100,999, 5,   365,true,  10000,true,  true,  true,  999,true,  true,  true,  true,  'Dedicated', true, 3),
    ('Custom',     4, 'Tailored to your needs',             0,      0,      999, 100,999, 5,   365,true,  10000,true,  true,  true,  999,true,  true,  true,  true,  'SLA',       false,4);

-- ─── Organizations ──────────────────────────────────────────
CREATE TABLE organizations (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name            VARCHAR(256) NOT NULL,
    slug            VARCHAR(128) NOT NULL UNIQUE,
    logo_url        TEXT,
    primary_color   VARCHAR(16),
    website_url     TEXT,
    contact_email   VARCHAR(256) NOT NULL,
    contact_phone   VARCHAR(32),
    address         TEXT,
    country         VARCHAR(2),
    is_active       BOOLEAN NOT NULL DEFAULT true,
    is_white_label  BOOLEAN NOT NULL DEFAULT false,
    custom_domain   VARCHAR(256),
    metadata        JSONB NOT NULL DEFAULT '{}',
    is_deleted      BOOLEAN NOT NULL DEFAULT false,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_organizations_slug ON organizations(slug);
CREATE INDEX idx_organizations_active ON organizations(is_active) WHERE NOT is_deleted;

-- ─── Users ─────────────────────────────────────────────────
CREATE TABLE "Users" (
    "Id"                    UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "UserName"              VARCHAR(256),
    "NormalizedUserName"    VARCHAR(256) UNIQUE,
    "Email"                 VARCHAR(256),
    "NormalizedEmail"       VARCHAR(256) UNIQUE,
    "EmailConfirmed"        BOOLEAN NOT NULL DEFAULT false,
    "PasswordHash"          TEXT,
    "SecurityStamp"         TEXT,
    "ConcurrencyStamp"      TEXT,
    "PhoneNumber"           TEXT,
    "PhoneNumberConfirmed"  BOOLEAN NOT NULL DEFAULT false,
    "TwoFactorEnabled"      BOOLEAN NOT NULL DEFAULT false,
    "LockoutEnd"            TIMESTAMPTZ,
    "LockoutEnabled"        BOOLEAN NOT NULL DEFAULT false,
    "AccessFailedCount"     INT NOT NULL DEFAULT 0,
    first_name              VARCHAR(128) NOT NULL DEFAULT '',
    last_name               VARCHAR(128) NOT NULL DEFAULT '',
    avatar_url              TEXT,
    time_zone               VARCHAR(64) DEFAULT 'UTC',
    language                VARCHAR(8) DEFAULT 'en',
    is_platform_admin       BOOLEAN NOT NULL DEFAULT false,
    last_login_at           TIMESTAMPTZ,
    is_active               BOOLEAN NOT NULL DEFAULT true,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── User ↔ Organization link ───────────────────────────────
CREATE TABLE user_organizations (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id             UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    organization_id     UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    role                VARCHAR(64) NOT NULL DEFAULT 'Viewer',
    is_active           BOOLEAN NOT NULL DEFAULT true,
    invited_at          TIMESTAMPTZ,
    accepted_at         TIMESTAMPTZ,
    invited_by_user_id  UUID,
    is_deleted          BOOLEAN NOT NULL DEFAULT false,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, organization_id)
);
CREATE INDEX idx_user_orgs_org ON user_organizations(organization_id) WHERE is_active AND NOT is_deleted;
CREATE INDEX idx_user_orgs_user ON user_organizations(user_id) WHERE is_active AND NOT is_deleted;

-- ─── Organization Subscriptions ─────────────────────────────
CREATE TABLE organization_subscriptions (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id         UUID NOT NULL UNIQUE REFERENCES organizations(id),
    subscription_tier_id    UUID NOT NULL REFERENCES subscription_tiers(id),
    start_date              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    end_date                TIMESTAMPTZ,
    is_annual               BOOLEAN NOT NULL DEFAULT false,
    external_subscription_id VARCHAR(256),
    status                  VARCHAR(32) NOT NULL DEFAULT 'Active',
    trial_end_date          TIMESTAMPTZ,
    is_trial                BOOLEAN NOT NULL DEFAULT false,
    custom_limit_overrides  JSONB NOT NULL DEFAULT '{}',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── Tier Usage Counters ────────────────────────────────────
CREATE TABLE tier_usage_counters (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id         UUID NOT NULL UNIQUE REFERENCES organizations(id),
    active_containers       INT NOT NULL DEFAULT 0,
    active_shipments        INT NOT NULL DEFAULT 0,
    active_users            INT NOT NULL DEFAULT 0,
    active_alerts           INT NOT NULL DEFAULT 0,
    api_calls_this_hour     INT NOT NULL DEFAULT 0,
    api_calls_today         INT NOT NULL DEFAULT 0,
    exports_this_month      INT NOT NULL DEFAULT 0,
    last_api_call_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_reset              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── Vessels ───────────────────────────────────────────────
CREATE TABLE vessels (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    imo                     VARCHAR(20) NOT NULL UNIQUE,
    mmsi                    VARCHAR(20),
    name                    VARCHAR(256) NOT NULL,
    call_sign               VARCHAR(20),
    flag                    VARCHAR(4),
    type                    VARCHAR(64),
    shipping_line           VARCHAR(128),
    length                  NUMERIC(8,2),
    beam                    NUMERIC(8,2),
    draught                 NUMERIC(8,2),
    year_built              SMALLINT,
    current_position        geometry(Point, 4326),
    speed_knots             NUMERIC(6,2),
    heading                 NUMERIC(6,2),
    navigation_status       VARCHAR(64),
    destination             VARCHAR(128),
    estimated_arrival       TIMESTAMPTZ,
    last_position_update    TIMESTAMPTZ,
    last_known_port         VARCHAR(64),
    is_deleted              BOOLEAN NOT NULL DEFAULT false,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_vessels_imo ON vessels(imo);
CREATE INDEX idx_vessels_mmsi ON vessels(mmsi);
CREATE INDEX idx_vessels_position ON vessels USING gist(current_position) WHERE current_position IS NOT NULL;

-- ─── Shipments ─────────────────────────────────────────────
CREATE TABLE shipments (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id     UUID NOT NULL REFERENCES organizations(id),
    reference           VARCHAR(128) NOT NULL,
    description         TEXT,
    status              SMALLINT NOT NULL DEFAULT 0,
    carrier_id          VARCHAR(10),
    carrier_name        VARCHAR(128),
    service_name        VARCHAR(128),
    origin_port         VARCHAR(128),
    origin_port_code    VARCHAR(10),
    destination_port    VARCHAR(128),
    destination_port_code VARCHAR(10),
    estimated_departure TIMESTAMPTZ,
    actual_departure    TIMESTAMPTZ,
    estimated_arrival   TIMESTAMPTZ,
    actual_arrival      TIMESTAMPTZ,
    voyage_number       VARCHAR(32),
    vessel_id           UUID REFERENCES vessels(id),
    incoterms           VARCHAR(8),
    purchase_order_number VARCHAR(64),
    notes               TEXT,
    is_delay_detected   BOOLEAN NOT NULL DEFAULT false,
    delay_days          INT,
    created_by_user_id  UUID,
    custom_fields       JSONB NOT NULL DEFAULT '{}',
    is_deleted          BOOLEAN NOT NULL DEFAULT false,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_shipments_org ON shipments(organization_id) WHERE NOT is_deleted;
CREATE INDEX idx_shipments_status ON shipments(organization_id, status) WHERE NOT is_deleted;

-- ─── Containers ────────────────────────────────────────────
CREATE TABLE containers (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id         UUID NOT NULL REFERENCES organizations(id),
    container_number        VARCHAR(20) NOT NULL,
    status                  SMALLINT NOT NULL DEFAULT 0,
    size_type               VARCHAR(10),
    iso_type                VARCHAR(10),
    cargo_description       TEXT,
    weight_kg               NUMERIC(10,2),
    is_hazmat               BOOLEAN NOT NULL DEFAULT false,
    hazmat_class            VARCHAR(16),
    is_reefer_required      BOOLEAN NOT NULL DEFAULT false,
    temperature_set_point   NUMERIC(5,1),
    shipment_id             UUID REFERENCES shipments(id),
    bill_of_lading_id       UUID,
    current_location        VARCHAR(256),
    current_port_code       VARCHAR(10),
    current_position        geometry(Point, 4326),
    last_event_at           TIMESTAMPTZ,
    eta_destination         TIMESTAMPTZ,
    actual_arrival          TIMESTAMPTZ,
    is_tracking_active      BOOLEAN NOT NULL DEFAULT true,
    last_polled_at          TIMESTAMPTZ,
    last_provider_response  TEXT,
    public_tracking_token   VARCHAR(64),
    custom_fields           JSONB NOT NULL DEFAULT '{}',
    is_deleted              BOOLEAN NOT NULL DEFAULT false,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(organization_id, container_number)
);
CREATE INDEX idx_containers_org ON containers(organization_id) WHERE NOT is_deleted;
CREATE INDEX idx_containers_status ON containers(organization_id, status) WHERE NOT is_deleted;
CREATE UNIQUE INDEX idx_containers_tracking_token ON containers(public_tracking_token) WHERE public_tracking_token IS NOT NULL;
CREATE INDEX idx_containers_position ON containers USING gist(current_position) WHERE current_position IS NOT NULL;

-- ─── Bills of Lading ───────────────────────────────────────
CREATE TABLE bills_of_lading (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id     UUID NOT NULL REFERENCES organizations(id),
    bol_number          VARCHAR(50) NOT NULL,
    carrier_name        VARCHAR(128),
    carrier_id          VARCHAR(10),
    shipment_id         UUID REFERENCES shipments(id),
    consignee_name      VARCHAR(256),
    shipper_name        VARCHAR(256),
    notify_party_name   VARCHAR(256),
    origin_port         VARCHAR(128),
    origin_port_code    VARCHAR(10),
    destination_port    VARCHAR(128),
    destination_port_code VARCHAR(10),
    issue_date          DATE,
    is_seawaybill       BOOLEAN NOT NULL DEFAULT false,
    status              VARCHAR(32),
    document_url        TEXT,
    is_deleted          BOOLEAN NOT NULL DEFAULT false,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_bol_org ON bills_of_lading(organization_id, bol_number) WHERE NOT is_deleted;
ALTER TABLE containers ADD CONSTRAINT fk_containers_bol FOREIGN KEY (bill_of_lading_id) REFERENCES bills_of_lading(id);

-- ─── Tracking Events ───────────────────────────────────────
CREATE TABLE tracking_events (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id         UUID NOT NULL REFERENCES organizations(id),
    event_type              SMALLINT NOT NULL,
    provider_type           SMALLINT NOT NULL,
    provider_name           VARCHAR(64),
    provider_id             VARCHAR(64),
    external_event_id       VARCHAR(128),
    event_time              TIMESTAMPTZ NOT NULL,
    received_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    location                VARCHAR(256),
    location_code           VARCHAR(10),
    country_code            VARCHAR(4),
    geo_position            geometry(Point, 4326),
    description             TEXT NOT NULL DEFAULT '',
    voyage_number           VARCHAR(32),
    vessel_name             VARCHAR(256),
    vessel_imo              VARCHAR(20),
    speed_knots             NUMERIC(6,2),
    heading                 NUMERIC(6,2),
    container_status_after  SMALLINT,
    container_id            UUID REFERENCES containers(id),
    shipment_id             UUID REFERENCES shipments(id),
    bill_of_lading_id       UUID REFERENCES bills_of_lading(id),
    vessel_id               UUID REFERENCES vessels(id),
    raw_data                JSONB NOT NULL DEFAULT '{}',
    is_processed            BOOLEAN NOT NULL DEFAULT false,
    is_deleted              BOOLEAN NOT NULL DEFAULT false,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_events_org ON tracking_events(organization_id, event_time DESC) WHERE NOT is_deleted;
CREATE INDEX idx_events_container ON tracking_events(container_id, event_time DESC) WHERE NOT is_deleted;
CREATE INDEX idx_events_shipment ON tracking_events(shipment_id, event_time DESC) WHERE NOT is_deleted;
CREATE INDEX idx_events_external ON tracking_events(external_event_id, provider_type);
CREATE INDEX idx_events_position ON tracking_events USING gist(geo_position) WHERE geo_position IS NOT NULL;

-- ─── Tracking Providers ────────────────────────────────────
CREATE TABLE tracking_providers (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name                VARCHAR(128) NOT NULL,
    code                VARCHAR(32) NOT NULL UNIQUE,
    provider_type       SMALLINT NOT NULL,
    description         TEXT,
    base_url            TEXT,
    is_enabled          BOOLEAN NOT NULL DEFAULT true,
    is_global           BOOLEAN NOT NULL DEFAULT false,
    requires_credentials BOOLEAN NOT NULL DEFAULT true,
    poll_interval_minutes INT NOT NULL DEFAULT 60,
    priority            INT NOT NULL DEFAULT 0,
    config_schema       JSONB NOT NULL DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE provider_credentials (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id         UUID NOT NULL REFERENCES organizations(id),
    tracking_provider_id    UUID NOT NULL REFERENCES tracking_providers(id),
    encrypted_api_key       TEXT NOT NULL DEFAULT '',
    encrypted_api_secret    TEXT,
    encrypted_config        JSONB NOT NULL DEFAULT '{}',
    is_enabled              BOOLEAN NOT NULL DEFAULT true,
    last_used_at            TIMESTAMPTZ,
    valid_until             TIMESTAMPTZ,
    is_deleted              BOOLEAN NOT NULL DEFAULT false,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_credentials_org ON provider_credentials(organization_id, tracking_provider_id) WHERE NOT is_deleted;

-- ─── Dashboard Layouts & Widgets ───────────────────────────
CREATE TABLE dashboard_layouts (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id         UUID NOT NULL REFERENCES organizations(id),
    user_id                 UUID REFERENCES "Users"("Id"),
    name                    VARCHAR(128) NOT NULL DEFAULT 'Default Dashboard',
    is_default              BOOLEAN NOT NULL DEFAULT false,
    is_organization_default BOOLEAN NOT NULL DEFAULT false,
    columns                 INT NOT NULL DEFAULT 12,
    theme                   VARCHAR(32),
    is_public               BOOLEAN NOT NULL DEFAULT false,
    is_deleted              BOOLEAN NOT NULL DEFAULT false,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_dashboards_org ON dashboard_layouts(organization_id) WHERE NOT is_deleted;
CREATE INDEX idx_dashboards_user ON dashboard_layouts(organization_id, user_id) WHERE NOT is_deleted;

CREATE TABLE dashboard_widgets (
    id                          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    dashboard_layout_id         UUID NOT NULL REFERENCES dashboard_layouts(id) ON DELETE CASCADE,
    widget_type                 SMALLINT NOT NULL,
    title                       VARCHAR(128) NOT NULL,
    col                         INT NOT NULL DEFAULT 0,
    row                         INT NOT NULL DEFAULT 0,
    width                       INT NOT NULL DEFAULT 4,
    height                      INT NOT NULL DEFAULT 4,
    is_visible                  BOOLEAN NOT NULL DEFAULT true,
    refresh_interval_seconds    INT NOT NULL DEFAULT 60,
    config                      JSONB NOT NULL DEFAULT '{}',
    is_deleted                  BOOLEAN NOT NULL DEFAULT false,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_widgets_layout ON dashboard_widgets(dashboard_layout_id) WHERE NOT is_deleted;

-- ─── Alerts ────────────────────────────────────────────────
CREATE TABLE alerts (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id         UUID NOT NULL REFERENCES organizations(id),
    name                    VARCHAR(128) NOT NULL,
    trigger_type            SMALLINT NOT NULL,
    severity                SMALLINT NOT NULL DEFAULT 1,
    is_enabled              BOOLEAN NOT NULL DEFAULT true,
    conditions              JSONB NOT NULL DEFAULT '{}',
    notification_channels   JSONB NOT NULL DEFAULT '[]',
    recipient_emails        JSONB NOT NULL DEFAULT '[]',
    webhook_url             TEXT,
    webhook_secret          TEXT,
    container_id            UUID REFERENCES containers(id),
    shipment_id             UUID REFERENCES shipments(id),
    cooldown_minutes        INT NOT NULL DEFAULT 60,
    last_triggered_at       TIMESTAMPTZ,
    trigger_count           INT NOT NULL DEFAULT 0,
    is_deleted              BOOLEAN NOT NULL DEFAULT false,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_alerts_org ON alerts(organization_id) WHERE is_enabled AND NOT is_deleted;

-- ─── Notifications ─────────────────────────────────────────
CREATE TABLE notifications (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    alert_id            UUID REFERENCES alerts(id),
    organization_id     UUID NOT NULL,
    user_id             UUID REFERENCES "Users"("Id"),
    title               VARCHAR(256) NOT NULL,
    message             TEXT NOT NULL,
    severity            SMALLINT NOT NULL DEFAULT 0,
    channel             SMALLINT NOT NULL,
    status              SMALLINT NOT NULL DEFAULT 0,
    related_entity_type VARCHAR(64),
    related_entity_id   UUID,
    sent_at             TIMESTAMPTZ,
    read_at             TIMESTAMPTZ,
    failure_reason      TEXT,
    metadata            JSONB NOT NULL DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_notifications_user ON notifications(user_id, status);
CREATE INDEX idx_notifications_org ON notifications(organization_id, created_at DESC);

-- ─── API Keys ──────────────────────────────────────────────
CREATE TABLE api_keys (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id     UUID NOT NULL REFERENCES organizations(id),
    name                VARCHAR(128) NOT NULL,
    key_prefix          VARCHAR(16) NOT NULL,
    hashed_key          TEXT NOT NULL,
    scopes              JSONB NOT NULL DEFAULT '[]',
    is_active           BOOLEAN NOT NULL DEFAULT true,
    expires_at          TIMESTAMPTZ,
    last_used_at        TIMESTAMPTZ,
    created_by_user_id  UUID,
    call_count          INT NOT NULL DEFAULT 0,
    allowed_ip_addresses TEXT,
    is_deleted          BOOLEAN NOT NULL DEFAULT false,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_api_keys_org ON api_keys(organization_id) WHERE is_active AND NOT is_deleted;
CREATE INDEX idx_api_keys_prefix ON api_keys(key_prefix) WHERE is_active AND NOT is_deleted;

-- ─── Audit Logs ────────────────────────────────────────────
CREATE TABLE audit_logs (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID,
    user_id         UUID,
    action          VARCHAR(64) NOT NULL,
    entity_type     VARCHAR(64) NOT NULL,
    entity_id       UUID,
    old_values      JSONB,
    new_values      JSONB,
    ip_address      INET,
    user_agent      TEXT,
    request_path    TEXT,
    http_method     VARCHAR(10),
    success         BOOLEAN NOT NULL DEFAULT true,
    failure_reason  TEXT,
    metadata        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_audit_org ON audit_logs(organization_id, created_at DESC);
CREATE INDEX idx_audit_entity ON audit_logs(entity_type, entity_id);
CREATE INDEX idx_audit_user ON audit_logs(user_id, created_at DESC);

-- ─── Export Jobs ───────────────────────────────────────────
CREATE TABLE export_jobs (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id     UUID NOT NULL REFERENCES organizations(id),
    format              SMALLINT NOT NULL,
    data_type           SMALLINT NOT NULL,
    status              SMALLINT NOT NULL DEFAULT 0,
    requested_by_user_id UUID,
    filters             JSONB NOT NULL DEFAULT '{}',
    file_path           TEXT,
    file_url            TEXT,
    file_size_bytes     BIGINT,
    record_count        INT,
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    expires_at          TIMESTAMPTZ,
    failure_reason      TEXT,
    download_token      VARCHAR(64),
    download_count      INT NOT NULL DEFAULT 0,
    max_downloads       INT NOT NULL DEFAULT 5,
    is_deleted          BOOLEAN NOT NULL DEFAULT false,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_export_jobs_org ON export_jobs(organization_id, created_at DESC);
CREATE INDEX idx_export_jobs_token ON export_jobs(download_token) WHERE download_token IS NOT NULL;

-- ─── Functions & Triggers ──────────────────────────────────
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$ BEGIN NEW.updated_at = NOW(); RETURN NEW; END; $$ language 'plpgsql';

DO $$ DECLARE
    t TEXT;
BEGIN
    FOR t IN SELECT table_name FROM information_schema.tables
             WHERE table_schema = 'public' AND table_name IN (
                 'organizations','user_organizations','subscription_tiers',
                 'organization_subscriptions','tier_usage_counters','vessels',
                 'shipments','containers','bills_of_lading','tracking_events',
                 'tracking_providers','provider_credentials','dashboard_layouts',
                 'dashboard_widgets','alerts','api_keys','export_jobs')
    LOOP
        EXECUTE format('CREATE TRIGGER trg_update_%I BEFORE UPDATE ON %I FOR EACH ROW EXECUTE FUNCTION update_updated_at_column()', t, t);
    END LOOP;
END $$;
