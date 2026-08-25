$basePath = "S:\source\AiTools\src\SshKeyManager\Resources\Strings.resx"
[xml]$base = Get-Content $basePath -Encoding UTF8
$keys = @{}
foreach ($data in $base.root.data) { $keys[$data.name] = $data.value }

$en = @{
  App_StartupFailed = "Failed to start SshKeyManager.`n{0}"
  Nav_Keys = "Keys"; Nav_Generate = "Generate"; Nav_Import = "Import"; Nav_Connections = "Connections"; Nav_Settings = "Settings"; Nav_LockVault = "Lock vault"
  Status_Ready = "Ready"; Status_StartupError = "Startup error."; Status_KeysLoaded = "Loaded {0} key(s)."; Status_FailedLoadVault = "Failed to load vault."
  Status_PublicKeyCopied = "Public key copied."; Status_CopyFailed = "Copy failed."; Status_PrivateKeyCopied = "Private key copied — clear clipboard soon."
  Status_CopyPrivateKeyFailed = "Copy private key failed."; Status_PrivateKeyVisible = "Private key visible — auto-hide in 15s."; Status_RevealFailed = "Reveal failed."
  Status_KeyExported = "Key exported."; Status_ExportFailed = "Export failed."; Status_ExportedToSsh = "Exported to ~/.ssh."; Status_ExportToSshFailed = "Export to ~/.ssh failed."
  Status_KeyDeleted = "Key deleted."; Status_DeleteFailed = "Delete failed."; Status_PrivateKeyHidden = "Private key hidden."; Status_Generating = "Generating key…"
  Status_KeyGenerated = "Key generated."; Status_GenerateFailed = "Generate failed."; Status_Importing = "Importing key…"; Status_KeyImported = "Key imported."
  Status_ImportFailed = "Import failed."; Status_SshConnected = "Connected via SSH."; Status_SshConnectFailed = "SSH connection failed."; Status_Disconnected = "Disconnected."
  Status_VaultLocked = "Vault locked."; Status_OpenVaultFolder = "Vault folder opened."; Status_OpenVaultFailed = "Cannot open vault folder."; Status_OpenRootFolder = "App folder opened."
  Status_OpenRootFailed = "Cannot open app folder."; Status_LogCleared = "Log cleared."; Status_PasswordChanged = "Password changed."
  Inspector_Title = "Inspector"; Inspector_TabDetails = "Details"; Inspector_TabActions = "Actions"; Inspector_SelectKey = "Select a key to inspect."
  Inspector_Name = "Name"; Inspector_Algorithm = "Algorithm"; Inspector_Fingerprint = "Fingerprint"; Inspector_Comment = "Comment"; Inspector_PublicKey = "Public key"
  Inspector_VaultPath = "Vault path"; Inspector_CopyPublicKey = "Copy public key"; Inspector_ShowPrivateKey = "Show private key"; Inspector_CopyPrivateKey = "Copy private key"
  Inspector_ExportFolder = "Export to folder…"; Inspector_ExportSsh = "Export to ~/.ssh"; Inspector_Delete = "Delete"; Inspector_PrivateKey = "Private key"; Inspector_HideNow = "Hide now"
  Log_Title = "Operations"; Log_Clear = "Clear"; Keys_Title = "Keys"; Keys_Refresh = "Refresh"; Keys_SearchWatermark = "Search by name, fingerprint or comment…"
  Keys_Empty = "No keys in vault. Generate or import one."; Keys_CopyPub = "Copy .pub"; Keys_Export = "Export"; Keys_Delete = "Delete"; Keys_PassphraseYes = "Passphrase: yes"; Keys_PassphraseNo = "Passphrase: no"
  Generate_Title = "Generate key"; Generate_Name = "Name"; Generate_Comment = "Comment"; Generate_Algorithm = "Algorithm"; Generate_Algorithm_Ed25519 = "Ed25519 (recommended)"
  Generate_Algorithm_Rsa4096 = "RSA 4096"; Generate_Passphrase = "Passphrase (optional)"; Generate_ConfirmPassphrase = "Confirm passphrase"; Generate_Button = "Generate"
  Generate_Busy = "Generating…"; Generate_Fingerprint = "Fingerprint"; Generate_PublicKey = "Public key"; Generate_NameRequired = "Name is required."; Generate_PassphraseMismatch = "Passphrases do not match."
  Generate_Success = "Key generated and saved to vault."; Import_Title = "Import key"; Import_Name = "Name"; Import_PrivateKey = "Private key (OpenSSH PEM)"; Import_Browse = "Browse…"
  Import_Passphrase = "Passphrase (if encrypted)"; Import_Preview = "Preview"; Import_Button = "Import to vault"; Import_Fingerprint = "Fingerprint"; Import_PublicKey = "Public key"
  Import_NameRequired = "Name is required."; Import_KeyRequired = "Private key is required."; Import_PasteFirst = "Paste or load a private key first."; Import_Success = "Imported and saved to vault."
  Import_DialogTitle = "Import OpenSSH private key"; Import_LoadedFile = "Loaded file: {0}"; Import_Parsed = "Parsed {0} — {1}"
  Connections_Title = "SSH connections"; Connections_Host = "Host"; Connections_Port = "Port"; Connections_Username = "SSH user"; Connections_PasswordAuth = "Password authentication (instead of vault key)"
  Connections_SshPassword = "SSH password"; Connections_VaultKey = "Vault key"; Connections_KeyPassphrase = "OpenSSH key passphrase (optional)"; Connections_Status = "Status"
  Connections_Connect = "Connect"; Connections_Disconnect = "Disconnect"; Connections_Session = "Session (terminal)"; Connections_Clear = "Clear"; Connections_Send = "Send"
  Connections_Disconnected = "Disconnected"; Connections_Connecting = "Connecting…"; Connections_Connected = "Connected"; Connections_Disconnecting = "Disconnecting…"
  Connections_ErrHostUser = "Enter host and username."; Connections_ErrSelectKey = "Select a vault key or enable password authentication."
  Settings_Title = "Settings"; Settings_Info = "Private keys are encrypted with AES-256-GCM using a random master key protected by your password (Argon2id). KeyGarageHash verifies vault integrity. Private key material is never written to the operation log."
  Settings_RootPath = "App directory (vault)"; Settings_OpenRoot = "Open app directory"; Settings_VaultPath = "Vault folder (keys)"; Settings_OpenVault = "Open vault folder"
  Settings_SshPath = "Default ~/.ssh directory"; Settings_Language = "Language"; Settings_ChangePassword = "Change vault password"; Settings_ChangePasswordHint = "Re-encrypts the master key with a new password and refreshes KeyGarageHash."
  Settings_CurrentPassword = "Current password"; Settings_NewPassword = "New password"; Settings_ConfirmPassword = "Confirm new password"; Settings_ChangeButton = "Change password"
  Settings_Security = "Security"; Settings_SecurityHint = "The vault protects keys from other system users, not from malware in your session. Prefer Ed25519. Clear the clipboard after copying a private key."
  Settings_PasswordMismatch = "New passwords do not match."; Settings_PasswordChanged = "Password changed. Master key re-encrypted."; Settings_WrongPassword = "Current password is incorrect."
  Login_WindowTitle = "SshKey Manager — unlock vault"; Login_Title = "Unlock vault"; Login_Subtitle = "Enter your vault account to decrypt the master key."; Login_Username = "Username"; Login_Password = "Password"
  Login_Exit = "Exit"; Login_Unlock = "Unlock"; Login_InvalidCredentials = "Invalid username or password."
  Setup_WindowTitle = "SshKey Manager — vault setup"; Setup_Title = "First run"; Setup_Subtitle = "Create a local vault account. A random AES-256 key will be encrypted with your password (Argon2id)."
  Setup_Username = "Username"; Setup_Password = "Password (min. 8 characters)"; Setup_ConfirmPassword = "Confirm password"; Setup_Cancel = "Cancel"; Setup_Create = "Create vault"; Setup_PasswordMismatch = "Passwords do not match."
  Dialog_CopyPrivateKey = "Copy the private key to the clipboard?`nClear the clipboard when finished."; Dialog_RevealPrivateKey = "Show the private key in the inspector?`nIt will hide automatically after about 15 seconds."
  Dialog_ExportOverwrite = "Files already exist in the target folder. Overwrite?"; Dialog_ExportToSsh = "Export '{0}' to:`n{1}`n`nauthorized_keys will not be modified."; Dialog_SshOverwrite = "A key with this file name already exists in .ssh. Overwrite?"
  Dialog_DeleteKey = "Delete key '{0}' from the vault?`nThis cannot be undone."; Dialog_ExportFolderTitle = "Export key pair to folder"
  Log_AppStarted = "SshKeyManager started."; Log_VaultRefreshed = "Vault refreshed ({0} keys)."; Log_CopiedPublic = "Copied public key for '{0}'."; Log_CopiedPrivate = "Copied private key for '{0}' to clipboard."
  Log_RevealedPrivate = "Revealed private key for '{0}' (auto-hide)."; Log_Exported = "Exported '{0}' to {1}."; Log_ExportedSsh = "Exported '{0}' to .ssh."; Log_Deleted = "Deleted key '{0}'."
  Log_Generated = "Generated {0} key '{1}' ({2})."; Log_Imported = "Imported key '{0}' ({1})."; Log_SshConnected = "SSH connected to {0}:{1}."; Log_SshDisconnected = "SSH disconnected."
  Log_VaultLocked = "Vault locked."; Log_OpenVault = "Vault folder opened."; Log_OpenRoot = "App directory opened."; Log_PasswordChanged = "Vault password changed."; Log_StartupFailed = "Startup failed: {0}"
  Reveal_HidingIn = "Hiding in {0}s"; Connections_OutputError = "[ERR] {0}"
}

function Write-Resx($suffix, $map) {
  $dir = Split-Path $basePath
  $out = Join-Path $dir "Strings.$suffix.resx"
  $sb = New-Object System.Text.StringBuilder
  [void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
  [void]$sb.AppendLine('<root>')
  [void]$sb.AppendLine('  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>')
  [void]$sb.AppendLine('  <resheader name="version"><value>2.0</value></resheader>')
  [void]$sb.AppendLine('  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>')
  [void]$sb.AppendLine('  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>')
  foreach ($name in ($keys.Keys | Sort-Object)) {
    $val = if ($map.ContainsKey($name)) { $map[$name] } else { $en[$name] }
    if (-not $val) { $val = $keys[$name] }
    $val = [System.Security.SecurityElement]::Escape($val)
    [void]$sb.AppendLine("  <data name=""$name"" xml:space=""preserve""><value>$val</value></data>")
  }
  [void]$sb.AppendLine('</root>')
  [System.IO.File]::WriteAllText($out, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
  Write-Host "Wrote $out"
}

Write-Resx 'en' $en

# ru / zh-CN / fr / de: start from en and patch primary UI strings
$ru = $en.Clone(); $ru['Nav_Keys']='Ключи'; $ru['Nav_Generate']='Создать'; $ru['Nav_Import']='Импорт'; $ru['Nav_Connections']='Подключения'; $ru['Nav_Settings']='Настройки'; $ru['Nav_LockVault']='Заблокировать сейф'; $ru['Status_Ready']='Готов'; $ru['Keys_Title']='Ключи'; $ru['Generate_Title']='Создать ключ'; $ru['Import_Title']='Импорт ключа'; $ru['Connections_Title']='SSH-подключения'; $ru['Settings_Title']='Настройки'; $ru['Settings_Language']='Язык'; $ru['Login_Title']='Разблокировать сейф'; $ru['Setup_Title']='Первый запуск'; $ru['Inspector_Title']='Инспектор'; $ru['Log_Title']='Операции'; $ru['Connections_Connect']='Подключить'; $ru['Connections_Disconnect']='Отключить'; $ru['Connections_Disconnected']='Отключено'; $ru['Connections_Connected']='Подключено'; $ru['Connections_Connecting']='Подключение…'; $ru['Log_Clear']='Очистить'; $ru['Keys_Refresh']='Обновить'
$zh = $en.Clone(); $zh['Nav_Keys']='密钥'; $zh['Nav_Generate']='生成'; $zh['Nav_Import']='导入'; $zh['Nav_Connections']='连接'; $zh['Nav_Settings']='设置'; $zh['Nav_LockVault']='锁定保险库'; $zh['Status_Ready']='就绪'; $zh['Keys_Title']='密钥'; $zh['Generate_Title']='生成密钥'; $zh['Import_Title']='导入密钥'; $zh['Connections_Title']='SSH 连接'; $zh['Settings_Title']='设置'; $zh['Settings_Language']='语言'; $zh['Login_Title']='解锁保险库'; $zh['Setup_Title']='首次运行'; $zh['Inspector_Title']='检查器'; $zh['Log_Title']='操作'; $zh['Connections_Connect']='连接'; $zh['Connections_Disconnect']='断开'; $zh['Connections_Disconnected']='已断开'; $zh['Connections_Connected']='已连接'; $zh['Connections_Connecting']='连接中…'; $zh['Log_Clear']='清空'; $zh['Keys_Refresh']='刷新'
$fr = $en.Clone(); $fr['Nav_Keys']='Clés'; $fr['Nav_Generate']='Générer'; $fr['Nav_Import']='Importer'; $fr['Nav_Connections']='Connexions'; $fr['Nav_Settings']='Paramètres'; $fr['Nav_LockVault']='Verrouiller le coffre'; $fr['Status_Ready']='Prêt'; $fr['Keys_Title']='Clés'; $fr['Generate_Title']='Générer une clé'; $fr['Import_Title']='Importer une clé'; $fr['Connections_Title']='Connexions SSH'; $fr['Settings_Title']='Paramètres'; $fr['Settings_Language']='Langue'; $fr['Login_Title']='Déverrouiller le coffre'; $fr['Setup_Title']='Premier lancement'; $fr['Inspector_Title']='Inspecteur'; $fr['Log_Title']='Opérations'; $fr['Connections_Connect']='Connecter'; $fr['Connections_Disconnect']='Déconnecter'; $fr['Connections_Disconnected']='Déconnecté'; $fr['Connections_Connected']='Connecté'; $fr['Connections_Connecting']='Connexion…'; $fr['Log_Clear']='Effacer'; $fr['Keys_Refresh']='Actualiser'
$de = $en.Clone(); $de['Nav_Keys']='Schlüssel'; $de['Nav_Generate']='Generieren'; $de['Nav_Import']='Importieren'; $de['Nav_Connections']='Verbindungen'; $de['Nav_Settings']='Einstellungen'; $de['Nav_LockVault']='Tresor sperren'; $de['Status_Ready']='Bereit'; $de['Keys_Title']='Schlüssel'; $de['Generate_Title']='Schlüssel generieren'; $de['Import_Title']='Schlüssel importieren'; $de['Connections_Title']='SSH-Verbindungen'; $de['Settings_Title']='Einstellungen'; $de['Settings_Language']='Sprache'; $de['Login_Title']='Tresor entsperren'; $de['Setup_Title']='Erster Start'; $de['Inspector_Title']='Inspektor'; $de['Log_Title']='Vorgänge'; $de['Connections_Connect']='Verbinden'; $de['Connections_Disconnect']='Trennen'; $de['Connections_Disconnected']='Getrennt'; $de['Connections_Connected']='Verbunden'; $de['Connections_Connecting']='Verbinden…'; $de['Log_Clear']='Leeren'; $de['Keys_Refresh']='Aktualisieren'

Write-Resx 'ru' $ru
Write-Resx 'zh-CN' $zh
Write-Resx 'fr' $fr
Write-Resx 'de' $de
