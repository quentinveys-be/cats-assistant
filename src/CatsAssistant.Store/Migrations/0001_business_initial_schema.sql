CREATE TABLE jira_tickets (
    "key" TEXT PRIMARY KEY NOT NULL,
    summary TEXT,
    status TEXT,
    context TEXT,
    imputation_code_raw TEXT,
    posid TEXT,
    zwpid TEXT,
    effort REAL,
    last_sync TEXT NOT NULL
);

CREATE TABLE vcs_commits (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sha TEXT NOT NULL,
    ts TEXT NOT NULL,
    repo TEXT NOT NULL,
    branch TEXT NOT NULL,
    message TEXT NOT NULL,
    jira_key TEXT,
    UNIQUE (sha, repo)
);

CREATE TABLE calendar_events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    "start" TEXT NOT NULL,
    "end" TEXT NOT NULL,
    subject TEXT NOT NULL,
    organizer TEXT
);

CREATE TABLE time_blocks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT NOT NULL,
    "start" TEXT NOT NULL,
    "end" TEXT NOT NULL,
    source_summary TEXT NOT NULL,
    jira_key TEXT,
    posid TEXT NOT NULL,
    zwpid TEXT NOT NULL,
    note TEXT NOT NULL,
    duration_hours REAL NOT NULL,
    status TEXT NOT NULL,
    sap_counter TEXT
);

CREATE TABLE rules (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    matcher_kind TEXT NOT NULL,
    matcher_value TEXT NOT NULL,
    target TEXT NOT NULL,
    priority INTEGER NOT NULL,
    origin TEXT NOT NULL
);

CREATE INDEX ix_vcs_commits_ts ON vcs_commits (ts);
CREATE INDEX ix_calendar_events_start ON calendar_events ("start");
