-- Ripristino dei poligoni CTR/APP/MIL/FSS persi fra il 25 e il 26 agosto 2026.
--
-- PERCHE'. Dal 26 agosto l'API IVAO risponde `regionMapPolygon: []` su OGNI risorsa (verificato: settori,
-- postazioni e aree regolamentate, italiane ed estere). Gli upsert dei cataloghi scrivevano quel vuoto sopra
-- la shape che avevamo -- difetto corretto in `PolygonGeometry.IsEmptyShape`, ma il giro che ha fatto danno
-- era gia' passato. Le TWR si sono salvate perche' il ripiego GitHub le rimette ogni notte; CTR, APP, MIL e
-- FSS no, perche' per loro un ripiego non esiste.
--
-- COSA FA. Ricopia il poligono dal backup SOLO dove oggi non ce n'e' uno. Non tocca nulla che abbia gia' una
-- shape, quindi si puo' rilanciare senza pensarci.
--
-- COME. Da `sqlite3 vipi.db`, dopo aver fermato l'host:
--     .read tools/ripristino-shape/ripristina-poligoni.sql
-- Il percorso del backup va adattato alla riga ATTACH qui sotto.
--
-- ⚠️ Vale per SQLite. Su MariaDB il travaso e' un'altra cosa: si esporta dal backup e si applica per UPDATE.

ATTACH DATABASE 'vipi.db.bak-pre-pulizia-orfani-libg-20260825' AS bk;

BEGIN;

UPDATE AccSectors
   SET RegionMapPolygon = (SELECT b.RegionMapPolygon
                             FROM bk.AccSectors b
                            WHERE b.ComposePosition = AccSectors.ComposePosition)
 WHERE (RegionMapPolygon IS NULL OR length(RegionMapPolygon) <= 4)
   AND EXISTS (SELECT 1 FROM bk.AccSectors b
                WHERE b.ComposePosition = AccSectors.ComposePosition
                  AND length(b.RegionMapPolygon) > 4);

-- ⚠️ Le TWR restano fuori: le loro shape sono NOSTRE (GitHub + cerchio da 5 NM), non della sorgente, e il
-- ripiego notturno le tiene aggiornate. Riportarne indietro una vecchia sarebbe un passo all'indietro.
UPDATE AirportSectors
   SET RegionMapPolygon = (SELECT b.RegionMapPolygon
                             FROM bk.AirportSectors b
                            WHERE b.ComposePosition = AirportSectors.ComposePosition)
 WHERE Position <> 'TWR'
   AND (RegionMapPolygon IS NULL OR length(RegionMapPolygon) <= 4)
   AND EXISTS (SELECT 1 FROM bk.AirportSectors b
                WHERE b.ComposePosition = AirportSectors.ComposePosition
                  AND length(b.RegionMapPolygon) > 4);

COMMIT;

SELECT 'AccSectors con poligono'     AS cosa, count(*) AS quanti FROM AccSectors     WHERE length(RegionMapPolygon) > 4
UNION ALL
SELECT 'AirportSectors con poligono', count(*)                   FROM AirportSectors WHERE length(RegionMapPolygon) > 4;

DETACH DATABASE bk;
