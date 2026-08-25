# Changelog

All notable changes to KEYRA are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Fixed

## [1.1.0] - 2026-08-25

Platform upgrade aligning KEYRA with the New_Update_KEYRA architecture (SQLite envelope vault, JumpHost, agent, hardware keys, brand pack).

### Added

- SQLite vault (`keyra.db`) with `vault_metadata`, `ssh_keys`, `servers`, `connection_logs`
- Envelope encryption: Argon2id → MEK → AES-256-GCM(DBK) → AES-256-GCM(data); VirtualLock + memzero for sensitive buffers
- One-time migration from legacy JSON vault (`master.key.enc` / `vault/*.key.enc` / `connections.json`)
- Connection audit logs (SUCCESS / FAILED / TIMEOUT) on connect
- Jump host (bastion) via SSH.NET `ForwardedPortLocal` / direct-tcpip (Key A → Key B)
- Windows OpenSSH agent client (Named Pipe) + KEYRA agent provider pipe while vault is unlocked
- Hardware security keys panel: FIDO2 `sk-ed25519` pairing via OpenSSH `ssh-keygen -t ed25519-sk` when available
- ECDSA P-384 key generation; `key_type` model includes `ed25519`, `rsa_4096`, `ecdsa_p384`, `sk-ed25519`
- Embedded Inter (UI) + JetBrains Mono (terminal); KEY/RA typographic wordmark

### Changed

- Vault unlock/setup now targets SQLite envelope metadata (Argon2 params stored in DB; iterations remain **4** for continuity with prior KEYRA vaults)
- Connection profiles moved from `connections.json` into the `servers` table
- Settings copy updated for MEK/DBK envelope model; database path shown in Settings

### Known limitations

- KEYRA agent provider currently answers identity listing; full agent sign-for-CLI is not complete
- PKCS#11 / PIV YubiKey slots are not implemented (planned)
- FIDO2 pairing requires Windows OpenSSH `ssh-keygen` on PATH and a connected authenticator
- NU1903 advisory on transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (Microsoft.Data.Sqlite 10.0.0)

## [1.0.0] - Unreleased

Initial public baseline. Bump with `scripts/bump-version.ps1` and set the date when you tag `v1.0.0`.

### Added

- Encrypted SSH key vault (Argon2id + AES-256-GCM + KeyGarageHash)
- Key generate / import (Ed25519, RSA, OpenSSH formats)
- Connection profiles and multi-session SSH client
- ANSI-aware terminal UI
- Localization (EN, PL, DE, FR, ZH, RU)
- KEYRA branding and desktop status bar version from assembly metadata

[Unreleased]: https://github.com/OWNER/KEYRA/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/OWNER/KEYRA/releases/tag/v1.1.0
[1.0.0]: https://github.com/OWNER/KEYRA/releases/tag/v1.0.0
