-- Valori scomodi per l'andata e ritorno della copia (§A47). Li scrive andata-e-ritorno.sh nel database di
-- partenza, accanto allo schema vero: ogni riga è un modo in cui un letterale SQL può uscire sbagliato senza
-- che nessuno se ne accorga fino al giorno del ripristino.

-- Id 0 con AUTO_INCREMENT: senza NO_AUTO_VALUE_ON_ZERO nella testata del file, al ripristino diventerebbe 1.
SET SESSION SQL_MODE = 'NO_AUTO_VALUE_ON_ZERO';

CREATE TABLE `ProvaCopia` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Testo` longtext NULL,
  `Bin` longblob NULL,
  `Quando` datetime(6) NULL,
  `Ora` time(6) NULL,
  `Giorno` date NULL,
  `Importo` decimal(18,6) NULL,
  `Doppio` double NULL,
  `Singolo` float NULL,
  `Flag` tinyint(1) NULL,
  `Guid` char(36) NULL,
  `Grande` bigint unsigned NULL,
  `Json` json NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_as_cs;

INSERT INTO `ProvaCopia` VALUES
  (0, 'apice '' barra \\ doppio " a capo\nritorno\r zero\0 ctrl-z\Z emoji 🛫 accento è', 0x00FF00270A5C22,
   '2026-09-16 21:05:07.123456', '838:59:59.000000', '2024-02-29', -1234.567891, 0.1, 0.1, 1,
   'ABCDEF01-2345-6789-abcd-EF0123456789', 18446744073709551615, '{"a":"b\\n"}'),
  (1, '', '', '1000-01-01 00:00:00', '-01:00:00.500000', '9999-12-31', 0, 1e300, -3.4e38, 0,
   '00000000-0000-0000-0000-000000000000', 0, '[]'),
  (2, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
  -- double a 17 cifre significative e ai bordi: le coordinate dello schema sono double, e un formato che ne
  -- perde una cifra le sposta senza che nessuno lo veda.
  (3, 'doppi', NULL, NULL, NULL, NULL, NULL, 0.30000000000000004, NULL, NULL, NULL, NULL, NULL),
  (4, 'doppi', NULL, NULL, NULL, NULL, NULL, 41.800277777777779, NULL, NULL, NULL, NULL, NULL),
  (5, 'doppi', NULL, NULL, NULL, NULL, NULL, 5e-324, NULL, NULL, NULL, NULL, NULL),
  (6, 'doppi', NULL, NULL, NULL, NULL, NULL, -1.7976931348623157e308, NULL, NULL, NULL, NULL, NULL),
  -- Un blob da 3 MB (6 MB di esadecimale nell'INSERT): un'immagine al tetto degli upload.
  (7, 'blob grande', REPEAT(0xAB00, 1536 * 1024), NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

-- Due megabyte di righe piccole: la tabella deve uscire in più INSERT, e il blob qui sopra in uno suo.
INSERT INTO `ProvaCopia` (`Id`, `Testo`, `Doppio`)
  SELECT 1000 + seq, CONCAT('riga ', seq, ' ', REPEAT('è', 1000)), seq / 7 FROM seq_1_to_1000;

-- La tabella che la copia deve lasciare FUORI. Nello schema delle migrazioni non c'è (la crea il sito
-- all'avvio): qui la si crea, così l'esclusione si prova invece di passare perché la tabella manca.
CREATE TABLE IF NOT EXISTS `DataProtectionKeys` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `FriendlyName` longtext NULL,
  `Xml` longtext NULL,
  PRIMARY KEY (`Id`)
);
INSERT INTO `DataProtectionKeys` (`FriendlyName`, `Xml`) VALUES ('chiave', '<key/>');
