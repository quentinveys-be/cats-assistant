CREATE TABLE calendar_events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    "start" TEXT,
    "end" TEXT,
    subject TEXT,
    organizer TEXT
);

CREATE TABLE vcs_commits (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ts TEXT,
    repo TEXT,
    branch TEXT,
    message TEXT,
    jira_key TEXT
);

CREATE TABLE jira_tickets (
    "key" TEXT PRIMARY KEY,
    summary TEXT,
    status TEXT,
    imputation_code_raw TEXT,
    posid TEXT,
    zwpid TEXT,
    effort REAL,
    last_sync TEXT
);

CREATE TABLE time_blocks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT,
    "start" TEXT,
    "end" TEXT,
    source_summary TEXT,
    jira_key TEXT,
    posid TEXT,
    zwpid TEXT,
    note TEXT,
    duration_hours REAL,
    status TEXT,
    sap_counter TEXT
);

CREATE TABLE rules (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    matcher_kind TEXT,
    matcher_value TEXT,
    target TEXT,
    priority INTEGER,
    origin TEXT
);
