#Requires -Version 5.1
<#
    U-123 (revisione 3, S39): prova della seconda rete di tools/prepara-pacchetto.ps1, in una copia finta del
    repo sotto %TEMP%. Uso:  powershell -File docs/history/revisione-totale-3/prova-rete-segreti.ps1 [-Script <ps1>]
    Senza -Script prova tools/prepara-pacchetto.ps1 del repo. I valori «segreti» sono inventati.

    Atteso con la rete di oggi: 1 PASSA, 2 FERMO, 3 FERMO, 4 FERMO, 5 PASSA.
    Con quella di prima:        1 FERMO (appsettings.json), 2 PASSA (chiave RFO), 3 PASSA (docs/), 4 FERMO, 5 FERMO.
#>
param([string]$Script)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
if (-not $Script) { $Script = Join-Path $repo 'tools\prepara-pacchetto.ps1' }

function Scenario($nome, [hashtable]$dichiarati, [hashtable]$inDocs) {
    $finto = Join-Path $env:TEMP ('vipi-rete-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path (Join-Path $finto 'tools'), (Join-Path $finto 'deploy\atc-ivao') | Out-Null
    Copy-Item $Script (Join-Path $finto 'tools\prepara-pacchetto.ps1')
    foreach ($f in 'LEGGIMI-SEGRETI.md', 'LEGGIMI-AGGIORNARE-VIA-FTP.md', 'LEGGIMI-TRADUZIONE.md') {
        Copy-Item (Join-Path $repo "deploy\atc-ivao\$f") (Join-Path $finto "deploy\atc-ivao\$f")
    }
    Copy-Item (Join-Path $repo 'deploy\atc-ivao\LEGGIMI-PACCHETTO-1.33.0.md') (Join-Path $finto 'deploy\atc-ivao\LEGGIMI-PACCHETTO-9.9.9.md')

    $cart = Join-Path $finto 'artifacts\publish\solo-prova-9.9.9'
    $elenco = Join-Path $finto 'elenco.txt'
    $righe = @()
    foreach ($k in $dichiarati.Keys) {
        $dest = Join-Path $cart ($k -replace '/', '\')
        New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
        [IO.File]::WriteAllText($dest, $dichiarati[$k])
        $righe += $k
    }
    $righe | Set-Content $elenco -Encoding ascii
    $docs = Join-Path $finto 'artifacts\publish\docs'
    New-Item -ItemType Directory -Force -Path $docs | Out-Null
    if ($inDocs) { foreach ($k in $inDocs.Keys) { [IO.File]::WriteAllText((Join-Path $docs $k), $inDocs[$k]) } }

    $pp = Join-Path $finto 'tools\prepara-pacchetto.ps1'
    & powershell -NoProfile -File $pp -Azione Impronte -Pacchetto solo-prova-9.9.9 -Versione 9.9.9 -Elenco $elenco | Out-Null
    $out = & powershell -NoProfile -File $pp -Azione Zip -Pacchetto solo-prova-9.9.9 -Versione 9.9.9 -SoloProva 2>&1 | Out-String
    $esito = if ($out -match 'FERMO') { 'FERMO' } else { 'PASSA' }
    Write-Host ("{0,-55} {1}" -f $nome, $esito)
    Remove-Item $finto -Recurse -Force
}

$appsettings = Get-Content (Join-Path $repo 'src\Vipi.Host\appsettings.json') -Raw
$rfo = 'rfo_' + 'Qm9ndXNLZXlOb3RSZWFsX0FCQ0RFRkdISUpLTE1OT1BRUlM'
$dll = 'MZ finto'

Scenario '1 appsettings.json dichiarato (valori vuoti)' @{ 'appsettings.json' = $appsettings; 'bin/Vipi.Host.dll' = $dll } $null
Scenario '2 file col solo blocco Rfo dichiarato' @{ 'bin/Vipi.Host.dll' = $dll; 'rfo-chiavi.json' = ('{"Rfo":{"Chiavi":[{"Nome":"prova","Chiave":"' + $rfo + '"}]}}') } $null
Scenario '3 segreti copiati in docs/' @{ 'bin/Vipi.Host.dll' = $dll } @{ 'k7f3.json' = '{"Ivao":{"ClientId":"abc","ClientSecret":"s3gr3toV3ro-9x"}}' }
Scenario '4 segreti di produzione dichiarati' @{ 'k7f3a91c.json' = '{"ConnectionStrings":{"Vipi":"Server=localhost;User Id=u;Password=Xy7qPz9w;"}}' } $null
Scenario '5 solo i fogli di deploy veri in docs/' @{ 'bin/Vipi.Host.dll' = $dll; 'appsettings.json' = $appsettings } $null
