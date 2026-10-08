namespace Vipi.Application.Content;

/// <summary>
/// Snapshot editoriale serializzato in <c>DocRelease.PayloadJson</c> (doc 10 §3c): fotografia TOTALE del documento —
/// la struttura documentale (<see cref="Doc"/>) più l'output congelato delle sezioni derivabili in modalità Frozen
/// (<see cref="FrozenSections"/>). La visibilità (sezioni/settori/frequenze nascosti) è già dentro questa fotografia;
/// le sole sezioni in modalità Live si renderizzano coi cataloghi correnti al view.
/// </summary>
public sealed class DocReleasePayload
{
    /// <summary>Struttura documentale congelata (sezioni + blocchi statici), riusa il modello <see cref="RawDocument"/>.</summary>
    public RawDocument Doc { get; set; } = default!;

    /// <summary>Output congelato delle sezioni DERIVABILI in modalità Frozen (doc 10 §3c): chiave = Id della
    /// <c>DocumentSection</c> (== <c>RawSection.Id</c> in <see cref="Doc"/>), valore = JSON del view-model già
    /// renderizzato (frequenze/AoR/coord/…), serializzato dal <c>IFrozenSectionProvider</c> della famiglia. Le sezioni
    /// in modalità Live NON compaiono qui: il viewer le deriva sul momento. Vuoto = nessuna sezione derivabile congelata.</summary>
    public Dictionary<int, string> FrozenSections { get; set; } = new();

    /// <summary>I documenti collegati (§A109): la STRUTTURA congelata alla pubblicazione, con tutti i candidati;
    /// quali si vedono lo decide il disegno (<see cref="DocumentiCollegati.Resolve"/>).
    /// <para>⚠️ <c>null</c> = release di prima del 21 settembre 2026: i collegamenti si calcolano dalla struttura
    /// di adesso, così non bisogna ripubblicare tutto per vederli.</para></summary>
    public DocLinkSnapshot? Collegamenti { get; set; }

    /// <summary>
    /// Vero se questa release è nata quando le configurazioni stavano già in <b>Struttura</b> (dall'8 ottobre
    /// 2026, carta <c>2026-10-08-configurazioni-possibili</c>): la sezione <c>configurations</c> è una derivata
    /// come le altre — la sua voce congelata sta in <see cref="FrozenSections"/>, e se non c'è (sezione Live) si
    /// legge la struttura di adesso.
    /// <para>⚠️ <c>false</c> = release di prima: le configurazioni sono il <c>BodyJson</c> di quella sezione
    /// dentro <see cref="Doc"/>, e si leggono da lì. È il segno che permette di non toccare niente di già uscito:
    /// senza, un <c>BodyJson</c> rimasto nella versione di lavoro passerebbe per buono in ogni release futura.</para>
    /// </summary>
    public bool ConfigurazioniDallaStruttura { get; set; }
}
