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
}
