using Microsoft.Data.Sqlite;
using SshKeyManager.Services.Security;

namespace SshKeyManager.Services.Data;

public sealed class KeyraDb : IDisposable
{
    private const string SchemaSql = """
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
        """;

    private readonly VaultPaths _paths;
    private readonly object _sync = new();
    private SqliteConnection? _connection;
    private bool _disposed;

    public KeyraDb(VaultPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public string DatabasePath => _paths.DatabasePath;

    public bool DatabaseFileExists => File.Exists(_paths.DatabasePath);

    public SqliteConnection Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
        {
            if (_connection is { State: System.Data.ConnectionState.Open })
            {
                return _connection;
            }

            _paths.EnsureDirectories();
            _connection?.Dispose();
            _connection = new SqliteConnection($"Data Source={_paths.DatabasePath}");
            _connection.Open();
            using (var pragma = _connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
                pragma.ExecuteNonQuery();
            }

            using (var schema = _connection.CreateCommand())
            {
                schema.CommandText = SchemaSql;
                schema.ExecuteNonQuery();
            }

            return _connection;
        }
    }

    public void EnsureCreated()
    {
        Open();
    }

    public bool HasVaultMetadata()
    {
        if (!DatabaseFileExists)
        {
            return false;
        }

        var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM vault_metadata WHERE id = 1;";
        var result = cmd.ExecuteScalar();
        return result is long and > 0 || (result is int i && i > 0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
