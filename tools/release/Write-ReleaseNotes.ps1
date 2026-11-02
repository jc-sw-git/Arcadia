<#
.SYNOPSIS
  Writes the change list of a release: one entry per issue, grouped by kind (rules: docs/releases.md).
.DESCRIPTION
  Reads the PRs merged into main between the previous release tag and -Tag (first-parent history), takes the issues
  each PR names in its body ("Closes #N", "Fixes #N", "Resolves #N", "Part of #N") and lists every issue once,
  however many PRs it took. A PR that names no issue gets an entry of its own. Left out:
  - PRs labelled `no-changelog` (docs, process, tests, CI, spikes);
  - issues labelled `no-changelog`;
  - bug issues whose body says "Introduced by #N" for a PR N in the same range or an issue such a PR names: that bug
    never shipped.
  The previous tag of a stable release is the previous stable one ("Was ist neu" shows stable releases only), so its
  notes cover every release candidate before it. Needs `git` with tags fetched and an authenticated `gh`.
.EXAMPLE
  ./tools/release/Write-ReleaseNotes.ps1 -Tag v0.2.0 -Repository jc-sw-git/Arcadia
#>
param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Repository,
    # Overrides the automatic choice; for checking the notes of an older range.
    [string]$PreviousTag
)

$ErrorActionPreference = 'Stop'

function ConvertTo-SemVer([string]$name) {
    if ($name -notmatch '^v(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$') { return $null }
    [pscustomobject]@{ Tag = $name; Core = [version]"$($Matches[1]).$($Matches[2]).$($Matches[3])"; Pre = $Matches[4] }
}

# SemVer precedence: a pre-release sorts before its release; numeric identifiers compare as numbers.
function Compare-SemVer($a, $b) {
    $c = $a.Core.CompareTo($b.Core)
    if ($c -ne 0) { return $c }
    if (-not $a.Pre -and -not $b.Pre) { return 0 }
    if (-not $a.Pre) { return 1 }
    if (-not $b.Pre) { return -1 }
    $x = $a.Pre.Split('.'); $y = $b.Pre.Split('.')
    for ($i = 0; $i -lt [Math]::Min($x.Count, $y.Count); $i++) {
        $nx = 0; $ny = 0
        $xNum = [int]::TryParse($x[$i], [ref]$nx); $yNum = [int]::TryParse($y[$i], [ref]$ny)
        $c = if ($xNum -and $yNum) { $nx.CompareTo($ny) } elseif ($xNum) { -1 } elseif ($yNum) { 1 } else { [string]::CompareOrdinal($x[$i], $y[$i]) }
        if ($c -ne 0) { return [Math]::Sign($c) }
    }
    return $x.Count.CompareTo($y.Count)
}

function Find-PreviousTag([string]$tag) {
    $current = ConvertTo-SemVer $tag
    if (-not $current) { throw "Tag $tag is not a SemVer version like v1.2.3" }
    $older = git tag --list 'v*' | ForEach-Object { ConvertTo-SemVer $_ } |
        Where-Object { $_ -and (Compare-SemVer $_ $current) -lt 0 -and ($current.Pre -or -not $_.Pre) }
    $newest = $null
    foreach ($candidate in $older) { if (-not $newest -or (Compare-SemVer $candidate $newest) -gt 0) { $newest = $candidate } }
    return $newest.Tag
}

function Get-Json([string]$path) { gh api $path | ConvertFrom-Json }

function Get-Kind($labels, [string]$branch) {
    if ($labels -contains 'bug' -or $branch -like 'fix/*') { return 'Fixes' }
    if ($labels -contains 'type:tech-debt' -or $branch -like 'td/*') { return 'Under the hood' }
    return 'New and changed'
}

if (-not $PreviousTag) { $PreviousTag = Find-PreviousTag $Tag }
$range = if ($PreviousTag) { "$PreviousTag..$Tag" } else { $Tag }

# Merge commits say "Merge pull request #N from …", squash merges end in "(#N)".
$prNumbers = git log --first-parent --reverse --format='%s' $range | ForEach-Object {
    if ($_ -match '^Merge pull request #(\d+)\b' -or $_ -match '\(#(\d+)\)$') { [int]$Matches[1] }
}
if ($LASTEXITCODE -ne 0) { throw "git log $range failed" }

$prs = foreach ($n in $prNumbers | Select-Object -Unique) {
    $pr = Get-Json "repos/$Repository/pulls/$n"
    $issues = [regex]::Matches([string]$pr.body, '(?im)\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?|part of)\s*:?\s+#(\d+)') |
        ForEach-Object { [int]$_.Groups[1].Value } | Select-Object -Unique
    [pscustomobject]@{
        Number = $n; Title = $pr.title.Trim(); Branch = $pr.head.ref
        Labels = @($pr.labels.name); Issues = @($issues)
    }
}
$prs = @($prs | Where-Object { $_.Labels -notcontains 'no-changelog' })
# What this release brings: a bug introduced by one of these never shipped.
$newInRange = @($prNumbers) + @($prs | ForEach-Object { $_.Issues })

$issueCache = @{}
function Get-Issue([int]$n) {
    if (-not $issueCache.ContainsKey($n)) { $issueCache[$n] = Get-Json "repos/$Repository/issues/$n" }
    $issueCache[$n]
}

function Test-IssueIncluded($issue) {
    if ($issue.pull_request) { return $false }   # "#N" named a PR, not an issue
    $labels = @($issue.labels.name)
    if ($labels -contains 'no-changelog') { return $false }
    if ($labels -contains 'bug') {
        foreach ($m in [regex]::Matches([string]$issue.body, '(?i)introduced (?:by|in) (?:PR )?#(\d+)')) {
            if ($newInRange -contains [int]$m.Groups[1].Value) { return $false }
        }
    }
    return $true
}

# A part cut from a larger issue is its sub-issue; the reader sees the larger issue only. Epics (no longer used, still
# the parent of some older issues) group unrelated work, so the climb stops below them.
function Get-ListedIssue([int]$n) {
    $issue = Get-Issue $n
    while ($issue.parent_issue_url) {
        $parentNumber = [int]($issue.parent_issue_url -replace '^.*/', '')
        $parent = Get-Issue $parentNumber
        if (@($parent.labels.name) -contains 'type:epic') { break }
        $n = $parentNumber; $issue = $parent
    }
    return $n
}

$entries = [ordered]@{}
foreach ($pr in $prs) {
    if ($pr.Issues.Count -eq 0) {
        $entries["pr$($pr.Number)"] = [pscustomobject]@{ Kind = Get-Kind $pr.Labels $pr.Branch; Text = "$($pr.Title) (#$($pr.Number))" }
        continue
    }
    foreach ($named in $pr.Issues) {
        $n = Get-ListedIssue $named
        if ($entries.Contains("issue$n")) { continue }
        $issue = Get-Issue $n
        if (Test-IssueIncluded $issue) {
            $entries["issue$n"] = [pscustomobject]@{ Kind = Get-Kind @($issue.labels.name) ''; Text = "$($issue.title.Trim()) (#$n)" }
        }
    }
}

$lines = foreach ($kind in 'New and changed', 'Fixes', 'Under the hood') {
    $items = @($entries.Values | Where-Object Kind -eq $kind)
    if ($items.Count -gt 0) { "### $kind"; $items | ForEach-Object { "* $($_.Text)" }; '' }
}
if ($PreviousTag) { $lines += "**Full changelog**: https://github.com/$Repository/compare/$PreviousTag...$Tag" }
$lines -join "`n"
