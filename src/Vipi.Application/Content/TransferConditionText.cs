namespace Vipi.Application.Content;

/// <summary>
/// Le tre dimensioni della condizione di UNA riga: pista/e, area attiva, personalizzata. Tutte opzionali.
/// <para>Esiste come tipo perché nell'outline delle varianti una riga porta anche le condizioni dei propri
/// ANTENATI: un'eccezione di «pista 07» vale «con pista 07 in uso <b>e</b> R403B attiva», e la frase deve
/// dirlo tutto — viaggia da sola nella prosa del documento, senza il rientro che in tabella dà il contesto.
/// Una lista di clausole è la catena dalla capofila alla riga.</para>
/// </summary>
public sealed record ConditionClause(string? Runway, string? Area, string? Custom)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Runway) && string.IsNullOrWhiteSpace(Area) && string.IsNullOrWhiteSpace(Custom);
}

/// <summary>Compone l'etichetta condizione per il DISPLAY (pill admin / chip nelle viste live): pista/e · area ·
/// personalizzata, le dimensioni presenti unite da « · ». La frase di coordinamento completa è composta a parte da
/// <see cref="CoordinationSentenceComposer"/> (fraseologia lingua-neutra). Qui solo il tag breve.</summary>
public static class TransferConditionText
{
    public static string? Display(string? runwayLabel, string? areaLabel, string? customLabel)
    {
        var parts = new List<string>(3);
        var rwy = (runwayLabel ?? "").Trim();
        var area = (areaLabel ?? "").Trim();
        var custom = (customLabel ?? "").Trim();
        if (rwy.Length > 0) parts.Add(rwy);
        if (area.Length > 0) parts.Add($"area {area}");
        if (custom.Length > 0) parts.Add(custom);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
