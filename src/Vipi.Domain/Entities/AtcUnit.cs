namespace Vipi.Domain.Entities;

/// <summary>
/// Un <b>ente ATC</b> che ha un documento suo: «Palermo Approach», «Pratica di Mare». È l'aggancio della
/// vIPI APP, al posto del nominativo IVAO.
///
/// <para>🔴 <b>Perché non basta il nominativo</b> (committente, 29 settembre 2026). Fino ad allora la vIPI APP
/// era del settore APP (<c>Sector.DocumentId</c>), e questo obbligava la divisione a nominare le posizioni IVAO
/// in funzione del sito: a Pratica di Mare la posizione giusta è <c>LIRE_TWR</c> — una torre che fa anche
/// l'avvicinamento — ma c'è <c>LIRE_APP</c> «per far funzionare tutto». E un APP che cambiava stato perdeva il
/// documento: spuntato «remotizzato», il descrittore delle pubblicazioni non lo riconosceva più e la vIPI APP
/// diventava irraggiungibile. L'ente è stabile; le sue posizioni cambiano quando cambia IVAO.</para>
///
/// <para>Gli scali coperti <b>non</b> si scrivono qui: restano quelli che stanno sotto le posizioni dell'ente
/// nella struttura (Catania Approach copre Catania, Sigonella e Comiso perché ne è il padre). Scriverli due volte
/// darebbe due verità.</para>
/// </summary>
public class AtcUnit
{
    public int Id { get; set; }

    /// <summary>
    /// Il <b>codice</b> dell'ente: chiave di pubblicazione e indirizzo pubblico (<c>?app=</c>). Non cambia mai.
    /// <para>⚠️ Per gli enti nati dal legame vecchio è il nominativo che portava il documento (<c>LICJ_APP</c>,
    /// <c>LIRE_APP</c>): così le pubblicazioni già fatte e i link già in giro restano validi senza riscriverli.
    /// Che poi la posizione diventi <c>LIRE_TWR</c> non lo tocca — è il punto.</para>
    /// </summary>
    public string Code { get; set; } = default!;

    /// <summary>Il nome dell'ente, quello che va nel titolo («Palermo Approach»).</summary>
    public string Name { get; set; } = default!;

    public int AccId { get; set; }
    public Acc? Acc { get; set; }

    /// <summary>Dove vive il contenuto: nel documento dell'ente, o dentro la vIPI dell'ACC (APP remotizzato).</summary>
    public AtcUnitMode Mode { get; set; } = AtcUnitMode.OwnDocument;

    /// <summary>La vIPI APP dell'ente. Una sola, e un documento descrive un solo ente (indice unico).</summary>
    public int? DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>Le posizioni IVAO dell'ente, la prima (<see cref="AtcUnitPosition.Order"/> più basso) è la
    /// principale: da lei parte la derivazione (frequenze, AoR, coordinamenti).</summary>
    public ICollection<AtcUnitPosition> Positions { get; set; } = new List<AtcUnitPosition>();
}

/// <summary>
/// Una posizione IVAO di un ente, per <b>nominativo</b> e non per chiave esterna al settore: il nominativo può
/// non esistere ancora, o non esistere più, senza che l'ente o il suo documento se ne accorgano. È la stessa
/// scelta di <c>SectorFallback</c> e degli agganci AIP.
/// </summary>
public class AtcUnitPosition
{
    public int Id { get; set; }
    public int AtcUnitId { get; set; }
    public AtcUnit? AtcUnit { get; set; }

    /// <summary>Il nominativo (es. <c>LIRE_TWR</c>). Unico: una posizione appartiene a un ente solo.</summary>
    public string Callsign { get; set; } = default!;

    /// <summary>Ordine fra le posizioni dell'ente; la più bassa è la principale.</summary>
    public int Order { get; set; }
}
