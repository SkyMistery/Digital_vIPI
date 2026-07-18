namespace Vipi.Application.Content;

/// <summary>
/// Snapshot editoriale serializzato in <c>DocRelease.PayloadJson</c>. Contiene SOLO le scelte editoriali congelate:
/// la struttura documentale (<see cref="Doc"/>) e, per i tipi con overlay di visibilità, l'overlay (<see cref="Vloa"/>).
/// I dati derivati (poligoni/frequenze/gerarchia/trasferimenti) NON sono qui: si renderizzano coi cataloghi correnti.
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

    /// <summary>Overlay di visibilità (sezioni/settori/frequenze nascosti) congelato; null per i tipi senza overlay.
    /// Usato da vLOA e APP, entrambi dalla side-entity unificata <c>DocumentProfile</c> (doc 08e/08i).</summary>
    /// <remarks>DEAD-CODE in via di rimozione (doc 10 §3c, S5): scritto ma mai riletto; sostituito dalla copia
    /// congelata totale in <see cref="FrozenSections"/>. Vedi <c>SnapshotFreezeCharacterizationTests</c>.</remarks>
    public VloaOverlaySnapshot? Vloa { get; set; }
}
