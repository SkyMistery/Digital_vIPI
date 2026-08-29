namespace Vipi.Domain.Entities;

/// <summary>
/// Policy globale (riga singola, Id=1) che decide quali categorie di dati arrivano dalla sorgente esterna.
/// Semantica opt-out: <c>true</c> = importato e bloccato (sorgente autorevole, sola lettura per l'utente);
/// <c>false</c> = escluso (gestito a mano, l'import non lo tocca). Default tutto <c>true</c>.
/// Le categorie editoriali (regole pista, SID, livelli TL, link, ecc.) non sono qui: sempre dell'utente.
/// <para>
/// Sfumatura di <see cref="ImportSpecialAreas"/>: le aree regolamentate non sono editabili da nessuna UI, quindi
/// <c>false</c> non significa «le gestisco a mano» ma «congela quelle già in DB» — l'import non le aggiorna e
/// soprattutto non le pota. Serve a fermare la sorgente quando restituisce dati sbagliati.
/// </para>
/// </summary>
public class ImportPolicy
{
    public int Id { get; set; }                                   // riga singola: Id = 1
    public bool ImportTransitionAltitude { get; set; } = true;    // Airport.TransitionAltitudeFt
    // AirportRunway: Ident/LengthM/Bearing e, dal 30 agosto 2026, le coordinate della SOGLIA e la sua
    // elevazione — arrivano nella stessa risposta e sono di sorgente come gli altri tre.
    public bool ImportRunways { get; set; } = true;
    public bool ImportSectors { get; set; } = true;               // Sector.Callsign/Type/DefaultFrequency
    public bool ImportSids { get; set; } = true;                  // AirportSid dal sectorfile Aurora (GitHub)
    public bool ImportSpecialAreas { get; set; } = true;          // SpecialArea (aree regolamentate per ACC)

    /// <summary>
    /// Le radioassistenze dal sectorfile: frequenza, canale e coordinate di VOR e NDB (carta vSOP militari
    /// §12b). ⚠️ Default <c>true</c> nel MODELLO e nella migrazione, non solo qui — un <c>bool NOT NULL</c>
    /// nuovo nasce <c>false</c> su ogni riga già esistente, e per un flag opt-out significa nascere
    /// <b>spento</b>. È già successo con <c>ImportSids</c>.
    /// </summary>
    public bool ImportNavaids { get; set; } = true;

    /// <summary>
    /// Raccolta delle statistiche ATC: sessioni dal vivo e storico. ⚠️ Default <c>true</c> nel MODELLO e
    /// nella migrazione, non solo qui: un <c>bool NOT NULL</c> nuovo nasce <c>false</c> su ogni riga già
    /// esistente, e per un flag opt-out significa nascere <b>spento</b> — cioè spegnere la raccolta a chi
    /// non ha chiesto niente. È già successo una volta con <c>ImportSids</c>.
    /// </summary>
    public bool ImportAtcSessions { get; set; } = true;
    public DateTime UpdatedUtc { get; set; }
    public int UpdatedByUserId { get; set; }
}

/// <summary>
/// Ultima esecuzione RIUSCITA di un import periodico, per categoria (Acc/AirportSector/SpecialArea/Sid).
/// Persistente: gli hosted service saltano il fetch all'avvio se ancora "fresco" (entro il periodo), evitando
/// di richiamare la sorgente a ogni riavvio dell'app.
/// </summary>
public class ImportState
{
    public string Category { get; set; } = default!;   // chiave naturale
    public DateTime LastSuccessUtc { get; set; }

    /// <summary>
    /// Il giro riuscito <b>prima</b> dell'ultimo, o null se ce n'è stato uno solo. È il metro della regola
    /// «si elimina solo ciò che la sorgente non manda da due giri»: una riga il cui timbro d'import è più
    /// vecchio di questo istante non è stata confermata né dall'ultimo giro né dal penultimo.
    ///
    /// <para>⚠️ Non è «l'ultimo meno il periodo»: i giri slittano, e un intervallo calcolato darebbe per
    /// mancata una conferma che c'è stata. L'unico istante che vale è quello di un giro <b>davvero</b>
    /// avvenuto.</para>
    /// </summary>
    public DateTime? PrevSuccessUtc { get; set; }

    public DateTime? LastAttemptUtc { get; set; }       // ultimo tentativo (riuscito o no); null se mai tentato
    public string? LastError { get; set; }              // messaggio dell'ultimo fallimento; null se l'ultimo tentativo è riuscito
}
