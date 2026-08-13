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
    sha TEXT NOT NULL UNIQUE,
    ts TEXT NOT NULL,
    repo TEXT NOT NULL,
    branch TEXT NOT NULL,
    message TEXT NOT NULL,
    jira_key TEXT
);

CREATE TABLE calendar_events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    "start" TEXT NOT NULL,
    "end" TEXT NOT NULL,
    subject TEXT NOT NULL,
    organizer TEXT
);

CREATE INDEX ix_vcs_commits_ts ON vcs_commits (ts);
CREATE INDEX ix_calendar_events_start ON calendar_events ("start");
