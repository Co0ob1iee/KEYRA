# Security Policy

## Supported versions

| Version | Supported |
| ------- | --------- |
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
| `master.key.enc` | Encrypted master key |
| `KeyGarageHash` | Vault integrity hash |
| `vault\*.key.enc` | Encrypted private keys |
| `vault\index.json` | Key index |
| `connections.json` | SSH connection profiles |

Also never commit:

- Plaintext private keys (`.pem`, OpenSSH private key files)
- Passwords, passphrases, or API tokens
- `*.user` / user secrets files

The repository `.gitignore` already excludes these patterns. If you accidentally commit vault material, rotate affected keys immediately and scrub history before publishing.

## Security model (summary)

- Master password → **Argon2id** → wraps a random master key
- Private keys at rest → **AES-256-GCM**
- Vault integrity → **KeyGarageHash**
- SSH passwords entered for a session are **not** stored in connection profiles
- Vault lives only on the local machine (`%LocalAppData%\SshKeyManager`)
