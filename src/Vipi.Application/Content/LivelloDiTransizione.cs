using System.Text.RegularExpressions;

namespace Vipi.Application.Content;

/// <summary>
/// Il Transition Level «adesso»: la fascia della tabella dello scalo che contiene il QNH del METAR.
///
/// <para>🔴 <b>Una funzione sola</b> (U-216 e U-227 della revisione totale 3). Erano tre: la vista rapida e la sezione
/// del documento confrontavano le fasce <b>in testo</b> su <see cref="AirportTransitionView"/>, e il vAWOS le
/// confrontava <b>in numeri</b> sulla tabella viva dell'anagrafica. Due regole scritte due volte, e due dati diversi:
/// il vAWOS mostrava TA e TL dell'editor anche quando il documento pubblicato diceva altro.</para>
///
/// <para>⚠️ Dalla <b>tabella</b>, non da una formula a fasce come nel prototipo: il TL è dell'AIP di quell'aeroporto.
/// Senza QNH, o senza una fascia che lo contenga: null, che si legge «non lo so» e non la prima riga.</para>
/// </summary>
public static class LivelloDiTransizione
{
    /// <summary>Il livello della prima fascia che contiene <paramref name="qnh"/>; null se non si sa.</summary>
    public static string? Adesso(AirportTransitionView vista, int? qnh)
    {
        if (qnh is not int q) return null;
        var livello = vista.Rows.FirstOrDefault(r => FasciaContiene(r.QnhRange, q) && !string.IsNullOrWhiteSpace(r.Level))?.Level;
        return livello?.Trim();
    }

    /// <summary>
    /// Vero se <paramref name="qnh"/> ricade nell'intervallo testuale della riga TL. Formati riconosciuti:
    /// «1014 – 1030» (range), «≥ 1031» / «&gt;= 1031», «≤ 984» / «&lt;= 984», «&gt; 1031», «&lt; 984».
    /// Riga senza numeri ⇒ nessuna corrispondenza.
    /// </summary>
    public static bool FasciaContiene(string? range, int qnh)
    {
        var text = range ?? "";
        var nums = Regex.Matches(text, @"\d+")
            .Select(m => int.TryParse(m.Value, out var v) ? v : (int?)null)
            .Where(v => v is not null).Select(v => v!.Value).ToList();
        if (nums.Count == 0) return false;

        if (text.Contains('≥') || text.Contains(">=")) return qnh >= nums[0];
        if (text.Contains('≤') || text.Contains("<=")) return qnh <= nums[0];
        if (text.Contains('>')) return qnh > nums[0];
        if (text.Contains('<')) return qnh < nums[0];
        return nums.Count >= 2 && qnh >= Math.Min(nums[0], nums[1]) && qnh <= Math.Max(nums[0], nums[1]);
    }
}
