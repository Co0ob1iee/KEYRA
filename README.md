# KEYRA

<p align="center">
  <img src="src/SshKeyManager/Assets/keyra-logo.png" alt="KEYRA logo" width="160" />
</p>

<p align="center">
  <strong>Desktop SSH key vault and SSH client for Windows</strong><br />
  <a href="LICENSE">MIT</a> · v1.4.0 · Windows 10+
</p>

**KEYRA** lets you generate, import, and store SSH keys in an encrypted SQLite vault, manage server profiles and JumpHost paths, open multi-session ANSI terminals, and sign via a local SSH agent.

> C# namespaces remain `SshKeyManager` for API stability; UI branding is **KEYRA**.

---

## Features

- **Encrypted SQLite vault** — Argon2id MEK → envelope DBK → AES-256-GCM at rest, plus KeyGarageHash integrity
- **Key management** — Ed25519 / RSA / ECDSA P-384 generate & import; FIDO2 `sk-ed25519` pairing when OpenSSH is available
- **Servers & audit** — profiles in SQLite with connection SUCCESS / FAILED / TIMEOUT logs
- **JumpHost** — bastion direct-tcpip (Key A) then end-to-end target auth (Key B)
- **SSH agent** — Windows OpenSSH agent client + KEYRA agent pipe (`\\.\pipe\keyra-ssh-agent`) that lists and signs unlocked software vault keys (sk-ed25519 / passphrase keys: list only)
- **Multi-session SSH** — separate windows per session
- **ANSI terminal** — JetBrains Mono, color-aware terminal UI
- **i18n** — six languages (EN, PL via resources + DE / FR / ZH / RU locale packs)
- **In-app updater** — checks public GitHub Releases; prefers `*-setup.exe`, falls back to win-x64 zip (configure owner in Settings → Updates)

### FIDO2 / hardware limits (honest)

- Paired `sk-ed25519` keys are for **OpenSSH CLI / system agent**, not KEYRA in-app terminal sessions (SSH.NET cannot perform hardware SK auth)
- KEYRA agent returns failure on sign for sk-ed25519 and passphrase-protected keys (no interactive FIDO touch or passphrase prompt in-process)
- PKCS#11 / PIV YubiKey slots are not implemented

---

## Requirements

- **Windows 10** or later (x64 recommended)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source

## Build & run

```bash
dotnet build KEYRA.sln -c Release
dotnet run --project src/SshKeyManager/SshKeyManager.csproj -c Release
```

Debug:

```bash
dotnet build KEYRA.sln -c Debug
dotnet run --project src/SshKeyManager/SshKeyManager.csproj
```

## Publish a release build (self-contained)

Single-file Windows x64 executable (no separate .NET runtime on the target PC):

```bash
dotnet publish src/SshKeyManager/SshKeyManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/win-x64
```

Output folder: `dist/win-x64/` (app executable is `SshKeyManager.exe`).

### Release artifacts

Pushing a git tag `v*` runs [`.github/workflows/release.yml`](.github/workflows/release.yml), which publishes:

- `KEYRA-vX.Y.Z-win-x64.zip` — portable self-contained folder
- `KEYRA-X.Y.Z-win-x64-setup.exe` — per-user installer (Start menu, optional desktop shortcut; no admin/UAC). Vault data in `%LocalAppData%\SshKeyManager\` is **not** removed on uninstall.

Local zip + installer without Actions:

```powershell
pwsh scripts/publish.ps1
```

Full GitHub Desktop / release checklist: [PUBLISH.md](PUBLISH.md).

### Version bump

Product version is centralized in [`Directory.Build.props`](Directory.Build.props) (currently **1.4.0**). Do not scatter versions in the `.csproj`.

```powershell
pwsh scripts/bump-version.ps1 -Part patch   # or minor | major
```

Keep [`CHANGELOG.md`](CHANGELOG.md) in [Keep a Changelog](https://keepachangelog.com/) format.

---

## Security model

| Piece | Details |
| ----- | ------- |
| Unlock | Master password unlocks the vault (not Windows DPAPI) |
| KDF | Argon2id derives **MEK** (never stored) |
| Envelope | Random **DBK** wrapped by MEK (**AES-256-GCM**) in `vault_metadata` |
| At rest | Private keys / sensitive fields → **AES-256-GCM(DBK)** in SQLite |
| Integrity | GCM auth tags + **KeyGarageHash** / metadata HMAC |
| Memory | memzero + **VirtualLock** where possible |
| Profiles | SSH session passwords are **not** stored |
| Location | `%LocalAppData%\SshKeyManager\` (`keyra.db`, …) |

See [SECURITY.md](SECURITY.md) for vulnerability reporting and what must never be committed.

---

## Project structure

```text
KEYRA/
  KEYRA.sln
  Directory.Build.props      # SemVer (single source of truth)
  CHANGELOG.md
  scripts/bump-version.ps1
  scripts/publish.ps1
  LICENSE                    # MIT
  README.md
  SECURITY.md
  PUBLISH.md
  .github/workflows/         # tag v* → zip + Setup.exe
  src/
    SshKeyManager/           # WPF app (namespaces: SshKeyManager)
      Assets/                # keyra-logo.png, keyra-icon.ico
      Models/ ViewModels/ Views/
      Services/              # Vault, SSH, security, i18n, updater
      Resources/             # Themes, locales, strings
```

## Contributing

1. Fork / clone the repo
2. Open `KEYRA.sln` in Visual Studio 2022+ or use the .NET CLI
3. Keep vault files and secrets out of commits (see `.gitignore` and [SECURITY.md](SECURITY.md))
4. Prefer small, focused PRs with a short description of the change

## License

[MIT](LICENSE) © 2026 KEYRA contributors
