# Builds both installers into installer\Output (Windows, needs git, the .NET SDK and Inno Setup 7):
#   WinHotCorner-Full-<version>-setup.exe           the hot corner and the control panel
#   WinHotCorner-HotCornerOnly-<version>-setup.exe  the hot corner only
#
# The version comes from version.ps1 (see "Versions" in docs\technical.md): a local build is
# <major>.<minor>.0.0, GitHub Actions gives each build its number. -Release builds a release (GitHub Actions only).
param([switch]$Release)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $PSScriptRoot 'Output'
$iscc = Join-Path ${env:ProgramFiles} 'Inno Setup 7\ISCC.exe'

function Invoke-Checked([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit code $LASTEXITCODE)" }
}

$version = & (Join-Path $PSScriptRoot 'version.ps1') -Release:$Release
"Version $($version.Display)"
$versionProperties = @(
    "-p:Version=$($version.Numeric)"
    "-p:FileVersion=$($version.Numeric)"
    "-p:AssemblyVersion=$($version.Numeric)"
    "-p:InformationalVersion=$($version.Informational)"
)
$innoDefines = @("/DAppVersion=$($version.Display)", "/DFileNameVersion=$($version.FileName)")

Remove-Item -Recurse -Force $output -ErrorAction SilentlyContinue

Invoke-Checked 'Building the hot corner' { dotnet build (Join-Path $root 'src\WinHotCorner\WinHotCorner.csproj') -c Release -nologo -v:q @versionProperties }
Invoke-Checked 'Publishing the control panel' { dotnet publish (Join-Path $root 'src\ControlPanel\ControlPanel.csproj') -c Release -nologo -v:q -o (Join-Path $output 'ControlPanel') @versionProperties }

# The hot corner installer first: the control panel installer contains it
Invoke-Checked 'Compiling WinHotCorner.iss' { & $iscc /Q @innoDefines (Join-Path $PSScriptRoot 'WinHotCorner.iss') }
Invoke-Checked 'Compiling ControlPanel.iss' { & $iscc /Q @innoDefines (Join-Path $PSScriptRoot 'ControlPanel.iss') }

Get-ChildItem $output -Filter *.exe | ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }

# For the GitHub Actions workflow: naming the artifacts and the release, starting the next line
if ($env:GITHUB_OUTPUT) {
    @(
        "file-name-version=$($version.FileName)"
        "major=$($version.Major)"
        "minor=$($version.Minor)"
    ) | Add-Content $env:GITHUB_OUTPUT
}
