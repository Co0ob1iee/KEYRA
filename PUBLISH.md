# Publishing KEYRA with GitHub Desktop

The repository at `S:\source\KEYRA` already has an initial local git commit. You do **not** need the `gh` CLI.

## 1. Add the local repository

1. Open [GitHub Desktop](https://desktop.github.com/)
2. **File → Add local repository…**
3. Choose folder: `S:\source\KEYRA`
4. Confirm / add

If Desktop says the directory is not a Git repository, run once in PowerShell:

```powershell
cd S:\source\KEYRA
git status
```

(There should already be a commit; if `git` is missing from PATH, use GitHub Desktop’s “create a repository” only on an empty folder — prefer **Add local repository** on this existing repo.)

## 2. Publish as a public GitHub repo

1. In GitHub Desktop, with KEYRA selected
2. Click **Publish repository**
3. Name: `KEYRA` (or your preferred name)
4. Description (optional): `Desktop SSH key vault and SSH client for Windows`
5. Uncheck **Keep this code private** (publish as **Public**)
6. Choose your GitHub account / org
7. Click **Publish repository**

## 3. Create a GitHub Release (optional)

1. Build a distributable:

```powershell
cd S:\source\KEYRA
dotnet publish src/SshKeyManager/SshKeyManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/win-x64
```

2. Zip `dist\win-x64` (e.g. `KEYRA-1.0.0-win-x64.zip`)
3. On github.com → your KEYRA repo → **Releases → Draft a new release**
4. Tag: `v1.0.0`
5. Attach the zip
6. Publish release

## Do not commit

Never add vault files from `%LocalAppData%\SshKeyManager\` or private keys to the repo. See [SECURITY.md](SECURITY.md).
