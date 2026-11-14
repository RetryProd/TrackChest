param([string]$Sortie = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$sdk = Join-Path $env:LOCALAPPDATA 'TrackChest\sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) {
    $sdk = (Get-Command dotnet -ErrorAction Stop).Source
}
if (-not (& $sdk --list-sdks | Where-Object { $_ -match '^10\.' })) {
    throw 'Installez le SDK .NET 10 x64 pour compiler TrackChest.'
}

$project = Join-Path $PSScriptRoot 'src\ChestCounter\TrackChest.csproj'
$output = $Sortie
& $sdk publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $output 'LICENSE-ChestCounter.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'lib\LICENSE-FortniteReplayDecompressor') -Destination (Join-Path $output 'LICENSE-FortniteReplayDecompressor.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'lib\OozSharp\COPYING') -Destination (Join-Path $output 'LICENSE-GPL-3.0.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CREDITS.md') -Destination $output
