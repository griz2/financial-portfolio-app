#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this in Windows PowerShell as Administrator inside the Azure VM.'
}

$features = @(
    'IIS-WebServerRole', 'IIS-WebServer', 'IIS-ManagementConsole',
    'IIS-ManagementScriptingTools', 'IIS-StaticContent', 'IIS-DefaultDocument',
    'IIS-HttpErrors', 'IIS-HttpLogging', 'IIS-RequestFiltering'
)
$result = Enable-WindowsOptionalFeature -Online -FeatureName $features -All -NoRestart
if ($result.RestartNeeded) {
    Write-Host 'Restart the VM, reconnect, then run this script again.'
    return
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$release = $metadata.releases | Where-Object { $_.'release-version' -eq $metadata.'latest-release' } | Select-Object -First 1
$installer = $release.'aspnetcore-runtime'.files | Where-Object { $_.name -eq 'dotnet-hosting-win.exe' } | Select-Object -First 1
if (-not $installer -or -not $installer.hash) {
    throw 'The official .NET 10 Hosting Bundle could not be located in release metadata.'
}

$installerPath = Join-Path $env:TEMP 'portfolio-dotnet-hosting.exe'
Invoke-WebRequest -Uri $installer.url -OutFile $installerPath -UseBasicParsing
$actualHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA512).Hash
if ($actualHash -ne $installer.hash) {
    throw 'Hosting Bundle hash verification failed.'
}
$process = Start-Process -FilePath $installerPath -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -notin @(0, 3010)) {
    throw "Hosting Bundle installation failed with exit code $($process.ExitCode)."
}
if ($process.ExitCode -eq 3010) {
    Write-Host 'Hosting Bundle installed. Restart the VM before deploying.'
    return
}

Restart-Service W3SVC -Force
& "$env:ProgramFiles\dotnet\dotnet.exe" --list-runtimes
Write-Host 'IIS and the .NET 10 Hosting Bundle are ready.'
