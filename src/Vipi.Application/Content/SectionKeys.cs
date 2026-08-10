namespace Vipi.Application.Content;

/// <summary>
/// Chiavi delle sezioni libere (editoriali, senza corrispondenza nel <see cref="SectionCatalog"/>).
/// Storicamente ogni sezione nuova nasceva con la chiave costante <c>"custom"</c>: due sezioni libere dello stesso
/// documento erano quindi indistinguibili per chi indicizza per chiave (viewer vIPI ACC, «nascondi sezione» APP,
/// anchor di pagina). Da doc 11 §3a la chiave è UNIVOCA per sezione — stesso schema delle chiavi di blocco ACC
/// (<c>grp:{guid8}</c>).
/// </summary>
public static class SectionKeys
{
    /// <summary>Prefisso delle chiavi libere. La chiave storica non suffissata (<c>"custom"</c>) è ambigua.</summary>
    public const string CustomPrefix = "custom";

    /// <summary>Chiave storica ambigua, condivisa da tutte le sezioni libere create prima di doc 11.</summary>
    public const string LegacyCustom = "custom";

    /// <summary>Nuova chiave libera univoca, es. <c>custom:9f3a1c07</c>.</summary>
    public static string NewCustom() => $"{CustomPrefix}:{Guid.NewGuid():N}"[..(CustomPrefix.Length + 9)];

    /// <summary>Vero se la chiave è una sezione libera (storica o univoca), non una sezione di catalogo.</summary>
    public static bool IsCustom(string? key) =>
        key is not null
        && (key.Equals(LegacyCustom, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(CustomPrefix + ":", StringComparison.OrdinalIgnoreCase));

    /// <summary>Vero se la chiave è quella storica ambigua, da riconciliare (doc 11 §3a).</summary>
    public static bool IsLegacyCustom(string? key) =>
        key is not null && key.Equals(LegacyCustom, StringComparison.OrdinalIgnoreCase);

    // ---- coordinamenti vLOA: una chiave per direzione (doc 13 §3c) ----
    // Le due sotto-sezioni ripetevano la chiave del padre, «coordination». Costava caro: la cattura frozen
    // trovava TRE sezioni con quella chiave e derivava tre volte lo stesso payload, la lettura per chiave
    // pescava «la prima che capita», e in editor e viewer la direzione si riconosceva una per TITOLO e l'altra
    // per POSIZIONE — due modi diversi per la stessa cosa, entrambi fragili.

    /// <summary>Sezione padre dei coordinamenti (il corpo lo produce la pagina, non i blocchi).</summary>
    public const string Coordination = "coordination";

    /// <summary>Direzione Home → vicino.</summary>
    public const string CoordinationOut = "coordination:out";

    /// <summary>Direzione vicino → Home.</summary>
    public const string CoordinationIn = "coordination:in";
}
