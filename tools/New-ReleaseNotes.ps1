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

function Get-CuratedBody {
    param([string]$Heading)
    if ($Heading -notmatch '^v\d+') { return $null }
    $path = Join-Path $RepoRoot "docs/release-notes/$Heading.md"
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $raw = [System.IO.File]::ReadAllText($path).Trim()
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    return $raw
}

function Format-Section {
    param([string]$Heading, [string[]]$Subjects)
    $curated = Get-CuratedBody $Heading
    if ($curated) {
        return @("## $Heading", "", $curated, "")
    }
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
    $tags = @(git tag -l "v*" --sort=v:refname | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($LASTEXITCODE -ne 0) { throw "Impossibile elencare i tag." }

    if (-not [string]::IsNullOrWhiteSpace($Tag)) {
        if ($tags -notcontains $Tag) {
            throw "Il tag $Tag non è nel clone. Il checkout della release deve usare fetch-tags: true, altrimenti le note non possono essere delimitate rispetto al tag precedente."
        }
        # v2.1.0 è il primo tag della suite. Qualsiasi release successiva, se è
        # l'unico tag locale, includerebbe tutta la storia al posto del solo intervallo.
        if ($tags.Count -eq 1 -and $Tag -ne "v2.1.0") {
            throw "Nel clone c'è solo $Tag. Mancano i tag v* precedenti (serve fetch-tags: true)."
        }
        $tagIndex = [array]::IndexOf($tags, $Tag)
        if ($tagIndex -gt 0) {
            $previousTag = $tags[$tagIndex - 1]
            git -C $RepoRoot rev-parse --verify --quiet "refs/tags/$previousTag^{commit}" | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Il tag precedente $previousTag non risolve a un commit. Le note di $Tag non possono essere generate."
            }
        }
    }

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
        "Generato dai messaggi di commit fra i tag ``v*``. Se esiste ``docs/release-notes/<tag>.md``, quella sezione usa quel testo. Il workflow di release aggiorna questo file e usa la sezione del tag come note della release.",
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
