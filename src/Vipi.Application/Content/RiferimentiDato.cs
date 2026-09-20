using System.Text.RegularExpressions;

namespace Vipi.Application.Content;

/// <summary>Che dato cita un riferimento del testo.</summary>
public enum TipoDato
{
    /// <summary>La frequenza di un ente, per callsign: <c>[[FREQ LIRF_TWR]]</c> → <c>118.700</c>.</summary>
    Frequenza,

    /// <summary>Il nominativo radio di un ente, per callsign: <c>[[ATC LIRR_CTR]]</c> → <c>Roma Radar</c>.</summary>
    Nominativo,

    /// <summary>Una soglia pista di uno scalo: <c>[[RWY LIRF 16L]]</c>. Esce com'è scritta — vedi sotto.</summary>
    Pista,

    /// <summary>Un punto del catalogo: <c>[[FIX OST]]</c>. Esce com'è scritto — vedi sotto.</summary>
    Punto,
}

/// <summary>
/// Un <b>dato</b> citato nel testo di un documento: una frequenza, un nominativo, una soglia, un punto
/// (carta <c>docs/feature/2026-09-20-riferimenti-ai-dati.md</c>).
///
/// <para>Nel testo salvato sta <c>[[FREQ LIRF_TWR]]</c>: il tipo e la <b>chiave</b>. Al momento di mostrare si
/// cerca il valore di oggi — la frequenza del settore <c>LIRF_TWR</c> — e si scrive quello. Il testo salvato
/// non cambia mai, quindi la memoria di traduzione resta valida.</para>
///
/// <para>🔴 <b>Niente «ultimo valore visto» nel gettone</b>, al contrario delle procedure (§A73). Là l'ultimo
/// nome serviva a <b>ritrovare la riga</b> quando il nome cambia; qui la riga si ritrova con la chiave, che è
/// stabile. Se il dato non si trova più esce <b>la chiave</b> — <c>LIRF_TWR</c> dice sempre qualcosa di vero —
/// e l'editor lo segnala.</para>
///
/// <para>⚠️ Per <see cref="TipoDato.Pista"/> e <see cref="TipoDato.Punto"/> la chiave <b>è</b> il valore: escono
/// come sono scritti, sempre. Un rinomino per deriva magnetica (<c>16L</c> → <c>17L</c>) non si indovina, e il
/// guadagno di quei due gettoni è l'<b>avviso</b> quando il dato non esiste più.</para>
/// </summary>
public static class RiferimentiDato
{
    /// <summary>
    /// Il riferimento nel testo: <c>[[TIPO CHIAVE]]</c>, più un secondo gettone dove la chiave è di due pezzi
    /// (<c>[[RWY LIRF 16L]]</c>).
    /// </summary>
    /// <remarks>
    /// Le chiavi ammettono maiuscole, cifre e il trattino basso dei callsign (<c>LIRF_TWR</c>). ⚠️ Nessuna
    /// virgoletta né barra rovescia: il riferimento vive anche dentro le stringhe del JSON delle tabelle, e si
    /// sostituisce sul testo del JSON.
    /// <para>⚠️ <c>internal</c> per la stessa ragione delle procedure: la protezione dalla traduzione usa la
    /// STESSA regola, e un riferimento che il renderer riconosce e la protezione no partirebbe verso il
    /// motore.</para>
    /// </remarks>
    internal static readonly Regex Riferimento = new(
        @"\[\[(FREQ|ATC|RWY|FIX) ([A-Z0-9_]{2,16})(?: ([A-Z0-9]{1,4}))?\]\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>La parola del tipo, come si scrive nel riferimento.</summary>
    public static string Parola(TipoDato tipo) => tipo switch
    {
        TipoDato.Frequenza => "FREQ",
        TipoDato.Nominativo => "ATC",
        TipoDato.Pista => "RWY",
        _ => "FIX",
    };

    private static TipoDato TipoDi(Match m) => m.Groups[1].Value switch
    {
        "FREQ" => TipoDato.Frequenza,
        "ATC" => TipoDato.Nominativo,
        "RWY" => TipoDato.Pista,
        _ => TipoDato.Punto,
    };

    /// <summary>La chiave come la si confronta: i due gettoni uniti da uno spazio, in maiuscolo.</summary>
    private static string ChiaveDi(Match m) =>
        m.Groups[3].Success ? $"{m.Groups[2].Value} {m.Groups[3].Value}" : m.Groups[2].Value;

    /// <summary>Il testo del riferimento per quel tipo e quella chiave.</summary>
    public static string Scrivi(TipoDato tipo, string chiave) =>
        $"[[{Parola(tipo)} {RiferimentiProcedura.Norm(chiave)}]]";

    /// <summary>Vero se il testo contiene almeno un riferimento a un dato. La via breve: quasi nessun testo ne ha.</summary>
    public static bool Contiene(string? testo) =>
        !string.IsNullOrEmpty(testo)
        && testo.Contains("[[", StringComparison.Ordinal)
        && Riferimento.IsMatch(testo);

    /// <summary>I dati citati: tipo e chiave, senza doppioni.</summary>
    public static IReadOnlySet<(TipoDato Tipo, string Chiave)> Citati(IEnumerable<string?> testi)
    {
        var citati = new HashSet<(TipoDato, string)>();
        foreach (var testo in testi)
        {
            if (!Contiene(testo)) continue;
            foreach (Match m in Riferimento.Matches(testo!)) citati.Add((TipoDi(m), ChiaveDi(m)));
        }
        return citati;
    }

    /// <summary>
    /// Il testo con ogni riferimento a un dato sostituito dal valore di oggi. <paramref name="valori"/>
    /// <c>null</c>, o un dato che non si trova più, = la <b>chiave</b>: un riferimento non esce mai grezzo.
    /// </summary>
    public static string? Sostituisci(string? testo, ValoriDato? valori)
    {
        if (!Contiene(testo)) return testo;
        return Riferimento.Replace(testo!, m =>
        {
            var chiave = ChiaveDi(m);
            return valori?.Valore(TipoDi(m), chiave) ?? chiave;
        });
    }
}

/// <summary>
/// I valori di oggi dei dati citati da un documento: per tipo e chiave, quel che va scritto. Si costruisce
/// dalle stesse sorgenti che le pagine mostrano — il catalogo dei settori per frequenze e nominativi, le piste
/// dello scalo, il catalogo dei punti — così il testo dice sempre quel che dicono le tabelle.
/// </summary>
public sealed class ValoriDato
{
    private readonly Dictionary<(TipoDato Tipo, string Chiave), string> _valori = new();

    public static ValoriDato Vuoto { get; } = new(Array.Empty<(TipoDato, string, string)>());

    /// <param name="voci">Tipo, chiave e valore di oggi. Le chiavi si normalizzano qui, una volta sola.</param>
    public ValoriDato(IEnumerable<(TipoDato Tipo, string Chiave, string Valore)> voci)
    {
        foreach (var (tipo, chiave, valore) in voci)
        {
            var k = RiferimentiProcedura.Norm(chiave);
            if (k.Length == 0 || string.IsNullOrWhiteSpace(valore)) continue;
            _valori[(tipo, k)] = valore.Trim();
        }
    }

    /// <summary>Il valore di oggi, o <c>null</c> se quel dato non si trova più.</summary>
    public string? Valore(TipoDato tipo, string chiave) =>
        _valori.TryGetValue((tipo, RiferimentiProcedura.Norm(chiave)), out var v) ? v : null;

    /// <summary>Vero se non c'è niente da sostituire: la via breve dei chiamanti.</summary>
    public bool Vuota => _valori.Count == 0;
}

/// <summary>Un dato citato che l'editor segnala in cima al documento: non si trova più.</summary>
/// <param name="Tipo">Che dato era.</param>
/// <param name="Chiave">La chiave scritta nel riferimento (<c>LIRF_TWR</c>).</param>
/// <param name="Dove">Le sezioni in cui compare, per titolo.</param>
public sealed record DatoDaRivedere(TipoDato Tipo, string Chiave, IReadOnlyList<string> Dove)
{
    /// <summary>Come si nomina il tipo in pagina: <c>FREQ</c>, <c>ATC</c>, <c>RWY</c>, <c>FIX</c>.</summary>
    public string Parola => RiferimentiDato.Parola(Tipo);
}

/// <summary>Il controllo dei dati citati in un documento: quali non si trovano più.</summary>
public static class ControlloDatiCitati
{
    /// <summary>
    /// I dati da ricontrollare nei testi dati, ognuno con la sezione in cui sta. Una voce per riferimento
    /// (tipo + chiave), con tutte le sezioni che lo citano.
    /// </summary>
    public static IReadOnlyList<DatoDaRivedere> Controlla(
        IEnumerable<(string Dove, string? Testo)> testi, ValoriDato valori)
    {
        var dove = new Dictionary<(TipoDato Tipo, string Chiave), List<string>>();
        foreach (var (sezione, testo) in testi)
        {
            if (!RiferimentiDato.Contiene(testo)) continue;
            foreach (var (tipo, chiave) in RiferimentiDato.Citati(new[] { testo }))
            {
                if (!dove.TryGetValue((tipo, chiave), out var sezioni)) dove[(tipo, chiave)] = sezioni = new List<string>();
                if (!sezioni.Contains(sezione)) sezioni.Add(sezione);
            }
        }

        // ⚠️ Solo i tipi che si RISOLVONO: per una pista o un punto la chiave è il valore, esce sempre giusta,
        // e segnalarla come «non trovata» sarebbe un falso allarme a ogni riga. Il loro controllo arriva con la
        // loro sorgente (slice 6c della carta).
        return dove.Where(d => d.Key.Tipo is TipoDato.Frequenza or TipoDato.Nominativo)
            .Where(d => valori.Valore(d.Key.Tipo, d.Key.Chiave) is null)
            .OrderBy(d => d.Key.Tipo)
            .ThenBy(d => d.Key.Chiave, StringComparer.Ordinal)
            .Select(d => new DatoDaRivedere(d.Key.Tipo, d.Key.Chiave, d.Value))
            .ToList();
    }
}
