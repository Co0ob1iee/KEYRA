# Publishing KEYRA with GitHub Desktop

The repository at `S:\source\KEYRA` already has an initial local git commit. You do **not** need the `gh` CLI.

## Versioning

Product version is centralized in [`Directory.Build.props`](Directory.Build.props):

| Property | Example | Role |
| -------- | ------- | ---- |
| `Version` / `InformationalVersion` | `1.0.0` | SemVer (status bar: `KEYRA v1.0.0`) |
| `AssemblyVersion` / `FileVersion` | `1.0.0.0` | Win32 / assembly four-part version |

**Baseline:** `1.0.0` (first release). Bump before each subsequent release.

```powershell
# Preferred — updates Directory.Build.props + CHANGELOG stub
pwsh scripts/bump-version.ps1 -Part patch   # 1.0.0 → 1.0.1
pwsh scripts/bump-version.ps1 -Part minor   # 1.0.1 → 1.1.0
pwsh scripts/bump-version.ps1 -Part major   # 1.1.0 → 2.0.0
```

Or edit the four version properties in `Directory.Build.props` only (do not put versions back in the `.csproj`).

Keep [`CHANGELOG.md`](CHANGELOG.md) in [Keep a Changelog](https://keepachangelog.com/) format.

## 1. Add the local repository

1. Open [GitHub Desktop](https://desktop.github.com/)
2. **File → Add local repository…**
3. Choose folder: `S:\source\KEYRA`
4. Confirm / add

The folder already contains a `.git` directory and an initial commit on branch `main`. Prefer **Add local repository** (do not “create” a second empty repo on top of it).

If `git` is not on your PATH, that is fine — GitHub Desktop bundles its own Git.

## 2. Publish as a public GitHub repo

1. In GitHub Desktop, with KEYRA selected
2. Click **Publish repository**
3. Name: `KEYRA` (or your preferred name)
4. Description (optional): `Desktop SSH key vault and SSH client for Windows`
5. Uncheck **Keep this code private** (publish as **Public**)
6. Choose your GitHub account / org
7. Click **Publish repository**

## 3. Release checklist

For every GitHub Release (example: `1.0.0`):

1. **Bump version** (skip bump on the very first `1.0.0` if props already say `1.0.0`):

   ```powershell
   pwsh scripts/bump-version.ps1 -Part patch
   ```

2. **Update CHANGELOG** — fill Added / Changed / Fixed under `[x.y.z]`, set the date, move notes out of `[Unreleased]` as needed.

3. **Build Release** and confirm it succeeds:

   ```powershell
   cd S:\source\KEYRA
   dotnet build KEYRA.sln -c Release
   ```

4. **Publish** self-contained win-x64:

   ```powershell
   dotnet publish src/SshKeyManager/SshKeyManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/win-x64
   ```

5. Zip `dist\win-x64` as `KEYRA-1.0.0-win-x64.zip` (match the SemVer).

6. **GitHub Desktop:**
   - Commit version + changelog + any code changes
   - Create tag `v1.0.0` (Repository → Create tag…, or tag after commit)
   - Push commits **and** tags to `origin`

7. On github.com → **Releases → Draft a new release**
   - Choose tag `v1.0.0`
   - Title / notes from CHANGELOG
   - Attach `KEYRA-1.0.0-win-x64.zip`
   - Publish release

Optional: pushing tag `v*` also runs [`.github/workflows/release.yml`](.github/workflows/release.yml), which publishes a win-x64 zip to the GitHub Release automatically (when Actions are enabled on the repo).

## Do not commit

Never add vault files from `%LocalAppData%\SshKeyManager\` or private keys to the repo. See [SECURITY.md](SECURITY.md).
