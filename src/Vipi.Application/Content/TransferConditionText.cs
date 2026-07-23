namespace Vipi.Application.Content;

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
