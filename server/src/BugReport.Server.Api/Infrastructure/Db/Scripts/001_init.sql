-- BugReport initial schema

CREATE TABLE IF NOT EXISTS reports (
    id              UUID PRIMARY KEY,
    project_id      TEXT NOT NULL,
    client_report_id TEXT NOT NULL,
    level           TEXT NOT NULL,
    message         TEXT NOT NULL,
    stack_trace     TEXT NULL,
    device_platform TEXT NULL,
    device_os_version TEXT NULL,
    device_model    TEXT NULL,
    device_id       TEXT NULL,
    app_version     TEXT NULL,
    occurred_at     TIMESTAMPTZ NOT NULL,
    custom_data     JSONB NULL,
    status          TEXT NOT NULL DEFAULT 'Open',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_reports_project_client
    ON reports (project_id, client_report_id);

CREATE INDEX IF NOT EXISTS ix_reports_project_status
    ON reports (project_id, status);

CREATE INDEX IF NOT EXISTS ix_reports_occurred_at
    ON reports (occurred_at DESC);

CREATE TABLE IF NOT EXISTS attachments (
    id              UUID PRIMARY KEY,
    project_id      TEXT NOT NULL,
    file_name       TEXT NOT NULL,
    content_type    TEXT NOT NULL,
    size_bytes      BIGINT NOT NULL,
    storage_key     TEXT NOT NULL,
    url             TEXT NULL,
    etag            TEXT NULL,
    status          TEXT NOT NULL DEFAULT 'Pending', -- Pending | Completed | Failed
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_attachments_project
    ON attachments (project_id);

CREATE TABLE IF NOT EXISTS report_attachments (
    report_id       UUID NOT NULL REFERENCES reports(id) ON DELETE CASCADE,
    attachment_id   UUID NOT NULL REFERENCES attachments(id) ON DELETE CASCADE,
    PRIMARY KEY (report_id, attachment_id)
);
