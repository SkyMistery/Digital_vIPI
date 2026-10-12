namespace Vipi.Domain.Entities;

/// <summary>
/// Un NOTAM della divisione, copiato dalla sorgente (IVAO, <c>/v2/divisions/{id}/notams/all</c>). Carta
/// <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>.
///
/// <para>La tabella è lo <b>specchio</b> dell'elenco della sorgente, scaduti compresi: la sorgente non toglie un
/// NOTAM alla scadenza (misurato il 12 ottobre 2026: uno scaduto il 31 agosto era ancora in elenco), quindi
/// <b>se un NOTAM si mostra lo decide chi legge</b>, dalle date, e non la presenza della riga.</para>
///
/// <para>⚠️ Aeroporti e centri sono due elenchi di codici separati da uno spazio, e non due tabelle di legame: le
/// righe sono una manciata, si leggono tutte insieme e si filtrano in memoria. Non c'è nessuna domanda che il
/// database debba fare per codice.</para>
/// </summary>
public class Notam
{
    public const int NomeMassimo = 64;

    public int Id { get; set; }

    /// <summary>L'id della sorgente: univoco, è la chiave con cui il giro ritrova la riga.</summary>
    public int IvaoId { get; set; }

    /// <summary>Il numero del NOTAM com'è scritto alla sorgente, es. <c>IT-A003/25</c>.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Da quando vale. <c>null</c> = la sorgente non lo dice (succede): vale da sempre.</summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>Fino a quando vale. <c>null</c> = senza scadenza.</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>Il testo, com'è: righe separate da <c>\n</c>.</summary>
    public string Text { get; set; } = "";

    public bool Military { get; set; }

    /// <summary>Gli ICAO degli aeroporti che il NOTAM riguarda, maiuscoli, separati da uno spazio.</summary>
    public string Airports { get; set; } = "";

    /// <summary>I codici degli ACC che il NOTAM riguarda, maiuscoli, separati da uno spazio.</summary>
    public string Centers { get; set; } = "";

    /// <summary>L'ultima modifica alla sorgente, se la dice.</summary>
    public DateTime? SourceUpdatedUtc { get; set; }

    public DateTime ImportedAtUtc { get; set; }

    /// <summary>Un elenco di codici nella forma in cui si salva: maiuscoli, senza doppi, in ordine.</summary>
    public static string Unisci(IEnumerable<string> codici) =>
        string.Join(' ', codici.Select(c => (c ?? "").Trim().ToUpperInvariant())
                               .Where(c => c.Length > 0)
                               .Distinct(StringComparer.Ordinal)
                               .OrderBy(c => c, StringComparer.Ordinal));

    /// <summary>L'elenco salvato, di nuovo a codici.</summary>
    public static IReadOnlyList<string> Dividi(string? elenco) =>
        (elenco ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
