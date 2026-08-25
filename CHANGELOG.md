# Changelog

All notable changes to KEYRA are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Fixed

## [1.0.0] - Unreleased

Initial public baseline. Bump with `scripts/bump-version.ps1` and set the date when you tag `v1.0.0`.

### Added

- Encrypted SSH key vault (Argon2id + AES-256-GCM + KeyGarageHash)
- Key generate / import (Ed25519, RSA, OpenSSH formats)
- Connection profiles and multi-session SSH client
- ANSI-aware terminal UI
- Localization (EN, PL, DE, FR, ZH, RU)
- KEYRA branding and desktop status bar version from assembly metadata

[Unreleased]: https://github.com/OWNER/KEYRA/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/OWNER/KEYRA/releases/tag/v1.0.0
