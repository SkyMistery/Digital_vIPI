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

    /// <summary>Overlay di visibilità (sezioni/settori/frequenze nascosti) congelato; null per i tipi senza overlay.
    /// Usato da vLOA e APP, entrambi dalla side-entity unificata <c>DocumentProfile</c> (doc 08e/08i).</summary>
    public VloaOverlaySnapshot? Vloa { get; set; }
}
