#Requires -Version 5.1
<#
.SYNOPSIS
    Prepara un pacchetto di consegna per atc.it.ivao.aero: ruota la consegna vecchia, scrive le impronte,
    costruisce lo zip. Il runbook per esteso è in docs/guide/preparare-un-pacchetto.md.

.DESCRIPTION
    Esiste per due cose che non si possono affidare alla memoria di chi consegna.

    ⚠️ LA PRIMA, ed è la ragione per cui questo file esiste. Il 31 agosto 2026 nella cartella dei file da
    caricare era comparso `k7f3a91c4atce8b2.json`, cioè i SEGRETI di produzione — connection string con la
    password, ClientSecret di IVAO. Lo zip veniva costruito camminando la cartella, quindi se l'era portato
    dentro: le credenziali dentro il file che si spedisce per posta. Quel file è protetto SOLO dal nome non
    indovinabile, e dentro un allegato non è protetto da niente. Non era la prima volta: la stessa cosa sta
    in publish_old/20260824-i/solo-4-file-i/, del 24 agosto, insieme a una chiave del key-ring.

    Perciò lo zip si costruisce dall'ELENCO DICHIARATO (IMPRONTE.txt), mai dalla cartella, e c'è una seconda
    rete che guarda dentro i file dichiarati: un `.json` che contiene una password o un ClientSecret ferma
    il pacchetto anche se qualcuno l'avesse messo in elenco.

    ⚠️ LA SECONDA: i documenti non stanno con i file da caricare. Un `.md` sul server non fa danno, ma una
    cartella che mescola «si carica» e «si legge» è il modo in cui si finisce per caricare la cosa
    sbagliata. Nello zip sono due rami paralleli.

.PARAMETER Azione
    Ruota    la consegna che sta in publish/ passa in publish_old/<data>/, con i suoi docs/.
    Impronte (ri)scrive IMPRONTE.txt nella cartella del pacchetto, dall'elenco passato con -Elenco.
    Zip      costruisce lo zip da IMPRONTE.txt + docs/, con le due reti descritte sopra.

.EXAMPLE
    .\tools\prepara-pacchetto.ps1 -Azione Zip -Pacchetto solo-18-file-1.1.0 -Versione 1.1.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Ruota', 'Impronte', 'Zip')][string]$Azione,
    [string]$Pacchetto,
    [string]$Versione,
    [string]$Elenco,
    [switch]$SoloProva
)

$ErrorActionPreference = 'Stop'
$radice  = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $radice 'artifacts\publish'
$vecchi  = Join-Path $radice 'artifacts\publish_old'
$fogli   = Join-Path $radice 'deploy\atc-ivao'

function Fermati($messaggio) {
    Write-Host ''
    Write-Host "FERMO: $messaggio" -ForegroundColor Red
    exit 1
}

# ⚠️ La seconda rete. Guarda DENTRO i file, perché il nome non basta: quello del file dei segreti è scelto
# apposta perché non dica niente. Si cercano le CHIAVI, mai i valori — così l'output di questo controllo si
# può incollare in una chat senza pensarci.
#
# ⚠️ E si guardano SOLO i file di testo. La prima stesura leggeva qualunque file sotto il mezzo mega, e
# accusava `Vipi.Host.dll`: dentro un assembly la stringa «ClientSecret» c'è perché è il NOME di una chiave
# di configurazione scritta nel codice. Un allarme che suona a ogni consegna su un file che dev'esserci non
# è un allarme: è la ragione per cui si smette di leggerli. Le credenziali stanno nei file di testo, e in
# un pacchetto di questo prodotto un `.json`, un `.xml` o un `.env` non ci hanno niente da fare — tranne
# l'indice degli asset, che è dichiarato e si riconosce dal nome.
#
# ⚠️ Si cercano CHIAVI CON UN VALORE, non nomi di chiave (U-123, revisione 3). La prima stesura cercava i nomi
# («ConnectionStrings», «ClientSecret»…) e aveva due buchi opposti: bloccava per sempre `appsettings.json`,
# che quei nomi li porta con i valori VUOTI, e lasciava passare il file del ponte RFO — `{"Rfo":{"Chiavi":
# [{"Chiave":"rfo_…"}]}}` — che nessuno di quei nomi lo contiene, ed è proprio la forma che il foglio 1.33.0
# consiglia. Ora: una chiave nota con un valore che non è un segnaposto («…», «<…>», «LA-PASSWORD-VERA»),
# una `Password=` con un valore vero, una chiave RFO (`rfo_` + almeno 40 caratteri), una chiave privata o
# un key-ring. Così si possono guardare anche i `.md` di docs/, che gli esempi li scrivono coi segnaposto.
function Segnaposto($valore) {
    $v = $valore.Trim()
    if ($v -eq '') { return $true }
    if ($v.Contains([string][char]0x2026) -or $v.Contains('...') -or $v.Contains('<') -or $v.Contains('>')) { return $true }
    # Tutto maiuscolo, cifre escluse: «LA-PASSWORD-VERA», «XXX». Un segreto vero mescola.
    if ($v -cmatch '^[A-Z_-]+$') { return $true }
    # «xxx», «***»: lo stesso carattere ripetuto (deploy/render/README.md scrive `Password=xxx`).
    if ($v -match '^(.)\1*$') { return $true }
    return $false
}

function TrovaSegreti($percorso) {
    $testuali = @('.json', '.txt', '.xml', '.config', '.env', '.pem', '.key', '.ini', '.yml', '.yaml', '.md')
    $sempre = @('BEGIN PRIVATE KEY', '<key id=')
    $chiaveValore = '"(ClientSecret|ApiKey|Chiave|Password|Pwd|Secret|Token)"\s*:\s*"(?<v>[^"]*)"'
    # Il backtick fuori dal valore: nei `.md` «`Password=`» è il nome scritto in prosa, non un'assegnazione.
    $password = '(?i)\b(Password|Pwd)\s*=\s*(?<v>[^;"''`\s]+)'
    $rfo = 'rfo_[A-Za-z0-9_-]{40,}'
    $sospetti = @()
    foreach ($f in $percorso) {
        if ($testuali -notcontains [IO.Path]::GetExtension($f).ToLower()) { continue }
        if ((Get-Item $f).Length -gt 2MB) { continue }
        $testo = ''
        try { $testo = Get-Content $f -Raw -Encoding UTF8 -ErrorAction Stop } catch { continue }
        if (-not $testo) { continue }
        $spia = $null
        foreach ($s in $sempre) { if ($testo.Contains($s)) { $spia = $s; break } }
        if (-not $spia -and $testo -cmatch $rfo) { $spia = 'chiave RFO (rfo_...)' }
        if (-not $spia) {
            foreach ($m in [regex]::Matches($testo, $chiaveValore)) {
                if (-not (Segnaposto $m.Groups['v'].Value)) { $spia = ('"' + $m.Groups[1].Value + '" con un valore'); break }
            }
        }
        if (-not $spia) {
            foreach ($m in [regex]::Matches($testo, $password)) {
                if (-not (Segnaposto $m.Groups['v'].Value)) { $spia = "$($m.Groups[1].Value)= con un valore"; break }
            }
        }
        if ($spia) { $sospetti += [pscustomobject]@{ File = $f; Spia = $spia } }
    }
    return $sospetti
}

switch ($Azione) {

    # ── Ruota ────────────────────────────────────────────────────────────────────────────────────────
    # La consegna corrente diventa storia. Si porta via i SUOI docs: quelli di allora, che non si
    # aggiornano mai — è l'unico modo di rispondere fra sei mesi a «cosa gli avevamo detto di fare?».
    'Ruota' {
        # ⚠️ UNA SOLA, o non si ruota. Il 6 settembre 2026 in publish/ ce n'erano DUE — il rifacimento di
        # 1.11.0 e la consegna 1.12.0 — e questo `Select-Object -First 1` prendeva la PRIMA in ordine
        # alfabetico, cioè quella vecchia: avrebbe archiviato la consegna online sotto la data sbagliata,
        # e lasciato in publish/ la cartella da cui poi si confrontano le impronte. È la stessa trappola
        # che il pacchetto 1.12.0 aveva già pagato una volta, dall'altro capo: prendere «la cartella che
        # si ha sotto mano» invece di quella della consegna. Qui lo script non può indovinare — e allora
        # lo dice invece di scegliere.
        $tutte = @(Get-ChildItem $publish -Directory -Filter 'linux-x64-*')
        if ($tutte.Count -eq 0) { Fermati "in ${publish} non c'è nessuna cartella linux-x64-*: niente da ruotare." }
        if ($tutte.Count -gt 1) {
            Fermati ("in ${publish} ci sono $($tutte.Count) cartelle linux-x64-*: " +
                     ($tutte.Name -join ', ') + ". Non so quale sia la consegna: spostane a mano quelle " +
                     'che non lo sono, poi rilancia.')
        }
        $pubDir = $tutte[0]

        $data = $pubDir.Name -replace '^linux-x64-', ''
        $dest = Join-Path $vecchi $data
        if (Test-Path $dest) { Fermati "${dest} esiste già: una consegna non si ruota due volte." }

        $daSpostare = @($pubDir.FullName)
        $daSpostare += (Get-ChildItem $publish -Directory -Filter 'solo-*' | ForEach-Object { $_.FullName })
        $daSpostare += (Get-ChildItem $publish -Directory -Filter 'completo-*' | ForEach-Object { $_.FullName })
        $daSpostare += (Get-ChildItem $publish -File -Filter 'vipi-*.zip*' | ForEach-Object { $_.FullName })
        $docs = Join-Path $publish 'docs'
        if (Test-Path $docs) { $daSpostare += $docs }

        Write-Host "Ruoto la consegna $data in publish_old:" -ForegroundColor Cyan
        foreach ($x in $daSpostare) { Write-Host "  $(Split-Path $x -Leaf)" }
        if ($SoloProva) { Write-Host '(prova: niente spostato)' -ForegroundColor Yellow; break }

        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        foreach ($x in $daSpostare) { Move-Item $x (Join-Path $dest (Split-Path $x -Leaf)) }
        Write-Host "Fatto: $dest" -ForegroundColor Green
    }

    # ── Impronte ─────────────────────────────────────────────────────────────────────────────────────
    # L'elenco dichiarato. Da qui in poi è LUI a dire che cosa entra nello zip, non la cartella.
    'Impronte' {
        if (-not $Pacchetto) { Fermati 'serve -Pacchetto (es. solo-18-file-1.1.0).' }
        if (-not $Elenco)    { Fermati 'serve -Elenco: un file di testo con un percorso relativo per riga.' }
        $cart = Join-Path $publish $Pacchetto
        if (-not (Test-Path $cart)) { Fermati "${cart} non esiste." }

        $righe = Get-Content $Elenco | Where-Object { $_.Trim() -ne '' -and -not $_.StartsWith('#') }
        $out = New-Object System.Collections.ArrayList
        [void]$out.Add("Pacchetto vIPI $Versione - impronte sha256 dei $($righe.Count) file da caricare")
        [void]$out.Add(('=' * 86))
        foreach ($r in $righe) {
            $f = Join-Path $cart ($r -replace '/', '\')
            if (-not (Test-Path $f)) { Fermati "l'elenco nomina un file che non c'e': $r" }
            $h = (Get-FileHash $f -Algorithm SHA256).Hash.ToLower()
            [void]$out.Add(('{0}  {1,9} B  {2}' -f $h, (Get-Item $f).Length, $r))
        }
        if ($SoloProva) { $out | ForEach-Object { Write-Host $_ }; break }
        $out | Out-File (Join-Path $cart 'IMPRONTE.txt') -Encoding utf8
        Write-Host "IMPRONTE.txt scritto: $($righe.Count) file." -ForegroundColor Green
    }

    # ── Zip ──────────────────────────────────────────────────────────────────────────────────────────
    'Zip' {
        if (-not $Pacchetto) { Fermati 'serve -Pacchetto (es. solo-18-file-1.1.0).' }
        if (-not $Versione)  { Fermati 'serve -Versione (es. 1.1.0).' }
        $cart = Join-Path $publish $Pacchetto
        $impronte = Join-Path $cart 'IMPRONTE.txt'
        if (-not (Test-Path $impronte)) { Fermati "manca ${impronte}: lo zip si costruisce da li', non dalla cartella. Vedi -Azione Impronte." }

        # I documenti si ricopiano dalla sorgente vera a ogni consegna: una copia sola invecchia da sola.
        $docs = Join-Path $publish 'docs'
        New-Item -ItemType Directory -Force -Path $docs | Out-Null
        $daFogli = @("LEGGIMI-PACCHETTO-$Versione.md", 'LEGGIMI-AGGIORNARE-VIA-FTP.md', 'LEGGIMI-SEGRETI.md', 'LEGGIMI-TRADUZIONE.md')
        foreach ($f in $daFogli) {
            $src = Join-Path $fogli $f
            if (Test-Path $src) { Copy-Item $src (Join-Path $docs $f) -Force }
            else { Write-Host "  (manca in deploy/atc-ivao: $f)" -ForegroundColor Yellow }
        }

        $dichiarati = @()
        foreach ($r in Get-Content $impronte) {
            if ($r -match '^[0-9a-f]{64}\s+\d+\s+B\s+(.+)$') { $dichiarati += $Matches[1].Trim() }
        }
        if ($dichiarati.Count -eq 0) { Fermati "${impronte} non dichiara nessun file." }

        # PRIMA RETE: quel che sta nella cartella e nessuno ha dichiarato non entra, e si dice a voce alta.
        $suDisco = Get-ChildItem $cart -Recurse -File | ForEach-Object {
            $_.FullName.Substring($cart.Length + 1) -replace '\\', '/'
        }
        $intrusi = @($suDisco | Where-Object { $_ -ne 'IMPRONTE.txt' -and $dichiarati -notcontains $_ })
        if ($intrusi.Count -gt 0) {
            Write-Host ''
            Write-Host 'NELLA CARTELLA CI SONO FILE CHE IL FOGLIO NON DICHIARA. Non entrano nello zip:' -ForegroundColor Yellow
            $intrusi | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
            Write-Host '  Se uno di questi doveva essere consegnato, va messo in elenco (-Azione Impronte), non nello zip di straforo.'
        }

        # SECONDA RETE: dentro i file DICHIARATI. Il nome non basta - quello dei segreti e' scelto apposta
        # perche' non dica niente.
        # ⚠️ @() anche qui: con UN file dichiarato `$percorsi` è una stringa, e «stringa + array» in PowerShell
        # concatena il testo invece di unire gli elenchi — la rete leggeva un percorso che non esiste.
        $percorsi = @($dichiarati | ForEach-Object { Join-Path $cart ($_ -replace '/', '\') })
        # ⚠️ Le @() non sono decorazione. Con UN SOLO file sospetto PowerShell 5.1 restituisce un oggetto
        # scalare, e `.Count` su un PSCustomObject scalare non vale 1: non vale niente. Questa rete l'ha
        # fatto vedere al primo giro — con due file trovati parlava, con uno solo restava muta, cioè taceva
        # esattamente nel caso per cui esiste.
        # ⚠️ Anche il ramo docs/ (U-123): entra nello zip intero, e un file copiato lì a mano non passava da
        # nessuna rete.
        $inDocs = @(Get-ChildItem $docs -Recurse -File | ForEach-Object { $_.FullName })
        $sospetti = @(TrovaSegreti ($percorsi + @($intrusi | ForEach-Object { Join-Path $cart ($_ -replace '/', '\') }) + $inDocs))
        if ($sospetti.Count -gt 0) {
            Write-Host ''
            foreach ($s in $sospetti) {
                Write-Host ("  {0}  -> contiene {1}" -f $s.File.Substring($publish.Length + 1), $s.Spia) -ForegroundColor Red
            }
            Fermati 'un file del pacchetto sembra contenere credenziali. Toglilo dalla cartella e rifai lo zip. Se e'' un falso allarme, guardalo con i tuoi occhi prima di forzare.'
        }

        # Un pacchetto COMPLETO (cartella completo-<versione>, es. il salto a net10 di 1.40.0) non e' «solo i file
        # cambiati»: il nome dello zip lo dice, perche' chi lo apre decide da li' come caricarlo.
        $genere = if ($Pacchetto -like 'completo-*') { 'completo' } else { 'solo-file-cambiati' }
        $zip = Join-Path $publish "vipi-$Versione-$genere.zip"
        Write-Host ''
        Write-Host "Zip: $($dichiarati.Count) file dichiarati + IMPRONTE.txt + $((Get-ChildItem $docs -File).Count) fogli in docs/" -ForegroundColor Cyan
        if ($SoloProva) { Write-Host '(prova: nessuno zip scritto)' -ForegroundColor Yellow; break }

        # Si passa da una cartella di transito: i nomi delle voci si leggono da li', e l'unica forma che conta
        # e' quella - due rami paralleli, `solo-...` e `docs`.
        $transito = Join-Path $env:TEMP ("vipi-zip-" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path (Join-Path $transito $Pacchetto) | Out-Null
        foreach ($r in ($dichiarati + 'IMPRONTE.txt')) {
            $dest = Join-Path (Join-Path $transito $Pacchetto) ($r -replace '/', '\')
            New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
            Copy-Item (Join-Path $cart ($r -replace '/', '\')) $dest
        }
        Copy-Item $docs (Join-Path $transito 'docs') -Recurse

        if (Test-Path $zip) { Remove-Item $zip -Force }
        # 🔴 T-074 (revisione del 13 settembre 2026): NIENTE Compress-Archive. La 1.0.1 di PowerShell 5.1 scrive
        # le voci col BACKSLASH (misurato sullo zip di 1.26.0: 20 voci su 20), che la specifica zip non ammette:
        # un estrattore lato Linux puo' creare file piatti chiamati «solo-15-file-1.26.0\bin\Vipi.dll». ⚠️ E
        # zipfile di Python su Windows lo NASCONDE, perche' in lettura rimette le barre: si guardano i byte.
        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
        $archivio = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($f in (Get-ChildItem $transito -Recurse -File)) {
                $voce = $f.FullName.Substring($transito.Length + 1).Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archivio, $f.FullName, $voce, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        }
        finally { $archivio.Dispose() }
        Remove-Item $transito -Recurse -Force

        $letto = [System.IO.Compression.ZipFile]::OpenRead($zip)
        try { $storte = @($letto.Entries | Where-Object { $_.FullName.Contains('\') }) }
        finally { $letto.Dispose() }
        if ($storte.Count -gt 0) { Fermati "lo zip ha $($storte.Count) voci col backslash: un estrattore Linux le scriverebbe piatte." }

        $h = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
        "$h  $(Split-Path $zip -Leaf)" | Out-File "$zip.sha256" -Encoding ascii
        Write-Host ("Fatto: {0}  {1:N2} MB" -f (Split-Path $zip -Leaf), ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green
        Write-Host "sha256 $h"
    }
}
