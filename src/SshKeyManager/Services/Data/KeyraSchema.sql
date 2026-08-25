-- KEYRA vault schema (envelope encryption + SSH inventory)

CREATE TABLE IF NOT EXISTS vault_metadata (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    username TEXT NOT NULL,
    salt BLOB NOT NULL,
    argon_memory INTEGER NOT NULL,
    argon_iterations INTEGER NOT NULL,
    argon_parallelism INTEGER NOT NULL,
    enc_dbk BLOB NOT NULL,
    dbk_nonce BLOB NOT NULL,
    dbk_tag BLOB NOT NULL,
    integrity_hmac BLOB,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE TABLE IF NOT EXISTS ssh_keys (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    key_type TEXT NOT NULL,
    public_key TEXT NOT NULL,
    fingerprint_sha256 TEXT NOT NULL,
    enc_private_key BLOB NOT NULL,
    private_key_nonce BLOB NOT NULL,
    private_key_tag BLOB NOT NULL,
    enc_passphrase BLOB,
    passphrase_nonce BLOB,
    passphrase_tag BLOB,
    comment TEXT,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE TABLE IF NOT EXISTS servers (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    host TEXT NOT NULL,
    port INTEGER NOT NULL DEFAULT 22,
    username TEXT NOT NULL,
    default_key_id TEXT,
    proxy_jump_id TEXT,
    tags TEXT,
    notes TEXT,
    auth_mode TEXT NOT NULL DEFAULT 'key',
    is_favorite INTEGER NOT NULL DEFAULT 0,
    last_connected_at TEXT,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (default_key_id) REFERENCES ssh_keys(id) ON DELETE SET NULL,
    FOREIGN KEY (proxy_jump_id) REFERENCES servers(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS connection_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    server_id TEXT NOT NULL,
    connected_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    status TEXT NOT NULL,
    error_message TEXT,
    FOREIGN KEY (server_id) REFERENCES servers(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_connection_logs_server_id ON connection_logs(server_id);
CREATE INDEX IF NOT EXISTS idx_servers_proxy_jump_id ON servers(proxy_jump_id);
