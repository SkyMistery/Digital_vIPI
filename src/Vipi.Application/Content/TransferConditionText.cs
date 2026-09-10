namespace Vipi.Application.Content;

/// <summary>
/// Le tre dimensioni della condizione di UNA riga: pista/e, area attiva, personalizzata. Tutte opzionali.
/// <para>Esiste come tipo perché nell'outline delle varianti una riga porta anche le condizioni dei propri
/// ANTENATI: un'eccezione di «pista 07» vale «con pista 07 in uso <b>e</b> R403B attiva», e la frase deve
/// dirlo tutto — viaggia da sola nella prosa del documento, senza il rientro che in tabella dà il contesto.
/// Una lista di clausole è la catena dalla capofila alla riga.</para>
/// </summary>
/// <param name="Area">Le aree, separate da <see cref="TransferConditionText.SeparatoreAree"/>.</param>
/// <param name="AreaNegated">La condizione d'area è ROVESCIA: vale quando le aree <b>non</b> sono attive.
/// <para>⚠️ Posizionale e SENZA valore di comodo, di proposito: un default farebbe compilare in silenzio ogni
/// chiamante che non ha ancora pensato alla polarità, e quello che si perde è una condizione resa al
/// rovescio — nessun errore, e una frase che dice l'opposto di quel che è scritto in archivio.</para></param>
/// <param name="AreaAll">Con più aree: <c>true</c> = valgono tutte, <c>false</c> = ne basta una qualunque.</param>
public sealed record ConditionClause(string? Runway, string? Area, bool AreaNegated, bool AreaAll, string? Custom)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Runway) && string.IsNullOrWhiteSpace(Area) && string.IsNullOrWhiteSpace(Custom);

    /// <summary>I nomi delle aree, ripuliti. Vuoto se non ce ne sono.</summary>
    public IReadOnlyList<string> Aree => TransferConditionText.SpezzaAree(Area);
}

/// <summary>Compone l'etichetta condizione per il DISPLAY (pill admin / chip nelle viste live): pista/e · area ·
/// personalizzata, le dimensioni presenti unite da « · ». La frase di coordinamento completa è composta a parte da
/// <see cref="CoordinationSentenceComposer"/> (fraseologia lingua-neutra). Qui solo il tag breve.</summary>
public static class TransferConditionText
{
    /// <summary>Il simbolo dell'area che deve essere NON attiva.
    /// <para>⚠️ Un simbolo e non una parola, ed è una divisione dichiarata (carta
    /// <c>2026-09-10-condizione-area-non-attiva.md</c>): questo è un <b>tag</b>, composto da una proprietà
    /// calcolata che non ha un localizzatore — il prefisso «area» è già non tradotto — mentre la <b>prosa</b>
    /// del documento la dice a parole, in tutt'e due le lingue, e quella la legge il pubblico.</para>
    /// <para>⚠️ <c>⊘</c> e non <c>✕</c>: in questa pagina il ✕ è il tasto che ELIMINA la riga.</para></summary>
    public const string NonAttiva = "⊘";

    /// <summary>Il separatore fra i nomi delle aree in <c>ConditionAreaLabel</c>.
    /// <para>🔴 <c>;</c> e <b>non</b> <c>/</c> come le piste: <b>cinque aree del catalogo IVAO hanno già lo
    /// <c>/</c> nel nome</b> — <c>LI/LD D35/A-CRIT</c>, <c>LI R49A/B/C/D/E/F - Zita</c> — e tagliarle lì le
    /// farebbe a pezzi. Misurato sulle 241 aree in archivio prima di scegliere: <c>;</c> non compare in
    /// nessun nome.</para></summary>
    public const string SeparatoreAree = ";";

    /// <summary>Il simbolo che unisce le aree nel TAG quando valgono tutte.</summary>
    public const string TutteLeAree = "+";
    /// <summary>Il simbolo che le unisce quando ne basta una qualunque.</summary>
    public const string UnaQualunque = "/";

    /// <summary>I nomi delle aree di un'etichetta, ripuliti e senza vuoti.</summary>
    public static IReadOnlyList<string> SpezzaAree(string? areaLabel) =>
        string.IsNullOrWhiteSpace(areaLabel)
            ? Array.Empty<string>()
            : areaLabel.Split(SeparatoreAree, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>L'etichetta da salvare, dai nomi scelti: ripuliti, senza vuoti e senza doppioni.</summary>
    public static string? UnisciAree(IEnumerable<string> nomi)
    {
        var puliti = nomi.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return puliti.Count == 0 ? null : string.Join(SeparatoreAree, puliti);
    }

    public static string? Display(string? runwayLabel, string? areaLabel, bool areaNegated, bool areaAll,
                                  string? customLabel)
    {
        var parts = new List<string>(3);
        var rwy = (runwayLabel ?? "").Trim();
        var aree = SpezzaAree(areaLabel);
        var custom = (customLabel ?? "").Trim();
        if (rwy.Length > 0) parts.Add(rwy);
        // ⚠️ Le bandiere senza le aree non si mostrano: non vogliono dire niente, e un «⊘» solo sarebbe un
        // avviso senza soggetto.
        if (aree.Count > 0)
        {
            // ⚠️ SIMBOLI e non parole: questa è una proprietà calcolata senza localizzatore, e il prefisso
            // «area» era già non tradotto. La divisione è dichiarata in carta — tag simbolico, prosa a parole
            // — ed è la stessa convenzione compatta di LevelFormatting (≤ ≥ + − ↑ ↓).
            var giunto = $" {(areaAll ? TutteLeAree : UnaQualunque)} ";
            var testo = $"area {string.Join(giunto, aree)}";
            parts.Add(areaNegated ? $"{testo} {NonAttiva}" : testo);
        }
        if (custom.Length > 0) parts.Add(custom);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
