# Builds both installers into installer\Output (Windows, needs the .NET SDK and Inno Setup 7):
#   WinHotCorner-<version>-setup.exe              the hot corner only
#   WinHotCornerControlPanel-<version>-setup.exe  the control panel, installs the hot corner too if needed
#
# Every build raises the third number of the version (build) in src\Directory.Build.props first;
# -NoBump builds the current version again.
param([switch]$NoBump)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$props = Join-Path $root 'src\Directory.Build.props'
$text = [IO.File]::ReadAllText($props)
$match = [regex]::Match($text, '<Version>(\d+)\.(\d+)\.(\d+)\.(\d+)</Version>')
if (-not $match.Success) { throw "No <Version>a.b.c.d</Version> in $props" }
$version = $match.Groups[1..4] | ForEach-Object { [int]$_.Value }
if (-not $NoBump) {
    $version[2]++
    $text = $text.Substring(0, $match.Index) + "<Version>$($version -join '.')</Version>" + $text.Substring($match.Index + $match.Length)
    [IO.File]::WriteAllText($props, $text)
}
"Version $($version -join '.')"
$output = Join-Path $PSScriptRoot 'Output'
$iscc = Join-Path ${env:ProgramFiles} 'Inno Setup 7\ISCC.exe'

function Invoke-Checked([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit code $LASTEXITCODE)" }
}

Remove-Item -Recurse -Force $output -ErrorAction SilentlyContinue

Invoke-Checked 'Building the hot corner' { dotnet build (Join-Path $root 'src\WinHotCorner\WinHotCorner.csproj') -c Release -nologo -v:q }
Invoke-Checked 'Publishing the control panel' { dotnet publish (Join-Path $root 'src\ControlPanel\ControlPanel.csproj') -c Release -nologo -v:q -o (Join-Path $output 'ControlPanel') }

# The hot corner installer first: the control panel installer contains it
Invoke-Checked 'Compiling WinHotCorner.iss' { & $iscc /Q (Join-Path $PSScriptRoot 'WinHotCorner.iss') }
Invoke-Checked 'Compiling ControlPanel.iss' { & $iscc /Q (Join-Path $PSScriptRoot 'ControlPanel.iss') }

Get-ChildItem $output -Filter *.exe | ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
