# I giri di prova del Sector Lab

Questa cartella va sul `master` del fork di prova (`SkyMistery/it-aurora-sector-test`, cartella `giri/`): il Lab la legge
a ogni avvio, e i giri compaiono nel pannello «🧪» degli AOD (carta `docs/feature/2026-11-03-giri-di-prova-dal-fork.md`
di Digital_vIPI). Qui in `tools/kit-aod/giri/` c'è l'originale: un test del Lab (`GiriDelKitTests`) li legge tutti, e un
giro rotto non arriva sul fork.

Un giro per file, in ordine di nome (`01-primi-passi.json`, `02-ground.json`…). «Primi passi» sta anche dentro il Lab: un
`01-primi-passi.json` qui prende il suo posto.

    {
      "id": "ground",                        lettere minuscole, cifre e «-»; non cambia mai
      "titolo": "Il ground dello scalo",
      "versione": 1,                         più alta = cambiato: agli AOD torna «nuovo»
      "perChi": "…",                         facoltativo
      "compiti": [
        { "id": "1", "titolo": "…", "pagina": "Sector", "fai": "…", "dovresti": "…" },
        { "id": "10", "titolo": "Che cosa ti è mancato?", "fai": "…", "soloNota": true }
      ]
    }

`pagina` (facoltativa: dà il tasto «Vai a …»): `Sector`, `Testo`, `Consegna`, `AipIndice`, `Aip`, `Confronti`,
`Impostazioni`. Gli esiti si tengono per `id` del compito: cambiando un compito tienigli l'`id` se è lo stesso compito,
dagliene uno nuovo se è un altro.
