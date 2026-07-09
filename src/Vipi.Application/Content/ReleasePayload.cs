namespace Vipi.Application.Content;

/// <summary>
/// Snapshot editoriale serializzato in <c>DocRelease.PayloadJson</c>. Contiene SOLO le scelte editoriali congelate:
/// la struttura documentale (<see cref="Doc"/>) e, per le vLOA, l'overlay di visibilità (<see cref="Vloa"/>).
/// I dati derivati (poligoni/frequenze/gerarchia/trasferimenti) NON sono qui: si renderizzano coi cataloghi correnti.
/// </summary>
public sealed class DocReleasePayload
{
    /// <summary>Struttura documentale congelata (sezioni + blocchi statici), riusa il modello <see cref="RawDocument"/>.</summary>
    public RawDocument Doc { get; set; } = default!;

    /// <summary>Overlay vLOA (sezioni/settori/frequenze nascosti) congelato; null per i tipi non-vLOA.</summary>
    public VloaOverlaySnapshot? Vloa { get; set; }
}

/// <summary>Overlay di visibilità della vLOA congelato nella release (fotografia di <c>VloaProfile</c>).</summary>
public sealed class VloaOverlaySnapshot
{
    public List<string> HiddenAorSectors { get; set; } = new();
    public List<string> HiddenFrequencies { get; set; } = new();
    public List<string> HiddenSections { get; set; } = new();
}

/// <summary>Snapshot editoriale di un APP standalone (profile-based): i 6 blob + template + i link-frequenza per
/// callsign (il VALORE della frequenza resta derivato live in fase di vista).</summary>
public sealed class AppReleaseSnapshot
{
    public string SeparationsJson { get; set; } = "[]";
    public string? VfrJson { get; set; }
    public string SectionOrderJson { get; set; } = "[]";
    public string HiddenSectionsJson { get; set; } = "[]";
    public string FreqOrderJson { get; set; } = "{}";
    public string CustomSectionsJson { get; set; } = "[]";
    public string? CoordinationSentenceTemplate { get; set; }
    public List<AppReleaseFreqLink> FreqLinks { get; set; } = new();
}

/// <summary>Link-frequenza congelato per callsign (scelta editoriale); il valore si ri-risolve dal catalogo corrente.</summary>
public sealed record AppReleaseFreqLink(string Callsign, string? Label, int Order);
