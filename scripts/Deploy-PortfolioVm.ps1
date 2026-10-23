#Requires -RunAsAdministrator
param(
    [string]$SqlServer = 'mgportfolioproject.database.windows.net',
    [string]$Database = 'FinancialPortfolio',
    [string]$SqlLogin = 'portfolioadmin',
    [string]$PublicIp = '135.237.161.17',
    [string]$CertificateThumbprint,
    [switch]$UseTemporaryCertificate,
    [switch]$CreateAdmin
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this in Windows PowerShell as Administrator inside the Azure VM.'
}
if (-not $CertificateThumbprint -and -not $UseTemporaryCertificate) {
    throw 'Supply -CertificateThumbprint for an installed HTTPS certificate, or -UseTemporaryCertificate for a self-signed presentation certificate with a browser warning.'
}

Import-Module WebAdministration
$dotnet = "$env:ProgramFiles\dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'Run Install-VmRequirements.ps1 first.' }
$package = Join-Path $PSScriptRoot 'portfolio-deploy.zip'
if (-not (Test-Path -LiteralPath $package)) { throw 'Place portfolio-deploy.zip beside this script.' }
$sitePath = 'C:\inetpub\Portfolio'
$siteName = 'Portfolio'
$poolIdentity = 'IIS AppPool\Portfolio'

if (Test-Path "IIS:\Sites\$siteName") {
    $existingPath = (Get-Item "IIS:\Sites\$siteName").physicalPath
    if ([IO.Path]::GetFullPath($existingPath) -ne [IO.Path]::GetFullPath($sitePath)) {
        throw 'An existing Portfolio site points to another directory. Review it before deploying.'
    }
    Stop-Website -Name $siteName
    if ((Get-WebAppPoolState -Name $siteName).Value -eq 'Started') { Stop-WebAppPool -Name $siteName }
}
New-Item -ItemType Directory -Path $sitePath -Force | Out-Null
Expand-Archive -LiteralPath $package -DestinationPath $sitePath -Force
if (-not (Test-Path "$sitePath\wwwroot\index.html")) { throw 'The package is missing the Angular frontend.' }
if (-not (Test-Path "IIS:\AppPools\$siteName")) { New-WebAppPool -Name $siteName | Out-Null }
Set-ItemProperty "IIS:\AppPools\$siteName" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$siteName" -Name processModel.identityType -Value 4
Set-ItemProperty "IIS:\AppPools\$siteName" -Name processModel.loadUserProfile -Value $true
Set-ItemProperty "IIS:\AppPools\$siteName" -Name processModel.setProfileEnvironment -Value $true

New-Item -ItemType Directory -Path "$sitePath\App_Data\Keys" -Force | Out-Null
& icacls.exe $sitePath /grant ($poolIdentity + ':(OI)(CI)RX') /T | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not grant the application read access.' }
& icacls.exe "$sitePath\App_Data" /grant ($poolIdentity + ':(OI)(CI)M') /T | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not grant access to the authentication key directory.' }

$secret = Read-Host 'Azure SQL password for the server administrator' -AsSecureString
$connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$connection['Data Source'] = "tcp:$SqlServer,1433"
$connection['Initial Catalog'] = $Database
$connection['User ID'] = $SqlLogin
$connection['Password'] = [Net.NetworkCredential]::new('', $secret).Password
$connection['Encrypt'] = $true
$connection['TrustServerCertificate'] = $false
$connection['Connect Timeout'] = 30
$configuration = @{ ConnectionStrings = @{ PortfolioDatabase = $connection.ConnectionString } }
$configurationPath = Join-Path $sitePath 'appsettings.Production.json'
$configuration | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $configurationPath -Encoding UTF8
& icacls.exe $configurationPath /inheritance:r /grant:r 'SYSTEM:F' 'BUILTIN\Administrators:F' ($poolIdentity + ':R') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not restrict access to the database configuration.' }
$secret = $null
$connection.Clear()
$configuration = $null

Push-Location $sitePath
try {
    & $dotnet .\Portfolio.Api.dll --environment Production --initialize-database
    if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed. Check the SQL firewall and credentials.' }
    if ($CreateAdmin) {
        $env:PortfolioBootstrap__Email = Read-Host 'New website administrator email'
        $adminSecret = Read-Host 'New website password with 12-128 characters, uppercase, lowercase, digit and special character' -AsSecureString
        $env:PortfolioBootstrap__Password = [Net.NetworkCredential]::new('', $adminSecret).Password
        try {
            & $dotnet .\Portfolio.Api.dll --environment Production --create-admin
            if ($LASTEXITCODE -ne 0) { throw 'Website administrator creation failed. Use a new email or sign in with an existing account.' }
        } finally {
            Remove-Item Env:PortfolioBootstrap__Email -ErrorAction SilentlyContinue
            Remove-Item Env:PortfolioBootstrap__Password -ErrorAction SilentlyContinue
            $adminSecret = $null
        }
    }
} finally { Pop-Location }

if ($UseTemporaryCertificate -and -not $CertificateThumbprint) {
    $certificate = New-SelfSignedCertificate -Type Custom -Subject "CN=$PublicIp" -TextExtension @(
        "2.5.29.17={text}IPAddress=$PublicIp",
        '2.5.29.37={text}1.3.6.1.5.5.7.3.1'
    ) -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyUsage DigitalSignature, KeyEncipherment -CertStoreLocation Cert:\LocalMachine\My -NotAfter (Get-Date).AddDays(7) -FriendlyName 'Portfolio temporary presentation certificate'
    $CertificateThumbprint = $certificate.Thumbprint
    Write-Host 'This temporary certificate causes a browser trust warning. It is not a publicly trusted deployment certificate.'
}
if (-not (Test-Path "Cert:\LocalMachine\My\$CertificateThumbprint")) { throw 'HTTPS certificate was not found.' }
if (-not (Test-Path "IIS:\Sites\$siteName")) {
    New-Website -Name $siteName -PhysicalPath $sitePath -ApplicationPool $siteName -Port 443 -Ssl | Out-Null
}
$binding = Get-WebBinding -Name $siteName -Protocol https
if (-not $binding) { New-WebBinding -Name $siteName -Protocol https -Port 443; $binding = Get-WebBinding -Name $siteName -Protocol https }
$binding.AddSslCertificate($CertificateThumbprint, 'My')
if (-not (Get-NetFirewallRule -Name 'Portfolio-HTTPS' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'Portfolio-HTTPS' -DisplayName 'Portfolio HTTPS' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 443 | Out-Null
}
Start-WebAppPool -Name $siteName
Start-Website -Name $siteName
Write-Host "Application configured at https://$PublicIp. Azure's network security group must also allow TCP 443."

