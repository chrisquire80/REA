# Genera CHANGELOG.md dai messaggi di commit fra i tag v* e, se richiesto,
# la sezione di un tag da usare come corpo della GitHub Release.
param(
    [string]$RepoRoot = "",
    [string]$ChangelogPath = "",
    [string]$Tag = "",
    [string]$NotesPath = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}
if ([string]::IsNullOrWhiteSpace($ChangelogPath)) {
    $ChangelogPath = Join-Path $RepoRoot "CHANGELOG.md"
}

function Get-CommitSubjects {
    param([string]$Range)
    $output = git -C $RepoRoot log --no-merges --pretty=format:%s $Range
    if ($LASTEXITCODE -ne 0) { throw "git log non riuscito per $Range" }
    $subjects = @()
    foreach ($line in ($output -split "`n")) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -gt 0) { $subjects += $trimmed }
    }
    return $subjects
}

function Format-Section {
    param([string]$Heading, [string[]]$Subjects)
    $lines = @("## $Heading", "")
    if ($Subjects.Count -eq 0) {
        $lines += "- Nessun cambiamento registrato."
    } else {
        foreach ($subject in $Subjects) { $lines += "- $subject" }
    }
    $lines += ""
    return $lines
}

Push-Location $RepoRoot
try {
    $tags = @(git tag -l "v*" --sort=v:refname)
    if ($LASTEXITCODE -ne 0) { throw "Impossibile elencare i tag." }

    $sections = New-Object System.Collections.Generic.List[string]
    $previous = $null
    foreach ($name in $tags) {
        $range = if ($null -eq $previous) { $name } else { "$previous..$name" }
        $subjects = @(Get-CommitSubjects $range)
        $block = Format-Section $name $subjects
        # Inseriti in testa: il changelog elenca prima la versione più recente.
        $sections.Insert(0, ($block -join "`n"))
        $previous = $name
    }

    $unreleased = @()
    if ($tags.Count -gt 0) {
        $unreleased = @(Get-CommitSubjects "$($tags[-1])..HEAD")
    }
    $header = @(
        "# Changelog",
        "",
        "Generato dai messaggi di commit fra i tag ``v*``. Il workflow di release aggiorna questo file e usa la sezione del tag come note della release.",
        ""
    )
    $body = New-Object System.Collections.Generic.List[string]
    foreach ($line in $header) { [void]$body.Add($line) }
    if ($unreleased.Count -gt 0) {
        $body.Add((Format-Section "Non rilasciato" $unreleased) -join "`n")
    }
    foreach ($section in $sections) { $body.Add($section) }

    $text = ($body -join "`n").TrimEnd() + "`n"
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($ChangelogPath, $text, $utf8)

    if (-not [string]::IsNullOrWhiteSpace($Tag)) {
        $matched = $false
        $noteLines = New-Object System.Collections.Generic.List[string]
        foreach ($section in $sections) {
            if ($section.StartsWith("## $Tag`n") -or $section.StartsWith("## $Tag`r")) {
                $noteLines.Add($section.TrimEnd())
                $matched = $true
                break
            }
        }
        if (-not $matched) { throw "Il tag $Tag non ha una sezione nel changelog." }
        $notes = ($noteLines -join "`n").TrimEnd() + "`n"
        if ([string]::IsNullOrWhiteSpace($NotesPath)) {
            Write-Output $notes
        } else {
            [System.IO.File]::WriteAllText($NotesPath, $notes, $utf8)
        }
    }
}
finally {
    Pop-Location
}
