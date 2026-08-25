# Security Policy

## Supported versions

| Version | Supported |
| ------- | --------- |
| 1.1.x   | Yes       |
| 1.0.x   | Yes       |

## Reporting a vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities that could expose vault data or private keys.

Instead:

1. Open a **private** security advisory on GitHub (Security → Advisories → New draft advisory), **or**
2. Contact the maintainers via the repository’s contact method / email listed on the GitHub profile.

Include:

- KEYRA version (`KEYRA vX.Y.Z` in the status bar)
- OS version (Windows 10 / 11)
- Steps to reproduce
- Impact assessment (e.g. vault unlock bypass, key material leak)

We aim to acknowledge reports within a reasonable time and coordinate a fix before public disclosure.

## What must never be committed

KEYRA stores sensitive data under Local Application Data. **Never** add these to git:

| Path (under `%LocalAppData%\SshKeyManager\`) | Content |
| -------------------------------------------- | ------- |
| `keyra.db` | SQLite vault (encrypted private keys + metadata) |
| `keyra.db-wal` / `keyra.db-shm` / `*.db-journal` | SQLite WAL/shared-memory/journal sidecars |
| `KeyGarageHash` | Vault integrity HMAC (alongside DB) |
| `master.key.enc` | Legacy encrypted master key (pre-1.1; migrated on unlock) |
| `vault\*.key.enc` | Legacy encrypted private keys |
| `vault\index.json` | Legacy key index |
| `connections.json` | Legacy SSH connection profiles |

Also never commit:

- Plaintext private keys (`.pem`, OpenSSH private key files)
- Passwords, passphrases, or API tokens
- `*.user` / user secrets files
- Any SQLite vault copy (`keyra.db`, `*.db`, `*.db-wal`, `*.db-shm`, `*.db-journal`)

The repository `.gitignore` excludes these vault/database patterns (`keyra.db`, `*.db`, `*.db-wal`, `*.db-shm`, `*.db-journal`, plus legacy vault files). If you accidentally commit vault material, rotate affected keys immediately and scrub history before publishing.

## Security model (summary)

- Master password → **Argon2id** → **MEK** (never stored)
- Random **DBK** wrapped by MEK (**AES-256-GCM** envelope) in `vault_metadata`
- Private keys / sensitive fields at rest → **AES-256-GCM(DBK)** in SQLite
- Vault integrity → GCM auth tags + **KeyGarageHash** / metadata HMAC
- Sensitive buffers → memzero + **VirtualLock** where possible
- SSH passwords entered for a session are **not** stored in connection profiles
- Vault lives only on the local machine (`%LocalAppData%\SshKeyManager`)
