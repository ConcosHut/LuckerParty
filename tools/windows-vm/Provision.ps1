$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = 'C:\LuckerParty'
New-Item -ItemType Directory -Force $root, "$root\incoming", "$root\runs", 'C:\Tools' | Out-Null
Start-Transcript "$root\provision.log" -Append
try {
    $hashes = Get-Content "$PSScriptRoot\assets.json" -Raw | ConvertFrom-Json
    foreach ($asset in $hashes.PSObject.Properties) {
        if ((Get-FileHash (Join-Path $PSScriptRoot $asset.Name)).Hash -ne $asset.Value) { throw "Asset hash mismatch: $($asset.Name)" }
    }
    $python = Start-Process "$PSScriptRoot\python-3.11.9-amd64.exe" -Wait -PassThru -ArgumentList '/quiet InstallAllUsers=1 TargetDir=C:\Tools\Python Include_test=0 Include_launcher=0 PrependPath=1'
    if ($python.ExitCode -ne 0) { throw "Python installation failed: $($python.ExitCode)" }
    foreach ($asset in @(
        @('dotnet-sdk-8.0.425-win-x64.zip', 'C:\Tools\dotnet'),
        @('Godot_v4.5.2-stable_mono_win64.zip', 'C:\Tools\Godot'),
        @('MinGit-2.56.0-64-bit.zip', 'C:\Tools\Git'),
        @('templates.tpz', "$root\templates")
    )) {
        & 'C:\Tools\Python\python.exe' -m zipfile -e (Join-Path $PSScriptRoot $asset[0]) $asset[1]
        if ($LASTEXITCODE -ne 0) { throw "Extraction failed: $($asset[0])" }
    }
    $templates = "$env:APPDATA\Godot\export_templates\4.5.2.stable.mono"
    New-Item -ItemType Directory -Force $templates | Out-Null
    Copy-Item "$root\templates\templates\*" $templates -Force
    Remove-Item "$root\templates" -Recurse -Force
    Copy-Item "$PSScriptRoot\Run-Integration.ps1" "$root\Run-Integration.ps1" -Force
    (Get-Item "$root\Run-Integration.ps1").IsReadOnly = $false
    $godot = (Get-ChildItem 'C:\Tools\Godot' -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1).FullName
    New-NetFirewallRule -Name LuckerParty-Local-Scenarios -DisplayName 'Lucker Party local network scenarios' -Direction Inbound -Action Allow -Program $godot -RemoteAddress 127.0.0.1 -LocalAddress 127.0.0.1 | Out-Null
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File C:\LuckerParty\Run-Integration.ps1'
    $principal = New-ScheduledTaskPrincipal -UserId 'vmrunner' -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName 'LuckerParty-Integration' -Action $action -Principal $principal -Settings $settings -Force | Out-Null
    # Dedicated guest only: preserve password login on the console, use keys for SSH.
    Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
    $ssh = "$env:ProgramData\ssh"
    New-Item -ItemType Directory -Force $ssh | Out-Null
    Copy-Item "$PSScriptRoot\ssh_host_ed25519_key*" $ssh -Force
    Copy-Item "$PSScriptRoot\authorized_keys" "$ssh\administrators_authorized_keys" -Force
    & icacls.exe "$ssh\ssh_host_ed25519_key" /inheritance:r /grant '*S-1-5-18:F' '*S-1-5-32-544:F'
    & icacls.exe "$ssh\administrators_authorized_keys" /inheritance:r /grant '*S-1-5-18:F' '*S-1-5-32-544:F'
    @'
Port 22
HostKey __PROGRAMDATA__/ssh/ssh_host_ed25519_key
PubkeyAuthentication yes
PasswordAuthentication no
AllowUsers vmrunner
Subsystem sftp sftp-server.exe
Match Group administrators
    AuthorizedKeysFile __PROGRAMDATA__/ssh/administrators_authorized_keys
'@ | Set-Content "$ssh\sshd_config" -Encoding ascii
    & "$env:WINDIR\System32\OpenSSH\sshd.exe" -t
    if ($LASTEXITCODE -ne 0) { throw 'Invalid sshd configuration' }
    Get-NetFirewallRule -Name OpenSSH-Server-In-TCP -ErrorAction SilentlyContinue | Disable-NetFirewallRule
    New-NetFirewallRule -Name LuckerParty-Box-SSH -DisplayName 'Lucker Party SSH from QEMU host' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 22 -RemoteAddress 10.0.2.2 | Out-Null
    Set-Service sshd -StartupType Automatic
    Start-Service sshd
    Set-LocalUser -Name vmrunner -PasswordNeverExpires $true
    powercfg.exe /change standby-timeout-ac 0
    powercfg.exe /change monitor-timeout-ac 0
    'PROVISION_PASS' | Set-Content "$root\provision-status.txt"
} catch {
    $_ | Out-String | Set-Content "$root\provision-error.txt"
    throw
} finally { Stop-Transcript }
