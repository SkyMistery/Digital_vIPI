using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Riga CoP di un flusso (lettura). <see cref="LevelText"/> è il livello già formattato.
/// <para><c>record</c> come <see cref="TransferPointInput"/> e per la stessa ragione: con la faccetta
/// trasferimento i campi sono venticinque, e chi ne varia uno — un test, una proiezione — deve poter scrivere
/// <c>with</c> invece di ricopiarli tutti e sbagliarne uno in silenzio.</para></summary>
public sealed record TransferPointRow : IOutlineRow
{
    public required int Id { get; init; }
    public required string Cop { get; init; }
    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public LevelParity Parity { get; init; } = LevelParity.Any;
    public TransferVerticalState VerticalState { get; init; } = TransferVerticalState.Unspecified;
    public required string LevelText { get; init; }
    public int? NextSectorId { get; init; }
    public string? NextSectorCallsign { get; init; }

    // Condizione operativa: tre dimensioni indipendenti (pista/e · area · personalizzata). Tutte null = sempre valida.
    public string? ConditionLabel { get; init; }        // pista/e in uso
    public int? ConditionRefId { get; init; }           // soft-ref pista singola
    public string? ConditionAreaLabel { get; init; }    // area attiva
    public string? ConditionCustomLabel { get; init; }  // condizione personalizzata

    /// <summary>Etichetta condizione combinata per il display (pill/chip): pista · area · personalizzata. Vuota se nessuna.</summary>
    public string? ConditionDisplay => TransferConditionText.Display(ConditionLabel, ConditionAreaLabel, ConditionCustomLabel);

    // Faccetta trasferimento. HandoffKind = Unspecified ⇒ trasferimento e ingresso coincidono (riga «come prima»).
    // La PAROLA del luogo («al confine dell'AoR») non sta qui: è lingua, e la lingua vive nel template della frase,
    // che esiste in italiano e in inglese (vLOA). Qui viaggiano il tipo e l'etichetta grezza.
    public TransferHandoffKind HandoffKind { get; init; } = TransferHandoffKind.Unspecified;
    public string? HandoffLabel { get; init; }
    public int? HandoffLevelValue { get; init; }
    public LevelUnit HandoffLevelUnit { get; init; } = LevelUnit.Fl;
    public LevelConstraint HandoffLevelConstraint { get; init; } = LevelConstraint.Exact;
    public TransferHandoffKind CommsHandoffKind { get; init; } = TransferHandoffKind.Unspecified;
    public string? CommsHandoffLabel { get; init; }
    public int? SpeedValue { get; init; }
    public SpeedConstraint SpeedConstraint { get; init; } = SpeedConstraint.Unspecified;

    /// <summary>Livello al trasferimento già formattato («FL110»); vuoto se la riga non lo porta.</summary>
    public string HandoffLevelText => LevelFormatting.FormatHandoffLevel(HandoffLevelValue, HandoffLevelUnit, HandoffLevelConstraint);
    /// <summary>Velocità già formattata («≤250 kt»); vuota se la riga non la porta.</summary>
    public string SpeedText => LevelFormatting.FormatSpeed(SpeedValue, SpeedConstraint);
    /// <summary>True se la riga usa la faccetta trasferimento: è il criterio con cui le viste decidono se mostrare
    /// le colonne relative (per presenza di dati, mai per tipo di ente).</summary>
    public bool HasHandoff => HandoffKind != TransferHandoffKind.Unspecified;

    // Varianti: righe dello stesso accordo, organizzate a outline. Gruppo e profondità li assegna il repository.
    public int? VariantGroup { get; init; }
    /// <summary>Rientro nell'outline: 0 = alternativa, 1 = sua eccezione, 2 = eccezione dell'eccezione, …</summary>
    public int VariantDepth { get; init; }
    /// <summary>La riga scavalca le alternative: vale per tutto il gruppo, e si rende in fondo.</summary>
    public bool IsGroupWide { get; init; }

    public required int Order { get; init; }

    // ---- provenienza ----
    // Da quale CLAUSOLA viene la riga, e cosa diceva quella clausola per intero. L'espansione apre un elenco di
    // punti in tante righe perche' e' la lettura giusta per il matcher e per la vista live; la TABELLA del
    // documento vuole richiuderlo, e senza sapere da dove viene ogni riga dovrebbe indovinarlo confrontando
    // quindici campi — cioe' ricostruire per somiglianza un legame che il dato conosce.

    /// <summary>La clausola da cui la riga e' stata proiettata; <c>null</c> per le righe che non vengono da un
    /// accordo (nessuna, oggi: resta nullable perche' il tipo e' una proiezione e non un'entita').</summary>
    public int? ClauseId { get; init; }

    /// <summary>L'elenco completo dei punti della clausola («EKMUR, PISIP»); vuoto se la riga non viene da una.</summary>
    public string? Cops { get; init; }

    /// <summary>Gli aeroporti dell'accordo, quando sono <b>piu' d'uno</b> — vuoto altrimenti. Dice al lettore che
    /// questa riga non riguarda solo lo scalo sotto cui la sta leggendo: e' un accordo solo, non quattro
    /// coincidenze.</summary>
    public string? AgreementAirports { get; init; }
}
