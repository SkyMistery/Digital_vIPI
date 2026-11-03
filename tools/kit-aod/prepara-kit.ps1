# Prepara lo zip del kit di prova per gli AOD (carta docs/feature/2026-11-03-kit-di-prova-per-gli-aod.md, fetta 4).
# Dal commit registrato: pubblica il Lab completo (win-x64, self-contained) in "app", mette accanto "Avvia il Lab.cmd" e
# il LEGGIMI (con a capo di Windows e l'impronta del commit), fa l'autoprova e lo zip; accanto, il testo della release e
# il comando per pubblicarla (K10).
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\kit-aod\prepara-kit.ps1 [-Uscita <cartella>]
param([string]$Uscita = 'D:\Programmazione\IVAO_Test\SectorLab-prova\kit-aod')

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (git -C $repo status --porcelain) { throw "L'albero non e' pulito: il kit si fa da un commit registrato." }
$sha = (git -C $repo rev-parse --short=8 HEAD).Trim()

$nome = 'Aurora Sector Lab (prova)'
$cartella = Join-Path $Uscita $nome
if (Test-Path $cartella) { Remove-Item -Recurse -Force $cartella }
New-Item -ItemType Directory -Force $cartella | Out-Null

dotnet publish (Join-Path $repo 'src\Vipi.SectorLab') -c Release -r win-x64 --self-contained -o (Join-Path $cartella 'app')
if ($LASTEXITCODE -ne 0) { throw "dotnet publish e' fallito ($LASTEXITCODE)." }

# Il .cmd con gli a capo di Windows; il LEGGIMI anche, in UTF-8 col BOM (il Blocco note di Windows 10 lo legge giusto).
$cmd = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Avvia il Lab.cmd')) -replace "`r?`n", "`r`n"
[IO.File]::WriteAllText((Join-Path $cartella 'Avvia il Lab.cmd'), $cmd, (New-Object Text.UTF8Encoding $false))
$leggimi = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'LEGGIMI.txt')) -replace "`r?`n", "`r`n"
$leggimi = $leggimi.Replace('{SHA}', $sha).Replace('{DATA}', (Get-Date -Format 'dd/MM/yyyy'))
[IO.File]::WriteAllText((Join-Path $cartella 'LEGGIMI.txt'), $leggimi, (New-Object Text.UTF8Encoding $true))

# L'autoprova usa una cartella temporanea sua: nel kit non resta niente.
$prova = Start-Process (Join-Path $cartella 'app\VipiSectorLab.exe') -ArgumentList '--autoprova' -Wait -PassThru
if ($prova.ExitCode -ne 0) { throw "L'autoprova e' uscita con $($prova.ExitCode)." }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $Uscita "AuroraSectorLab-prova-$sha.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
[IO.Compression.ZipFile]::CreateFromDirectory($cartella, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Output "Kit pronto: $zip ($mb MB, eseguibile $sha, autoprova 0)"

# La pagina della release (K10): pre-release sul fork di prova, dove gli AOD sono invitati e mandano PR e esiti. Il testo
# e il comando si preparano qui; la release crea un tag, quindi la pubblica il committente (o la chat Master), mai il Lab.
$nomeZip = Split-Path -Leaf $zip
$note = Join-Path $Uscita "note-della-release-$sha.md"
$testo = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'RELEASE.md'))
$testo = $testo.Replace('{SHA}', $sha).Replace('{DATA}', (Get-Date -Format 'dd/MM/yyyy')).Replace('{ZIP}', $nomeZip)
[IO.File]::WriteAllText($note, $testo, (New-Object Text.UTF8Encoding $false))
$comando = "gh release create lab-prova-$sha `"$zip`" --repo SkyMistery/it-aurora-sector-test --target master " +
    "--prerelease --title `"Aurora Sector Lab, prova per gli AOD ($sha)`" --notes-file `"$note`""
[IO.File]::WriteAllText((Join-Path $Uscita "pubblica-la-release-$sha.txt"), $comando + "`r`n", (New-Object Text.UTF8Encoding $false))
Write-Output "Note della release: $note"
Write-Output "Per pubblicarla (committente o chat Master): $comando"
