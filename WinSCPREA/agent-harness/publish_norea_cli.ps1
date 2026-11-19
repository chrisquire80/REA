$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = Join-Path $repoRoot "NoreaCli\NoreaCli.csproj"
$output = Join-Path $repoRoot "dist"

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
dotnet publish $project -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:NuGetAudit=false -o $output
if ($LASTEXITCODE -ne 0) {
    throw "Pubblicazione di NoreaCli non riuscita (exit code $LASTEXITCODE)."
}
