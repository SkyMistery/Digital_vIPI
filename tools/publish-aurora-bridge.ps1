# Pubblica il tool desktop vIPI Aurora Bridge.
#
#   .\tools\publish-aurora-bridge.ps1              # win-x64, autonomo, file unico
#   .\tools\publish-aurora-bridge.ps1 -Runtime osx-arm64
#
# Autonomo (self-contained) di proposito: il tool gira sul PC di un controllore, non su un server, e
# pretendere che installi prima .NET è un ostacolo che si paga in supporto.

[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\Vipi.AuroraBridge\Vipi.AuroraBridge.csproj'
if (-not $Output) { $Output = Join-Path $root "artifacts\bridge\$Runtime" }

Write-Host "Pubblico $Runtime in $Output" -ForegroundColor Cyan

# PublishSingleFile tiene tutto in un eseguibile solo; le librerie native di Avalonia (Skia) restano
# comunque estratte a runtime, quindi niente IncludeNativeLibrariesForSelfExtract.
dotnet publish $project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:DebugType=none `
    --output $Output

if ($LASTEXITCODE -ne 0) { throw "dotnet publish ha fallito ($LASTEXITCODE)" }

$exe = Get-ChildItem $Output -Filter 'VipiAuroraBridge*' |
       Where-Object { $_.Extension -in '.exe', '' } |
       Select-Object -First 1

if ($exe) {
    $mb = [math]::Round($exe.Length / 1MB, 1)
    Write-Host "Fatto: $($exe.FullName) ($mb MB)" -ForegroundColor Green
} else {
    Write-Warning "Pubblicazione completata ma non trovo l'eseguibile in $Output"
}

Write-Host ""
Write-Host "Ricorda: in Aurora serve PVD -> F7 -> Other -> 3rd Party Software Access = YES," -ForegroundColor Yellow
Write-Host "riapplicato NELLA SESSIONE IN CORSO (il flag nel profilo da solo non apre la porta)." -ForegroundColor Yellow
