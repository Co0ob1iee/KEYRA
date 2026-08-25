# KEYRA

<p align="center">
  <img src="src/SshKeyManager/Assets/keyra-logo.png" alt="KEYRA logo" width="160" />
</p>

**KEYRA** is a desktop **SSH key vault** and **SSH client** for Windows — generate, import, and store keys securely, manage connection profiles, and open multi-session terminals with ANSI support.

> Nazwy przestrzeni C# pozostają `SshKeyManager` (stabilność API); branding UI to **KEYRA**.

---

## Features

- **Encrypted SQLite vault** — Argon2id MEK → envelope DBK → AES-256-GCM + KeyGarageHash
- **Key management** — Ed25519 / RSA / ECDSA P-384 generate & import; FIDO2 sk-ed25519 pairing when OpenSSH is available (in-app SSH sessions cannot use sk keys — use OpenSSH CLI)
- **Servers & audit** — profiles in SQLite with connection SUCCESS/FAILED/TIMEOUT logs
- **JumpHost** — bastion direct-tcpip (Key A) then end-to-end target auth (Key B)
- **SSH agent** — Windows OpenSSH agent client + KEYRA agent pipe (`\\.\pipe\keyra-ssh-agent`) that lists and signs vault software keys while unlocked (sk-ed25519 / passphrase keys: list only)
- **Multi-session SSH** — separate windows per session
- **ANSI terminal** — JetBrains Mono, color-aware terminal UI
- **i18n** — 6 languages (EN, PL locales via resources + DE / FR / ZH / RU locale packs)
- **KEYRA branding** — Inter UI font, KEY/RA wordmark, Cyber Emerald palette

## Screenshots

<!-- Add PNGs under docs/screenshots/ and uncomment:
![Vault](docs/screenshots/vault.png)
![Connections](docs/screenshots/connections.png)
![Terminal](docs/screenshots/terminal.png)
-->

_Screenshots coming soon — drop images into `docs/screenshots/` and link them here._

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

Single-file Windows x64 executable (no separate .NET runtime install required on the target PC):

```bash
dotnet publish src/SshKeyManager/SshKeyManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/win-x64
```

Distributable output folder:

```text
dist/win-x64/
  SshKeyManager.exe   # KEYRA app
  …
```

Zip `dist/win-x64` (or attach the folder contents) when creating a GitHub Release.

Pushing a git tag `v*` runs [`.github/workflows/release.yml`](.github/workflows/release.yml), which publishes:

- `KEYRA-vX.Y.Z-win-x64.zip` — portable self-contained folder
- `KEYRA-X.Y.Z-win-x64-setup.exe` — per-user installer (Start menu, optional desktop shortcut, uninstaller; no admin/UAC). Vault data in `%LocalAppData%\SshKeyManager\` is **not** removed on uninstall.

## Security model

| Piece | Details |
| ----- | ------- |
| Unlock | Master password unlocks the vault (not Windows DPAPI) |
| KDF | Argon2id derives key material from the password |
| At rest | Random master key encrypts private keys with AES-256-GCM |
| Integrity | KeyGarageHash verifies vault consistency |
| Profiles | SSH session passwords are **not** stored in `connections.json` |
| Location | `%LocalAppData%\SshKeyManager\` (`master.key.enc`, `vault\`, …) |

See [SECURITY.md](SECURITY.md) for reporting issues and what must never be committed.

## Project structure

```text
KEYRA/
  KEYRA.sln
  Directory.Build.props      # SemVer 1.0.0 (single source of truth)
  CHANGELOG.md               # Keep a Changelog
  scripts/bump-version.ps1   # pwsh … -Part patch|minor|major
  LICENSE                    # MIT
  README.md
  SECURITY.md
  PUBLISH.md                 # GitHub Desktop + release checklist
  .github/workflows/         # tag v* → zip + Setup.exe
  src/
    SshKeyManager/           # WPF app (namespaces: SshKeyManager)
      Assets/                # keyra-logo.png, keyra-icon.ico
      Models/ ViewModels/ Views/
      Services/              # Vault, SSH, security, i18n
      Resources/             # Themes, locales, strings
```

### Version bump

Edit only [`Directory.Build.props`](Directory.Build.props), or run:

```powershell
pwsh scripts/bump-version.ps1 -Part patch
```

See [PUBLISH.md](PUBLISH.md) and [CHANGELOG.md](CHANGELOG.md).

## Contributing

1. Fork / clone the repo
2. Open `KEYRA.sln` in Visual Studio 2022+ or use the .NET CLI
3. Keep vault files and secrets out of commits (see `.gitignore` and [SECURITY.md](SECURITY.md))
4. Prefer small, focused PRs with a short description of the change

## Publishing to GitHub (GitHub Desktop)

This repo is prepared for a **local git commit**. To put it on GitHub without the `gh` CLI, use **GitHub Desktop** — see [PUBLISH.md](PUBLISH.md).

Short version:

1. **File → Add local repository** → select `S:\source\KEYRA`
2. **Publish repository** → set name `KEYRA` → keep **Public** → Publish

## License

[MIT](LICENSE) © 2026 KEYRA contributors
