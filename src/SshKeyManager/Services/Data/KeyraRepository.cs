using Microsoft.Data.Sqlite;
using SshKeyManager.Models;

namespace SshKeyManager.Services.Data;

public sealed class KeyraRepository
{
    private readonly KeyraDb _db;

    public KeyraRepository(KeyraDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public VaultMetadataRow? GetVaultMetadata()
    {
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT username, salt, argon_memory, argon_iterations, argon_parallelism,
                   enc_dbk, dbk_nonce, dbk_tag, integrity_hmac, created_at
            FROM vault_metadata WHERE id = 1;
            """;

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new VaultMetadataRow
        {
            Username = reader.GetString(0),
            Salt = (byte[])reader[1],
            ArgonMemory = reader.GetInt32(2),
            ArgonIterations = reader.GetInt32(3),
            ArgonParallelism = reader.GetInt32(4),
            EncDbk = (byte[])reader[5],
            DbkNonce = (byte[])reader[6],
            DbkTag = (byte[])reader[7],
            IntegrityHmac = reader.IsDBNull(8) ? null : (byte[])reader[8],
            CreatedAt = reader.GetString(9)
        };
    }

    public void UpsertVaultMetadata(VaultMetadataRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO vault_metadata (
                id, username, salt, argon_memory, argon_iterations, argon_parallelism,
                enc_dbk, dbk_nonce, dbk_tag, integrity_hmac, created_at)
            VALUES (1, $username, $salt, $mem, $iter, $par, $enc, $nonce, $tag, $hmac, $created)
            ON CONFLICT(id) DO UPDATE SET
                username = excluded.username,
                salt = excluded.salt,
                argon_memory = excluded.argon_memory,
                argon_iterations = excluded.argon_iterations,
                argon_parallelism = excluded.argon_parallelism,
                enc_dbk = excluded.enc_dbk,
                dbk_nonce = excluded.dbk_nonce,
                dbk_tag = excluded.dbk_tag,
                integrity_hmac = excluded.integrity_hmac;
            """;

        cmd.Parameters.AddWithValue("$username", row.Username);
        cmd.Parameters.AddWithValue("$salt", row.Salt);
        cmd.Parameters.AddWithValue("$mem", row.ArgonMemory);
        cmd.Parameters.AddWithValue("$iter", row.ArgonIterations);
        cmd.Parameters.AddWithValue("$par", row.ArgonParallelism);
        cmd.Parameters.AddWithValue("$enc", row.EncDbk);
        cmd.Parameters.AddWithValue("$nonce", row.DbkNonce);
        cmd.Parameters.AddWithValue("$tag", row.DbkTag);
        cmd.Parameters.AddWithValue("$hmac", (object?)row.IntegrityHmac ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created",
            string.IsNullOrWhiteSpace(row.CreatedAt)
                ? DateTime.UtcNow.ToString("O")
                : row.CreatedAt);
        cmd.ExecuteNonQuery();
    }

    public void UpdateIntegrityHmac(byte[] hmac)
    {
        ArgumentNullException.ThrowIfNull(hmac);
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE vault_metadata SET integrity_hmac = $hmac WHERE id = 1;";
        cmd.Parameters.AddWithValue("$hmac", hmac);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<SshKeyRow> ListKeys()
    {
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, key_type, public_key, fingerprint_sha256,
                   enc_private_key, private_key_nonce, private_key_tag,
                   enc_passphrase, passphrase_nonce, passphrase_tag,
                   comment, created_at, updated_at
            FROM ssh_keys
            ORDER BY created_at DESC;
            """;

        var list = new List<SshKeyRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadKeyRow(reader));
        }

        return list;
    }

    public SshKeyRow? GetKey(string id)
    {
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, key_type, public_key, fingerprint_sha256,
                   enc_private_key, private_key_nonce, private_key_tag,
                   enc_passphrase, passphrase_nonce, passphrase_tag,
                   comment, created_at, updated_at
            FROM ssh_keys WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadKeyRow(reader) : null;
    }

    public void UpsertKey(SshKeyRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ssh_keys (
                id, name, key_type, public_key, fingerprint_sha256,
                enc_private_key, private_key_nonce, private_key_tag,
                enc_passphrase, passphrase_nonce, passphrase_tag,
                comment, created_at, updated_at)
            VALUES (
                $id, $name, $type, $pub, $fp,
                $enc, $nonce, $tag,
                $penc, $pnonce, $ptag,
                $comment, $created, $updated)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                key_type = excluded.key_type,
                public_key = excluded.public_key,
                fingerprint_sha256 = excluded.fingerprint_sha256,
                enc_private_key = excluded.enc_private_key,
                private_key_nonce = excluded.private_key_nonce,
                private_key_tag = excluded.private_key_tag,
                enc_passphrase = excluded.enc_passphrase,
                passphrase_nonce = excluded.passphrase_nonce,
                passphrase_tag = excluded.passphrase_tag,
                comment = excluded.comment,
                updated_at = excluded.updated_at;
            """;

        cmd.Parameters.AddWithValue("$id", row.Id);
        cmd.Parameters.AddWithValue("$name", row.Name);
        cmd.Parameters.AddWithValue("$type", row.KeyType);
        cmd.Parameters.AddWithValue("$pub", row.PublicKey);
        cmd.Parameters.AddWithValue("$fp", row.FingerprintSha256);
        cmd.Parameters.AddWithValue("$enc", row.EncPrivateKey);
        cmd.Parameters.AddWithValue("$nonce", row.PrivateKeyNonce);
        cmd.Parameters.AddWithValue("$tag", row.PrivateKeyTag);
        cmd.Parameters.AddWithValue("$penc", (object?)row.EncPassphrase ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pnonce", (object?)row.PassphraseNonce ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ptag", (object?)row.PassphraseTag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$comment", (object?)row.Comment ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", row.CreatedAt);
        cmd.Parameters.AddWithValue("$updated", row.UpdatedAt);
        cmd.ExecuteNonQuery();
    }

    public void DeleteKey(string id)
    {
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ssh_keys WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<ServerRow> ListServers()
    {
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, host, port, username, default_key_id, proxy_jump_id,
                   tags, notes, auth_mode, is_favorite, last_connected_at, created_at
            FROM servers;
            """;

        var list = new List<ServerRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadServerRow(reader));
        }

        return list;
    }

    public ServerRow? GetServer(string id)
    {
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, host, port, username, default_key_id, proxy_jump_id,
                   tags, notes, auth_mode, is_favorite, last_connected_at, created_at
            FROM servers WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadServerRow(reader) : null;
    }

    public void UpsertServer(ServerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO servers (
                id, title, host, port, username, default_key_id, proxy_jump_id,
                tags, notes, auth_mode, is_favorite, last_connected_at, created_at)
            VALUES (
                $id, $title, $host, $port, $user, $key, $jump,
                $tags, $notes, $auth, $fav, $last, $created)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                host = excluded.host,
                port = excluded.port,
                username = excluded.username,
                default_key_id = excluded.default_key_id,
                proxy_jump_id = excluded.proxy_jump_id,
                tags = excluded.tags,
                notes = excluded.notes,
                auth_mode = excluded.auth_mode,
                is_favorite = excluded.is_favorite,
                last_connected_at = excluded.last_connected_at;
            """;

        cmd.Parameters.AddWithValue("$id", row.Id);
        cmd.Parameters.AddWithValue("$title", row.Title);
        cmd.Parameters.AddWithValue("$host", row.Host);
        cmd.Parameters.AddWithValue("$port", row.Port);
        cmd.Parameters.AddWithValue("$user", row.Username);
        cmd.Parameters.AddWithValue("$key", (object?)row.DefaultKeyId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$jump", (object?)row.ProxyJumpId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tags", (object?)row.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)row.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$auth", row.AuthMode);
        cmd.Parameters.AddWithValue("$fav", row.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$last", (object?)row.LastConnectedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created",
            string.IsNullOrWhiteSpace(row.CreatedAt)
                ? DateTime.UtcNow.ToString("O")
                : row.CreatedAt);
        cmd.ExecuteNonQuery();
    }

    public bool DeleteServer(string id)
    {
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM servers WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public void InsertConnectionLog(string serverId, ConnectionLogStatus status, string? errorMessage)
    {
        var connection = _db.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO connection_logs (server_id, connected_at, status, error_message)
            VALUES ($sid, $at, $status, $err);
            """;
        cmd.Parameters.AddWithValue("$sid", serverId);
        cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$status", ConnectionLogStatusNames.ToDb(status));
        cmd.Parameters.AddWithValue("$err", (object?)errorMessage ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static string ToKeyType(SshKeyAlgorithm algorithm) => algorithm switch
    {
        SshKeyAlgorithm.Rsa4096 => "rsa_4096",
        SshKeyAlgorithm.EcdsaP384 => "ecdsa_p384",
        SshKeyAlgorithm.SkEd25519 => "sk-ed25519",
        _ => "ed25519"
    };

    public static SshKeyAlgorithm FromKeyType(string keyType) => keyType switch
    {
        "rsa_4096" => SshKeyAlgorithm.Rsa4096,
        "ecdsa_p384" => SshKeyAlgorithm.EcdsaP384,
        "sk-ed25519" => SshKeyAlgorithm.SkEd25519,
        _ => SshKeyAlgorithm.Ed25519
    };

    private static SshKeyRow ReadKeyRow(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Name = reader.GetString(1),
        KeyType = reader.GetString(2),
        PublicKey = reader.GetString(3),
        FingerprintSha256 = reader.GetString(4),
        EncPrivateKey = (byte[])reader[5],
        PrivateKeyNonce = (byte[])reader[6],
        PrivateKeyTag = (byte[])reader[7],
        EncPassphrase = reader.IsDBNull(8) ? null : (byte[])reader[8],
        PassphraseNonce = reader.IsDBNull(9) ? null : (byte[])reader[9],
        PassphraseTag = reader.IsDBNull(10) ? null : (byte[])reader[10],
        Comment = reader.IsDBNull(11) ? null : reader.GetString(11),
        CreatedAt = reader.GetString(12),
        UpdatedAt = reader.GetString(13)
    };

    private static ServerRow ReadServerRow(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Title = reader.GetString(1),
        Host = reader.GetString(2),
        Port = reader.GetInt32(3),
        Username = reader.GetString(4),
        DefaultKeyId = reader.IsDBNull(5) ? null : reader.GetString(5),
        ProxyJumpId = reader.IsDBNull(6) ? null : reader.GetString(6),
        Tags = reader.IsDBNull(7) ? null : reader.GetString(7),
        Notes = reader.IsDBNull(8) ? null : reader.GetString(8),
        AuthMode = reader.IsDBNull(9) ? "key" : reader.GetString(9),
        IsFavorite = !reader.IsDBNull(10) && reader.GetInt32(10) != 0,
        LastConnectedAt = reader.IsDBNull(11) ? null : reader.GetString(11),
        CreatedAt = reader.GetString(12)
    };
}
