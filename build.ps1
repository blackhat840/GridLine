param(
    [ValidatePattern('^[A-Za-z0-9_-]+\.exe$')]
    [string]$OutputName = 'GridLine.exe'
)

$ErrorActionPreference = 'Stop'

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'GridLine requires the Windows .NET Framework desktop runtime on a 64-bit Windows PC.'
}

$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$executable = Join-Path $output $OutputName
$references = @(
    'WPF\WindowsBase.dll'
    'WPF\PresentationCore.dll'
    'WPF\PresentationFramework.dll'
    'System.Xaml.dll'
    'System.Runtime.Serialization.dll'
    'System.Net.Http.dll'
    'System.Security.dll'
) | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src\GridLine') -Filter '*.cs' -File |
    ForEach-Object { $_.FullName }

$arguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:x64'
    '/langversion:5'
    '/optimize+'
    '/win32icon:' + (Join-Path $PSScriptRoot 'assets\brand\gridline.ico')
    '/out:' + $executable
) + $references + $sources

& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "GridLine compilation failed with exit code $LASTEXITCODE." }
Write-Output "Built $executable"
