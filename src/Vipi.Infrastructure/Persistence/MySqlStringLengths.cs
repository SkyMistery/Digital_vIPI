using Microsoft.EntityFrameworkCore;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Lunghezze delle colonne stringa <b>indicizzate</b>, applicate solo quando il provider è MySQL.
///
/// <para><b>Perché esiste.</b> Una stringa senza <c>HasMaxLength</c> diventa <c>text</c> su SQLite e
/// PostgreSQL — indicizzabile senza limiti — ma <c>longtext</c> su MySQL, che InnoDB <b>non indicizza</b>
/// senza prefix length. Senza queste lunghezze il <c>CREATE TABLE</c> di MySQL fallisce e basta. Il tetto
/// è 3072 byte per indice e <c>utf8mb4</c> costa 4 byte per carattere, quindi <b>768 caratteri</b> per
/// colonna indicizzata.</para>
///
/// <para><b>Perché solo su MySQL.</b> Applicarle a tutti i provider sarebbe un <b>cambio di tipo colonna</b>
/// su Neon (<c>text</c> → <c>varchar(n)</c>): il <c>PostgresSchemaReconciler</c> non lo sa fare — copre solo
/// le aggiunte — e <c>ISchemaDriftProbe</c> lo segnalerebbe come tipo divergente su ogni colonna toccata,
/// mandando la diagnostica in Degraded. È il caso «cambio di tipo» che ADR-0007 §D1-bis dichiara non
/// automatizzabile. Su MySQL invece la lunghezza nasce col database, quindi non c'è nulla da migrare.</para>
///
/// <para><b>Attenzione alle FK su chiave alternata.</b> MySQL rifiuta una foreign key fra colonne di
/// lunghezza diversa (<c>errno 150</c>), ed è il primo errore che salta fuori al <c>CREATE TABLE</c>.
/// Le coppie legate sono annotate qui sotto: cambiando una, va cambiata l'altra. Il vincolo è presidiato
/// da un test, non lasciato alla memoria.</para>
///
/// <para><b>Le misure.</b> I valori vengono dal massimo reale osservato sul <c>vipi.db</c> di sviluppo
/// (5 agosto 2026), arrotondato in alto con margine: una lunghezza troppo stretta tronca in silenzio su
/// MySQL non-strict e lancia in strict. Dove la tabella era vuota il valore è dimensionato sul formato,
/// non sui dati, ed è annotato come tale.</para>
///
/// <para>La copertura è presidiata da <c>IndexedStringLengthTests</c>: aggiungere una colonna stringa
/// indicizzata senza dimensionarla fa fallire la suite su net10, cioè dove il ramo MySQL non viene
/// nemmeno costruito.</para>
/// </summary>
public static class MySqlStringLengths
{
    /// <summary>
    /// Tetto per colonna indicizzata: 3072 byte / 4 byte per carattere <c>utf8mb4</c>.
    /// Non è un limite di stile, è il punto in cui InnoDB rifiuta l'indice.
    /// </summary>
    public const int MaxIndexableChars = 768;

    /// <summary>
    /// Lunghezza data a <b>ogni</b> enum salvato come stringa che non abbia già una voce esplicita in
    /// <see cref="Map"/>. Non è una misura sui dati: il valore più lungo dell'intero dominio è
    /// <c>Unspecified</c> (11 caratteri), e 32 lascia spazio a nomi nuovi senza avvicinarsi a niente.
    ///
    /// <para><b>Perché una regola e non altre trenta righe nella mappa.</b> Fino al 14 agosto 2026 si
    /// dimensionavano solo le colonne <i>indicizzate</i> (più quelle con un <c>DEFAULT</c>, eccezione già
    /// aggiunta una volta per necessità). Tutto il resto nasceva <c>longtext</c>: 128 colonne, di cui una
    /// trentina erano enum a valori corti e chiusi. Il problema non erano le prestazioni — a 4 800 righe non
    /// esistono — ma il fatto che <b>nessuna di quelle colonne è indicizzabile senza una migrazione di
    /// tipo</b>: il giorno in cui servisse un indice su <c>DocumentVersion.Status</c>, non si aggiunge un
    /// indice, si riscrive una tabella su un database di produzione altrui, in DDL non transazionale.
    /// Dimensionarle adesso costa zero perché il cutover non è ancora avvenuto e cambia solo la DDL di
    /// <c>InitialCreate</c>. Vedi <c>docs/history/audit-2026-08-14-database-mariadb.md</c> §D1.</para>
    ///
    /// <para>Una mappa a mano avrebbe richiesto di ricordarsene a ogni enum nuovo; la regola non si dimentica.
    /// Il suo unico presupposto — nessun nome di valore supera i 32 caratteri — è presidiato da un test.</para>
    /// </summary>
    public const int EnumChars = 32;

    /// <summary>Lunghezza per <c>(nome entità, nome proprietà)</c>.</summary>
    public static readonly IReadOnlyDictionary<(string Entity, string Property), int> Map =
        new Dictionary<(string, string), int>
        {
            // --- Codici ACC e ICAO ---------------------------------------------------------------------
            // Sono le due colonne che fanno da chiave alternata a più FK: ogni riferimento deve avere
            // ESATTAMENTE la stessa lunghezza della principale, o errno 150. Misurato: 4 caratteri
            // ovunque (sono codici ICAO), il margine è per i codici di enti non standard.
            [("Acc", "Code")] = 16,
            [("AccSector", "CenterId")] = 16,              // FK → Acc.Code
            [("AirportSector", "AccCode")] = 16,           // FK → Acc.Code
            // L'appartenenza dell'area al centro non è più una colonna su SpecialArea: un'area sta in più
            // elenchi ACC, quindi vive nell'entità di legame SpecialAreaCenter (SPEC §9.23). Le due colonne
            // sono INSIEME la chiave primaria e SEPARATAMENTE due FK: vanno dimensionate come le principali.
            [("SpecialAreaCenter", "CenterId")] = 16,      // FK → Acc.Code
            [("SpecialAreaCenter", "IvaoId")] = 64,        // FK → SpecialArea.IvaoId
            [("Airport", "Icao")] = 8,
            [("AirportSector", "AirportIcao")] = 8,        // FK → Airport.Icao
            // Non sono FK dichiarate ma contengono codici ACC: stessa taglia per coerenza.
            [("NeighbourCandidate", "HomeAccCode")] = 16,
            [("NeighbourCandidate", "ForeignAccCode")] = 16,

            // --- Callsign e posizioni ------------------------------------------------------------------
            // Misurato 12 (`LIMM_WS2_CTR`, `DAAA_MIL_CTR`). 32 lascia spazio a infissi più lunghi senza
            // avvicinarsi al tetto: anche l'indice composito più largo resta ben sotto i 3072 byte.
            [("AccSector", "ComposePosition")] = 32,
            [("AirportSector", "ComposePosition")] = 32,
            [("Sector", "Callsign")] = 32,
            [("AccSector", "ParentCallsign")] = 32,
            [("AirportSector", "ParentCallsign")] = 32,
            [("Airport", "ParentCallsign")] = 32,

            // --- Enum salvati come stringa -------------------------------------------------------------
            // OnModelCreating li mappa con SetProviderClrType(typeof(string)): sono colonne stringa
            // indicizzate senza sembrarlo guardando il tipo CLR. È la trappola che il piano segnala, ed è
            // il motivo per cui il test guarda il tipo provider e non quello CLR.
            [("Document", "Type")] = 32,                   // misurato 4 (`Vipi`)
            [("Document", "Status")] = 32,                 // misurato 9 (`Published`)
            // Non è indicizzata: sta qui perché ha un DEFAULT. In MySQL una colonna BLOB/TEXT non può
            // averlo — «BLOB, TEXT, GEOMETRY or JSON column 'RenderMode' can't have a default value» —
            // quindi senza lunghezza non diventa varchar e la migrazione si ferma. Scoperto applicando
            // le migrazioni a un MySQL vero, non generando la DDL.
            [("DocumentSection", "RenderMode")] = 32,
            [("DocRelease", "TargetType")] = 32,           // misurato 7 (`AccVipi`)
            [("EditorTask", "Status")] = 32,               // tabella vuota: dimensionato sui nomi dell'enum
            [("ImportState", "Category")] = 32,            // chiave primaria; misurato 13 (`AirportSector`)

            // --- Chiavi composte e identificatori ------------------------------------------------------
            // TargetKey è `{acc}|{root}`, cioè codice ACC + separatore + callsign: 16+1+32 = 49 nel caso
            // peggiore costruibile. Misurato 17 (`LIMM|LIMM_WS2_CTR`); 100 copre con abbondanza.
            [("DocRelease", "TargetKey")] = 100,
            // Chiavi nominate a mano (`admin:structure`, `editor:newdoc`): misurato 15.
            [("EditResourceLock", "ResourceKey")] = 100,
            // Chiave di blocco condiviso: tabella vuota, formato libero deciso dall'editor.
            [("SharedBlock", "Key")] = 100,

            // --- Riferimenti di navigazione ------------------------------------------------------------
            // Tabelle oggi vuote: dimensionate sul formato (identificatori ICAO di fix/navaid, cicli AIRAC
            // `AAAA`), non sui dati.
            [("NavReference", "Type")] = 16,
            [("NavReference", "Ident")] = 16,
            [("NavReference", "AiracCycle")] = 16,
            [("CoordinationPoint", "Ident")] = 16,
            [("SidFixAlias", "Prefix")] = 16,

            // --- Identità dalla sorgente ---------------------------------------------------------------
            // Id numerico IVAO tenuto come stringa: misurato 5.
            [("SpecialArea", "IvaoId")] = 64,

            // NB: MediaAsset.Sha256 non è qui perché ha già HasMaxLength(64) nel modello, per tutti i
            // provider — è content-addressed, la lunghezza è fissa per definizione dell'algoritmo.
        };

    /// <summary>
    /// Applica le lunghezze al modello. Da chiamare <b>solo</b> quando il provider è MySQL: su Postgres
    /// sarebbe un cambio di tipo colonna che il reconciler non sa applicare (vedi il commento del tipo).
    ///
    /// <para>Due fonti, in ordine: la voce esplicita in <see cref="Map"/> vince sempre, e a seguire la regola
    /// sugli enum (<see cref="EnumChars"/>). L'ordine conta: alcune voci della mappa <i>sono</i> enum —
    /// <c>Document.Type</c>, <c>TransferPoint.HandoffKind</c> — e stanno lì con una misura propria e un
    /// commento che la giustifica.</para>
    ///
    /// <para>⚠️ Dev'essere chiamata <b>dopo</b> la conversione globale enum→stringa di
    /// <c>OnModelCreating</c>: la regola riconosce un enum dal suo tipo provider, che prima di quella
    /// conversione non è ancora <c>string</c>.</para>
    /// </summary>
    public static void Apply(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
            {
                if (Map.TryGetValue((entity.ClrType.Name, prop.Name), out var lunghezza))
                {
                    prop.SetMaxLength(lunghezza);
                    continue;
                }

                if (EUnEnumSalvatoComeStringa(prop)) prop.SetMaxLength(EnumChars);
            }
    }

    /// <summary>
    /// Un enum che finisce in una colonna testuale. Il tipo CLR non basta a riconoscerlo — la conversione la
    /// fa <c>SetProviderClrType</c> in <c>OnModelCreating</c> — quindi si guardano entrambi: enum di là,
    /// stringa di qua.
    /// </summary>
    private static bool EUnEnumSalvatoComeStringa(Microsoft.EntityFrameworkCore.Metadata.IMutableProperty prop)
    {
        var tipoClr = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
        return tipoClr.IsEnum && prop.GetProviderClrType() == typeof(string);
    }
}
