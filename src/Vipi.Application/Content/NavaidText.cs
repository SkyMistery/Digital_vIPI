using Vipi.Application.Coordinates;

namespace Vipi.Application.Content;

/// <summary>
/// Come si scrive una radioassistenza in una tabella di SOP. Un posto solo, perché le forme sono due e si
/// somigliano: quella della colonna «Freq» delle Radioassistenze e quella della colonna «Navaids» degli
/// aeroporti alternati, che ci mette dentro anche il tipo.
///
/// <para>Le forme le ha dettate il committente, e sono queste:</para>
/// <list type="bullet">
/// <item><c>MNL - CH 99Y (115.25)</c> — col canale;</item>
/// <item><c>MNL - 115.25</c> — senza canale: <b>non</b> «CH (vuoto)», che è il modo in cui una tabella
/// generata a pezzi mostra di essere generata a pezzi;</item>
/// <item><c>MNL VORTACAN - 99Y (115.25)</c> — la forma con il tipo, per gli alternati.</item>
/// </list>
/// </summary>
public static class NavaidText
{
    /// <summary>La colonna «Freq»: codice, canale se c'è, frequenza fra parentesi.</summary>
    public static string Freq(string? code, string? channel, string? frequency)
    {
        var c = (code ?? "").Trim();
        var ch = (channel ?? "").Trim();
        var f = (frequency ?? "").Trim();

        // Senza né canale né frequenza resta il codice: una riga di tabella con un trattino e il vuoto dopo
        // fa pensare a un dato perso, mentre qui il dato non c'è ancora.
        if (ch.Length == 0 && f.Length == 0) return c;
        if (ch.Length == 0) return $"{c} - {f}";
        return f.Length == 0 ? $"{c} - CH {ch}" : $"{c} - CH {ch} ({f})";
    }

    /// <summary>La forma con il TIPO in mezzo, per gli aeroporti alternati: <c>MNL VORTACAN - 99Y (115.25)</c>.
    /// ⚠️ Qui il canale va <b>senza</b> «CH»: è la forma che ha chiesto il committente, e le due tabelle non
    /// stanno mai sulla stessa pagina.</summary>
    public static string ConTipo(string? code, string? type, string? channel, string? frequency)
    {
        var testa = string.Join(' ', new[] { (code ?? "").Trim(), (type ?? "").Trim() }.Where(s => s.Length > 0));
        var ch = (channel ?? "").Trim();
        var f = (frequency ?? "").Trim();

        if (ch.Length == 0 && f.Length == 0) return testa;
        if (ch.Length == 0) return $"{testa} - {f}";
        return f.Length == 0 ? $"{testa} - {ch}" : $"{testa} - {ch} ({f})";
    }

    /// <summary>Le coordinate come si leggono in tabella, o stringa vuota se la riga non ne ha.
    /// ⚠️ La coppia è un dato solo: una latitudine senza la sua longitudine non è una posizione, e mostrarne
    /// metà sarebbe peggio che non mostrarne nessuna.</summary>
    public static string Coordinate(double? lat, double? lon) =>
        lat is { } la && lon is { } lo ? SexagesimalPair.Format(la, lo) : "";
}
