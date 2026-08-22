using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// «Questo punto scritto a mano esiste davvero?» — la domanda che il catalogo permette di fare, e i limiti
/// entro cui ha senso farla.
///
/// <para><b>Perché non basta «sta nel catalogo?».</b> Un CoP è testo libero con validazione soft, e lo è di
/// proposito (vedi <see cref="CopList"/>): l'archivio contiene intervalli di aerovie (<c>Y01-Y12</c>), STAR
/// (<c>TOPNO 3A</c>), «ALL», «ALL to GR». Nessuna di queste è un nome di punto, e segnalarle sarebbe peggio di
/// non segnalare niente: un avviso che grida su dati corretti si impara a ignorare, e allora smette di servire
/// anche quando ha ragione.</para>
///
/// <para><b>La regola.</b> Si giudica solo ciò che <b>somiglia</b> a un nome di punto — da 2 a 5 lettere e
/// nient'altro. Sotto quella forma il catalogo è autorevole; fuori, il campo resta libero e muto. Misurato
/// sull'archivio reale: 52 token CoP su 62 sono verificabili, e 442 transition importate su 446. Il resto è
/// testo che non si giudica.</para>
///
/// <para><b>Il catalogo vuoto non accusa nessuno.</b> Se GitHub non risponde <see cref="NavaidCatalog.Empty"/>
/// arriva qui, e in quel caso NIENTE è sconosciuto. L'alternativa — segnare tutto — trasformerebbe un disservizio
/// della sorgente in una pagina piena di avvisi falsi.</para>
/// </summary>
public static class NavaidCheck
{
    /// <summary>Token che sono parole intere ma non nomi di punto. <c>ALL</c> è il quantificatore dei sorvoli
    /// («tutti i punti»), non un fix, e ha esattamente la forma che il controllo giudicherebbe.</summary>
    private static readonly HashSet<string> Special = new(StringComparer.OrdinalIgnoreCase) { "ALL" };

    /// <summary>Vero se il token ha la forma di un nome di punto (2-5 lettere sole) e non è un termine
    /// speciale. Solo su questi il catalogo può dire qualcosa.</summary>
    public static bool IsCheckable(string? token)
    {
        var t = (token ?? "").Trim();
        if (t.Length is < 2 or > 5) return false;
        foreach (var ch in t)
            if (!char.IsAsciiLetter(ch)) return false;
        return !Special.Contains(t);
    }

    /// <summary>Vero se il token ha forma di punto e il catalogo NON lo conosce. Falso in ogni altro caso —
    /// compreso il catalogo assente o vuoto.</summary>
    public static bool IsUnknown(string? token, NavaidCatalog? catalog)
    {
        if (catalog is null || catalog.Names.Count == 0) return false;
        return IsCheckable(token) && !catalog.Names.Contains(token!.Trim());
    }

    /// <summary>
    /// I punti di un elenco CoP che il catalogo non conosce, nell'ordine in cui sono scritti e senza ripetizioni.
    /// Vuoto = nulla da segnalare.
    /// </summary>
    public static IReadOnlyList<string> UnknownCops(string? raw, NavaidCatalog? catalog)
    {
        if (catalog is null || catalog.Names.Count == 0) return Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var token in CopList.Parse(raw))
            if (IsUnknown(token, catalog) && seen.Add(token.Trim()))
                result.Add(token.Trim());
        return result;
    }
}
