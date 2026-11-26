# Manifest winget e, su Windows con il Windows SDK, un pacchetto MSIX.
# Senza segreti di firma l'MSIX resta non firmato e si chiama NOREA_Suite_unsigned.msix:
# Windows non lo installa con un doppio clic. Per provarlo in locale: -SelfSigned.
param(
    [Parameter(Mandatory = $true)][string]$PortableDir,
    [Parameter(Mandatory = $true)][string]$ZipPath,
    [string]$Version = "",
    [string]$Tag = "",
    [string]$OutputDir = "",
    [switch]$SkipMsix,
    [switch]$SelfSigned
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $root "installer_out" }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($Version)) {
    $propsPath = Join-Path $root "Directory.Build.props"
    [xml]$props = Get-Content -LiteralPath $propsPath
    $Version = [string]$props.Project.PropertyGroup.NoreaSuiteVersion
    $Version = $Version.Trim()
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $rawProps = Get-Content -LiteralPath $propsPath -Raw
        if ($rawProps -match '<NoreaSuiteVersion>([^<]+)</NoreaSuiteVersion>') {
            $Version = $Matches[1].Trim()
        }
    }
}
if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw "Versione non valida: $Version" }
$identityVersion = if ($Version.Split('.').Count -eq 3) { "$Version.0" } else { $Version }
if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = if ([string]::IsNullOrWhiteSpace($env:NOREA_RELEASE_TAG)) { "v$($Version.Split('.')[0..2] -join '.')" } else { $env:NOREA_RELEASE_TAG }
}
$repo = if ([string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) { "chrisquire80/REA" } else { $env:GITHUB_REPOSITORY }
if (-not (Test-Path -LiteralPath $ZipPath)) { throw "Zip non trovato: $ZipPath" }
$hash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()

$winget = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.singleton.1.10.0.schema.json
PackageIdentifier: ChristianRobecchi.NOREA
PackageVersion: $Version
PackageLocale: it-IT
Publisher: Christian Robecchi
PublisherUrl: https://github.com/$repo
PackageName: NOREA Suite
License: MIT
LicenseUrl: https://github.com/$repo/blob/main/LICENSE
ShortDescription: Editor, ricerca file e client SFTP per Windows
Description: Suite portatile NOREA. Il pacchetto winget scompatta lo zip e registra i comandi portatili.
Tags:
- notepad
- sftp
- search
Installers:
- Architecture: x64
  InstallerType: zip
  NestedInstallerType: portable
  NestedInstallerFiles:
  - RelativeFilePath: NoreaLauncher.exe
    PortableCommandAlias: norea-launcher
  - RelativeFilePath: norea.exe
    PortableCommandAlias: norea
  - RelativeFilePath: NotepadREA.exe
    PortableCommandAlias: notepadrea
  InstallerUrl: https://github.com/$repo/releases/download/$Tag/NOREA_Suite_Portable.zip
  InstallerSha256: $hash
ManifestType: singleton
ManifestVersion: 1.10.0
"@
$wingetPath = Join-Path $OutputDir "ChristianRobecchi.NOREA.yaml"
Set-Content -LiteralPath $wingetPath -Value $winget.TrimEnd() -Encoding utf8
Write-Host "Manifest winget: $wingetPath"

if ($SkipMsix) { return }

$makeAppx = $null
$kitRoots = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
    "$env:ProgramFiles\Windows Kits\10\bin"
)
foreach ($kit in $kitRoots) {
    if (-not (Test-Path -LiteralPath $kit)) { continue }
    $found = Get-ChildItem -LiteralPath $kit -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\makeappx.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($found) { $makeAppx = $found.FullName; break }
}
if (-not $makeAppx) {
    Write-Warning "makeappx.exe non trovato: MSIX non creato. Il manifest winget è comunque pronto."
    return
}

$layout = Join-Path $OutputDir "msix-layout"
if (Test-Path $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
New-Item -ItemType Directory -Path $layout | Out-Null
Copy-Item -Path (Join-Path $PortableDir "*") -Destination $layout -Recurse -Force
$assetDest = Join-Path $layout "Assets"
New-Item -ItemType Directory -Path $assetDest -Force | Out-Null
Copy-Item -Path (Join-Path $root "installer\assets\*") -Destination $assetDest -Force

function ConvertTo-XmlText([string]$Value) { return [System.Security.SecurityElement]::Escape($Value) }

$publisher = "CN=Christian Robecchi"
$testPfx = $null
if ($SelfSigned) {
    $testPfx = Join-Path $OutputDir "norea-local-test.pfx"
    $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=NOREA Local Test" `
        -KeyUsage DigitalSignature -FriendlyName "NOREA Local Test" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
    $secure = ConvertTo-SecureString "norea-test" -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $testPfx -Password $secure | Out-Null
    $env:NOREA_CODESIGN_PFX_PATH = $testPfx
    $env:NOREA_CODESIGN_PFX_PASSWORD = "norea-test"
    $publisher = $cert.Subject
    Write-Host "Certificato di prova creato. Per installare l'MSIX importalo fra gli Autori attendibili."
} elseif (-not [string]::IsNullOrWhiteSpace($env:NOREA_CODESIGN_PFX_PATH) -or -not [string]::IsNullOrWhiteSpace($env:NOREA_CODESIGN_PFX_BASE64)) {
    $pfxForSubject = $env:NOREA_CODESIGN_PFX_PATH
    $tempSubjectPfx = $null
    if (-not [string]::IsNullOrWhiteSpace($env:NOREA_CODESIGN_PFX_BASE64)) {
        $tempSubjectPfx = Join-Path $OutputDir "subject.pfx"
        [IO.File]::WriteAllBytes($tempSubjectPfx, [Convert]::FromBase64String($env:NOREA_CODESIGN_PFX_BASE64))
        $pfxForSubject = $tempSubjectPfx
    }
    $subjectCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($pfxForSubject, $env:NOREA_CODESIGN_PFX_PASSWORD)
    $publisher = $subjectCert.Subject
    if ($tempSubjectPfx) { Remove-Item -LiteralPath $tempSubjectPfx -Force }
}

$manifest = Get-Content -LiteralPath (Join-Path $root "installer\AppxManifest.xml") -Raw
$manifest = $manifest.Replace("__PUBLISHER__", (ConvertTo-XmlText $publisher)).Replace("__VERSION__", $identityVersion)
Set-Content -LiteralPath (Join-Path $layout "AppxManifest.xml") -Value $manifest -Encoding utf8

$packed = Join-Path $OutputDir "NOREA_Suite.msix"
if (Test-Path $packed) { Remove-Item -LiteralPath $packed -Force }
& $makeAppx pack /d $layout /p $packed /o
if ($LASTEXITCODE -ne 0) { throw "makeappx ha restituito $LASTEXITCODE" }

. (Join-Path $root "firma_authenticode.ps1")
Invoke-NoreaAuthenticode -Files @($packed)
$willSign = (Test-NoreaSigningRequested @("NOREA_CODESIGN_PFX_BASE64", "NOREA_CODESIGN_PFX_PATH", "NOREA_CODESIGN_PFX_PASSWORD", "NOREA_AZURE_SIGNING_ENDPOINT", "NOREA_AZURE_SIGNING_ACCOUNT", "NOREA_AZURE_SIGNING_PROFILE", "NOREA_AZURE_TENANT_ID", "NOREA_AZURE_CLIENT_ID", "NOREA_AZURE_CLIENT_SECRET"))
if (-not $willSign) {
    $unsigned = Join-Path $OutputDir "NOREA_Suite_unsigned.msix"
    Move-Item -LiteralPath $packed -Destination $unsigned -Force
    Write-Host "MSIX non firmato: $unsigned"
} else {
    Write-Host "MSIX firmato: $packed"
}
if ($testPfx) { Remove-Item -LiteralPath $testPfx -Force -ErrorAction SilentlyContinue }
