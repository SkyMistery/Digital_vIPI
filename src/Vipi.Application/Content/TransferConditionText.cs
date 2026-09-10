namespace Vipi.Application.Content;

/// <summary>
/// Le tre dimensioni della condizione di UNA riga: pista/e, area attiva, personalizzata. Tutte opzionali.
/// <para>Esiste come tipo perché nell'outline delle varianti una riga porta anche le condizioni dei propri
/// ANTENATI: un'eccezione di «pista 07» vale «con pista 07 in uso <b>e</b> R403B attiva», e la frase deve
/// dirlo tutto — viaggia da sola nella prosa del documento, senza il rientro che in tabella dà il contesto.
/// Una lista di clausole è la catena dalla capofila alla riga.</para>
/// </summary>
/// <param name="AreaNegated">La condizione d'area è ROVESCIA: vale quando l'area <b>non</b> è attiva.
/// <para>⚠️ Posizionale e SENZA valore di comodo, di proposito: un default farebbe compilare in silenzio ogni
/// chiamante che non ha ancora pensato alla polarità, e quello che si perde è una condizione resa al
/// rovescio — nessun errore, e una frase che dice l'opposto di quel che è scritto in archivio.</para></param>
public sealed record ConditionClause(string? Runway, string? Area, bool AreaNegated, string? Custom)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Runway) && string.IsNullOrWhiteSpace(Area) && string.IsNullOrWhiteSpace(Custom);
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

    public static string? Display(string? runwayLabel, string? areaLabel, bool areaNegated, string? customLabel)
    {
        var parts = new List<string>(3);
        var rwy = (runwayLabel ?? "").Trim();
        var area = (areaLabel ?? "").Trim();
        var custom = (customLabel ?? "").Trim();
        if (rwy.Length > 0) parts.Add(rwy);
        // ⚠️ La bandiera senza l'etichetta non si mostra: non vuol dire niente, e un «⊘» solo sarebbe
        // un avviso senza soggetto.
        if (area.Length > 0) parts.Add(areaNegated ? $"area {area} {NonAttiva}" : $"area {area}");
        if (custom.Length > 0) parts.Add(custom);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
