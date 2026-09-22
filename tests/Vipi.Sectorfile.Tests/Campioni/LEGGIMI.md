# Campioni del sector

File veri del sector italiano (`ivao-italy/it-aurora-sector`, pubblico), copiati **senza modifiche** dal
`master` al commit `7e761aa` (22 settembre 2026), stessi percorsi sotto `SectorFiles/Include/IT`.

Sono i file che i test della libreria A leggevano per nome (carta F2, §5): 30 file, ~1,1 MB. Fra questi
`ACC/FRA-gates.artcc`, che da solo porta 5 041 delle righe opache del §1: servirà alla slice 4.
Fuori, per peso: `GEO/itgeo.geo` e `GEO/lirf.geo` (1,2 MB), che fanno il round-trip con l'albero intero in
`tools/Vipi.SectorfileProva`.

Aggiunti con la slice 4 (punti per nome), dallo stesso commit: `DYNAMIC_SEC/libb_es_ctr.tfl` (72 vertici per
nome) e `lied.sid` (le SID col tracciato sotto l'intestazione). Con la slice 5 (forme opache):
`NAVAIDS/APT.fix` e `NAVAIDS/VFR_NASCOSTI.fix` (fix a 4 e a 3 campi), `GND_LAYOUT/br_ad_gnd.pol` (il nome del
poligono in commento), `GEO/liap.geo` (colore vuoto).

- `.gitattributes` qui accanto: nessuna conversione dei fine riga. Aurora li legge in CRLF.
- `NAVAIDS/ENR.fix` è **vuoto** anche nel sector: è un caso vero, non un errore della copia.
- Aggiornarli: si ricopiano interi da un commit del sector e si scrive qui il nuovo commit.
