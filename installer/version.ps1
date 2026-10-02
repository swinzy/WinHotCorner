# Works out the version of this build, as described in docs/technical.md ("Versions"), and returns it as an object.
#
#   major.minor.build.revision
#   major     src\Directory.Build.props (VersionMajor), changed by hand
#   minor     the development line of that major: metadata/line-<major>.<minor> tags
#   build     GitHub Actions run number minus the line's baseline run (0 for local builds)
#   revision  how many times this commit was built before in this line; for a release, how many times its
#             run was re-run (0 for local builds)
#
# In GitHub Actions it can start the first line of a new major version, by pushing its metadata tag.
param(
    # Build a release: the next build of the line, named with three numbers (GitHub Actions only)
    [switch]$Release
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$inCI = $env:GITHUB_ACTIONS -eq 'true'

# Runs git in the repository; $null if it fails (no git, or not a repository)
function Invoke-Git {
    $output = & git -C $root @args 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return $output
}

# The baseline run number recorded in a line tag
function Get-Baseline([string]$tag) {
    $text = (Invoke-Git for-each-ref "refs/tags/$tag" --format='%(contents)') -join "`n"
    $match = [regex]::Match($text, 'baseline-run:\s*(\d+)')
    if (-not $match.Success) { throw "Tag $tag has no 'baseline-run: <number>' in its message" }
    return [int]$match.Groups[1].Value
}

$props = [IO.File]::ReadAllText((Join-Path $root 'src\Directory.Build.props'))
$majorMatch = [regex]::Match($props, '<VersionMajor>(\d+)</VersionMajor>')
if (-not $majorMatch.Success) { throw 'No <VersionMajor> in src\Directory.Build.props' }
$major = [int]$majorMatch.Groups[1].Value

$commit = Invoke-Git rev-parse --short=7 HEAD
if (-not $commit) { $commit = 'unknown' }

# Development lines of this major, and its releases
$lines = @(Invoke-Git tag --list "metadata/line-$major.*" | ForEach-Object {
    if ($_ -match "^metadata/line-$major\.(\d+)$") { [int]$Matches[1] } })
$releases = @(Invoke-Git tag --list "v$major.*" | ForEach-Object {
    if ($_ -match "^v$major\.(\d+)\.(\d+)$") { [pscustomobject]@{ Minor = [int]$Matches[1]; Build = [int]$Matches[2] } } })
$minor = if ($lines.Count) { ($lines | Measure-Object -Maximum).Maximum } else { 0 }
$lineTag = "metadata/line-$major.$minor"
$lineReleased = [bool]($releases | Where-Object Minor -eq $minor)

if (-not $inCI) {
    if ($Release) { throw 'Releases are built by GitHub Actions only' }
    # Anyone can build locally, so a local build cannot have a build number: it is always 0
    $build = 0
    $revision = 0
}
else {
    $runNumber = [int]$env:GITHUB_RUN_NUMBER
    $runAttempt = [int]$env:GITHUB_RUN_ATTEMPT

    if (-not $lines.Count) {
        # The major version was changed and has no line yet: this run starts line <major>.0 as its build 1
        $baseline = $runNumber - 1
        if ($env:GITHUB_EVENT_NAME -ne 'pull_request') {
            & git -C $root -c user.name='github-actions[bot]' -c user.email='41898282+github-actions[bot]@users.noreply.github.com' `
                tag -a $lineTag -m "Start of the $major.$minor development line" -m "baseline-run: $baseline" HEAD
            & git -C $root push origin "refs/tags/$lineTag"
            if ($LASTEXITCODE -ne 0) {
                # Another run created it first: use theirs
                & git -C $root fetch --force origin "refs/tags/${lineTag}:refs/tags/$lineTag"
                $baseline = Get-Baseline $lineTag
            }
        }
    }
    else {
        if ($lineReleased) {
            throw "Line $major.$minor is released but there is no metadata/line-$major.$($minor + 1) tag. The release run should have created it; create it by hand with the release's run number: git tag -a metadata/line-$major.$($minor + 1) -m 'baseline-run: <run number>'"
        }
        $baseline = Get-Baseline $lineTag
    }

    $build = $runNumber - $baseline
    $revision = 0

    if ($Release) {
        if ($env:GITHUB_REF -ne 'refs/heads/main') { throw 'Releases are made from main only' }
        if (Invoke-Git tag --points-at HEAD --list 'v*') { throw 'This commit is already released' }
        # A release is the line's next build even if CI built this commit before. A re-run of the release run
        # (for reasons other than the code) keeps the build number and raises the revision; the release is still
        # named with three numbers
        $revision = $runAttempt - 1
    }
    elseif ($env:GITHUB_EVENT_NAME -ne 'pull_request') {
        # The same commit built before in this line keeps its build number; each rebuild raises the revision.
        # (Pull request runs build a temporary merge commit, which is always new.)
        $headers = @{ Accept = 'application/vnd.github+json' }
        if ($env:GH_TOKEN) { $headers.Authorization = "Bearer $env:GH_TOKEN" }
        $workflow = ($env:GITHUB_WORKFLOW_REF -split '@')[0] -replace "^$([regex]::Escape($env:GITHUB_REPOSITORY))/", ''
        $runs = (Invoke-RestMethod -Headers $headers -Uri "$env:GITHUB_API_URL/repos/$env:GITHUB_REPOSITORY/actions/runs?head_sha=$env:GITHUB_SHA&per_page=100").workflow_runs |
            Where-Object { $_.path -eq $workflow -and $_.run_number -gt $baseline -and $_.run_number -lt $runNumber }
        if ($runs) {
            $build = (($runs | Measure-Object run_number -Minimum).Minimum) - $baseline
            $revision = ($runs | Measure-Object run_attempt -Sum).Sum
        }
    }

    # A re-run of this run is a rebuild too (for a release it is already counted above)
    if (-not $Release) { $revision += $runAttempt - 1 }
}

foreach ($part in $major, $minor, $build, $revision) {
    if ($part -gt 65535) { throw "Version part $part is over 65535, the most Windows allows" }
}

$numeric = "$major.$minor.$build.$revision"
if ($Release) {
    $short = "$major.$minor.$build"
    $display = $short
    $fileName = $short
    $informational = "$numeric+$commit"
}
elseif ($inCI) {
    $display = "$numeric+$commit"
    $fileName = $numeric
    $informational = $display
}
else {
    # A random id tells local builds apart, by different people or at different times
    $display = "$numeric+$commit.local." + [guid]::NewGuid().ToString('N').Substring(0, 8)
    if (Invoke-Git status --porcelain) { $display += '.devel' }
    $fileName = $numeric
    $informational = $display
}

[pscustomobject]@{
    Numeric       = $numeric       # file and assembly version
    Informational = $informational # product version string
    Display       = $display       # shown in Installed apps
    FileName      = $fileName      # in the installer file names
    Major         = $major
    Minor         = $minor
    Build         = $build
    Revision      = $revision
    Release       = [bool]$Release
}
