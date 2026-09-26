using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Vipi.Application.Content;

/// <summary>
/// Il payload della <b>tabella generica</b>: <c>{"columns":[…],"rows":[{"cells":[…]}]}</c>, cioe' il blocco
/// <c>Table</c> <b>senza</b> variante — quello in cui le colonne le decide chi scrive.
///
/// <para>
/// ⚠️ Esiste perche' la stessa lettura e la stessa scrittura stavano in <b>due</b> editor
/// (<c>DocumentBlocksEditor</c> e <c>DocumentSectionsEditor</c>), con tanto di commento «stesso formato JSON
/// di». Due copie della stessa forma sono due posti dove correggere la prossima trappola del JSON, e le
/// trappole di questo formato sono gia' costate una pagina in 500.
/// </para>
/// <para>
/// ⚠️ Le due guardie che quel 500 ha pagato, e che qui stanno in un posto solo: una <b>radice che non e' un
/// oggetto</b> (la forma vecchia delle aree regolamentate e' un array) e una <b>riga che non e' un
/// oggetto</b> fanno alzare <c>InvalidOperationException</c> a <c>TryGetProperty</c> — che non e' una
/// <c>JsonException</c> e quindi passava indenne dal <c>catch</c>.
/// </para>
/// </summary>
public static class TabellaGenerica
{
    /// <summary>Il payload di una tabella nuova: due colonne e nessuna riga.</summary>
    public static string Nuova(string colonna1 = "Colonna 1", string colonna2 = "Colonna 2") =>
        Scrivi(new List<string> { colonna1, colonna2 }, new List<List<string>>());

    /// <summary>
    /// Colonne e righe come stanno nel JSON. Un JSON assente, vuoto o illeggibile da' una tabella vuota:
    /// una tabella da compilare, non un errore in faccia a chi legge.
    /// </summary>
    public static (List<string> Colonne, List<List<string>> Righe) Leggi(string? json)
    {
        var colonne = new List<string>();
        var righe = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(json)) return (colonne, righe);

        try
        {
            using var doc = JsonDocument.Parse(json!);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return (colonne, righe);

            if (r.TryGetProperty("columns", out var c) && c.ValueKind == JsonValueKind.Array)
                colonne = c.EnumerateArray().Select(e => e.GetString() ?? "").ToList();

            if (r.TryGetProperty("rows", out var rr) && rr.ValueKind == JsonValueKind.Array)
                foreach (var riga in rr.EnumerateArray())
                    righe.Add(riga.ValueKind == JsonValueKind.Object
                              && riga.TryGetProperty("cells", out var cs)
                              && cs.ValueKind == JsonValueKind.Array
                        ? cs.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                        : new List<string>());
        }
        catch (JsonException) { }

        return (colonne, righe);
    }

    /// <summary>Larghezza minima e massima di una colonna, in percento della tabella.</summary>
    public const int LarghezzaMin = 1, LarghezzaMax = 100;

    /// <summary>
    /// Le larghezze delle colonne (S4, 23 settembre 2026): percento della tabella, <c>null</c> = automatica.
    /// Sempre <b>una per colonna</b>, allineate a <c>columns</c>: le mancanti sono automatiche, quelle in piu'
    /// si tagliano, un valore che non e' un numero fra <see cref="LarghezzaMin"/> e <see cref="LarghezzaMax"/>
    /// torna automatico.
    /// <para>⚠️ E' una chiave in PIU' nel JSON (<c>widths</c>) e non una colonna nel database: la tabella vive
    /// gia' tutta nel <c>BodyJson</c>, e la traduzione lascia intatte le chiavi che non sono testo.</para>
    /// </summary>
    public static List<int?> Larghezze(string? json)
    {
        var quante = Leggi(json).Colonne.Count;
        var valori = new List<int?>();
        if (quante > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(json!);
                var r = doc.RootElement;
                if (r.ValueKind == JsonValueKind.Object
                    && r.TryGetProperty("widths", out var w) && w.ValueKind == JsonValueKind.Array)
                    valori = w.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n) ? Valida(n) : null)
                        .ToList();
            }
            catch (JsonException) { }
        }
        return Allinea(valori, quante);
    }

    /// <summary>
    /// La larghezza scritta da chi edita: vuoto, non un numero o fuori dall'intervallo → automatica. Il «%»
    /// in coda si accetta, perche' e' quel che si scrive naturalmente.
    /// </summary>
    public static int? LarghezzaDa(string? testo)
    {
        var t = (testo ?? "").Trim().TrimEnd('%').Trim();
        return int.TryParse(t, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? Valida(n) : null;
    }

    private static int? Valida(int n) => n is >= LarghezzaMin and <= LarghezzaMax ? n : null;

    private static List<int?> Allinea(IEnumerable<int?>? larghezze, int quante)
    {
        var l = (larghezze ?? Enumerable.Empty<int?>()).Take(Math.Max(0, quante)).ToList();
        while (l.Count < quante) l.Add(null);
        return l;
    }

    /// <summary>
    /// Il JSON da salvare. <paramref name="larghezze"/> si allinea alle colonne; se sono tutte automatiche la
    /// chiave <c>widths</c> non si scrive, e il JSON resta quello di prima della S4.
    /// <para>⚠️ Chi riscrive una tabella gia' salvata DEVE ripassare le larghezze lette con
    /// <see cref="Larghezze"/>: senza, un clic su una cella le cancellerebbe.</para>
    /// </summary>
    public static string Scrivi(IReadOnlyList<string> colonne, IReadOnlyList<IReadOnlyList<string>> righe,
        IEnumerable<int?>? larghezze = null)
    {
        var l = Allinea(larghezze?.Select(x => x is int n ? Valida(n) : null), colonne.Count);
        return l.Any(x => x is not null)
            ? JsonSerializer.Serialize(new { columns = colonne, widths = l, rows = righe.Select(r => new { cells = r }) })
            : JsonSerializer.Serialize(new { columns = colonne, rows = righe.Select(r => new { cells = r }) });
    }

    /// <summary>
    /// Le righe portate a <paramref name="quante"/> celle: le mancanti si aggiungono vuote, quelle in piu'
    /// si tagliano.
    /// <para>⚠️ Serve dopo un import e dopo una colonna aggiunta o tolta: in una tabella HTML una riga corta
    /// non lascia una cella vuota, <b>sposta tutto a sinistra</b>. Il dato sembrerebbe sbagliato invece che
    /// incompleto.</para>
    /// </summary>
    public static List<List<string>> Pareggia(IEnumerable<IReadOnlyList<string>> righe, int quante) =>
        righe.Select(r => Enumerable.Range(0, Math.Max(0, quante))
                .Select(i => i < r.Count ? (r[i] ?? "") : "")
                .ToList())
            .ToList();
}
