# Builds both installers into installer\Output (Windows, needs the .NET SDK and Inno Setup 7):
#   WinHotCorner-<version>-setup.exe              the hot corner only
#   WinHotCornerControlPanel-<version>-setup.exe  the control panel, installs the hot corner too if needed
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

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
