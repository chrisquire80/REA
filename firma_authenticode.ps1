# Firma Authenticode opzionale. Senza segreti non fa nulla e non cambia il pacchetto.
# Con certificato: NOREA_CODESIGN_PFX_BASE64 oppure NOREA_CODESIGN_PFX_PATH, e
# NOREA_CODESIGN_PFX_PASSWORD (può essere vuota).
# In alternativa Azure Artifact Signing (ex Trusted Signing): NOREA_AZURE_SIGNING_ENDPOINT,
# NOREA_AZURE_SIGNING_ACCOUNT, NOREA_AZURE_SIGNING_PROFILE, NOREA_AZURE_TENANT_ID,
# NOREA_AZURE_CLIENT_ID, NOREA_AZURE_CLIENT_SECRET.
# Se sono presenti sia il PFX sia Azure, si usa il PFX.

function Test-NoreaSigningRequested {
    param([string[]]$Names)
    foreach ($name in $Names) {
        if (-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) { return $true }
    }
    return $false
}

function Invoke-NoreaAuthenticode {
    param([Parameter(Mandatory = $true)][string[]]$Files)

    $pfxNames = @("NOREA_CODESIGN_PFX_BASE64", "NOREA_CODESIGN_PFX_PATH", "NOREA_CODESIGN_PFX_PASSWORD")
    $azureNames = @(
        "NOREA_AZURE_SIGNING_ENDPOINT", "NOREA_AZURE_SIGNING_ACCOUNT", "NOREA_AZURE_SIGNING_PROFILE",
        "NOREA_AZURE_TENANT_ID", "NOREA_AZURE_CLIENT_ID", "NOREA_AZURE_CLIENT_SECRET"
    )
    $pfxRequested = Test-NoreaSigningRequested $pfxNames
    $azureRequested = Test-NoreaSigningRequested $azureNames
    if (-not $pfxRequested -and -not $azureRequested) { return }

    $existing = @($Files | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) })
    if ($existing.Count -eq 0) { throw "Firma richiesta ma nessun file da firmare." }

    if ($pfxRequested -and [string]::IsNullOrWhiteSpace($env:NOREA_CODESIGN_PFX_BASE64) -and [string]::IsNullOrWhiteSpace($env:NOREA_CODESIGN_PFX_PATH)) {
        throw "NOREA_CODESIGN_PFX_PASSWORD è impostata ma manca NOREA_CODESIGN_PFX_BASE64 o NOREA_CODESIGN_PFX_PATH."
    }

    if ($pfxRequested) {
        Invoke-NoreaPfxSign -Files $existing
        return
    }

    foreach ($name in $azureNames) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
            throw "Firma Azure incompleta: manca $name."
        }
    }
    Invoke-NoreaAzureSign -Files $existing
}

function Invoke-NoreaPfxSign {
    param([string[]]$Files)
    $temporary = $null
    try {
        $pfxPath = $env:NOREA_CODESIGN_PFX_PATH
        if (-not [string]::IsNullOrWhiteSpace($env:NOREA_CODESIGN_PFX_BASE64)) {
            $temporary = Join-Path ([System.IO.Path]::GetTempPath()) ("norea-codesign-" + [guid]::NewGuid().ToString("N") + ".pfx")
            [System.IO.File]::WriteAllBytes($temporary, [Convert]::FromBase64String($env:NOREA_CODESIGN_PFX_BASE64))
            $pfxPath = $temporary
        }
        if (-not (Test-Path -LiteralPath $pfxPath)) { throw "Il file PFX non esiste." }

        $signTool = Get-NoreaSignTool
        foreach ($file in $Files) {
            $arguments = @("sign", "/fd", "SHA256", "/td", "SHA256", "/tr", "http://timestamp.digicert.com", "/f", $pfxPath)
            if (-not [string]::IsNullOrEmpty($env:NOREA_CODESIGN_PFX_PASSWORD)) {
                $arguments += @("/p", $env:NOREA_CODESIGN_PFX_PASSWORD)
            }
            $arguments += $file
            & $signTool @arguments | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "signtool ha restituito $LASTEXITCODE per $file" }
        }
        Write-Host "Authenticode: firmati $($Files.Count) file con il certificato PFX."
    }
    finally {
        if ($temporary -and (Test-Path -LiteralPath $temporary)) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Invoke-NoreaAzureSign {
    param([string[]]$Files)
    $toolDir = Join-Path ([System.IO.Path]::GetTempPath()) ("norea-sign-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $toolDir | Out-Null
    try {
        dotnet tool install --tool-path $toolDir sign --version 1.1.5
        if ($LASTEXITCODE -ne 0) { throw "Installazione dello strumento sign non riuscita." }
        $env:AZURE_TENANT_ID = $env:NOREA_AZURE_TENANT_ID
        $env:AZURE_CLIENT_ID = $env:NOREA_AZURE_CLIENT_ID
        $env:AZURE_CLIENT_SECRET = $env:NOREA_AZURE_CLIENT_SECRET
        $sign = Join-Path $toolDir "sign.exe"
        if (-not (Test-Path -LiteralPath $sign)) { $sign = Join-Path $toolDir "sign" }
        & $sign code artifact-signing `
            --artifact-signing-endpoint $env:NOREA_AZURE_SIGNING_ENDPOINT `
            --artifact-signing-account $env:NOREA_AZURE_SIGNING_ACCOUNT `
            --artifact-signing-certificate-profile $env:NOREA_AZURE_SIGNING_PROFILE `
            --timestamp-url "http://timestamp.digicert.com" `
            --verbosity warning `
            @Files
        if ($LASTEXITCODE -ne 0) { throw "sign code artifact-signing ha restituito $LASTEXITCODE" }
        Write-Host "Authenticode: firmati $($Files.Count) file con Azure Artifact Signing."
    }
    finally {
        Remove-Item -LiteralPath $toolDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-NoreaSignTool {
    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    )
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $found = Get-ChildItem -LiteralPath $root -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    throw "signtool.exe non trovato. Su Windows serve il Windows SDK."
}
