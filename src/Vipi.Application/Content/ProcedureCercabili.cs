using System.Collections.Concurrent;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Partenze e arrivi di uno scalo come li vede il pubblico, per la ricerca.</summary>
public sealed record ProcedureDelloScalo(IReadOnlyList<AirportSidRowView> Sids, IReadOnlyList<AirportSidRowView> Stars)
{
    public static ProcedureDelloScalo Vuote { get; } = new(Array.Empty<AirportSidRowView>(), Array.Empty<AirportSidRowView>());
}

/// <summary>
/// Le SID e le STAR di uno scalo per la barra di ricerca (committente, 1 ottobre 2026: «se cerco ALAXI non mi dà che è nei
/// documenti di Napoli anche se è tra i suoi fix»). Le procedure non stanno nel testo pubblicato — si derivano a
/// view-time — e l'indice della ricerca, che legge gli snapshot, non le vedeva.
///
/// <para>⚠️ Dalla STESSA vista della pagina pubblica (<see cref="IAirportViewDerivationService.ResolveForViewAsync"/>,
/// con l'edizione giusta e le sezioni congelate): una seconda strada verso la tabella delle procedure divergerebbe dal
/// documento alla prima correzione a mano. È la regola dell'API degli aeroporti.</para>
///
/// <para>⚠️ Con una memoria di <see cref="Durata"/>: la ricerca parte a ogni tasto, e derivare le procedure di
/// settanta scali ogni volta sarebbe la ricerca lenta dell'11 agosto 2026 di nuovo. Le SID cambiano al ciclo AIRAC o
/// quando qualcuno le corregge; dieci minuti di ritardo nella ricerca non li vede nessuno.</para>
/// </summary>
public interface IProcedureCercabili
{
    Task<ProcedureDelloScalo> PerScaloAsync(string icao, ReleaseTargetType edizione, CancellationToken ct = default);
}

/// <inheritdoc cref="IProcedureCercabili"/>
public sealed class ProcedureCercabili : IProcedureCercabili
{
    internal static readonly TimeSpan Durata = TimeSpan.FromMinutes(10);

    // Statica: il servizio è per richiesta (la derivazione legge il database), la memoria è di tutto il processo.
    private static readonly ConcurrentDictionary<(string, ReleaseTargetType), (DateTime Quando, ProcedureDelloScalo Dati)> Memoria = new();

    private readonly IAirportViewDerivationService _viste;
    private readonly Func<DateTime> _adesso;

    public ProcedureCercabili(IAirportViewDerivationService viste, Func<DateTime>? adesso = null)
    {
        _viste = viste;
        _adesso = adesso ?? (() => DateTime.UtcNow);
    }

    public async Task<ProcedureDelloScalo> PerScaloAsync(string icao, ReleaseTargetType edizione, CancellationToken ct = default)
    {
        var chiave = ((icao ?? "").Trim().ToUpperInvariant(), edizione);
        if (chiave.Item1.Length == 0) return ProcedureDelloScalo.Vuote;
        var ora = _adesso();
        if (Memoria.TryGetValue(chiave, out var nota) && ora - nota.Quando < Durata) return nota.Dati;

        var d = await _viste.ResolveForViewAsync(chiave.Item1, useFrozen: true, edizione, ct: ct);
        var dati = new ProcedureDelloScalo(d.Sids.Rows, d.Stars.Rows);
        Memoria[chiave] = (ora, dati);
        return dati;
    }

    /// <summary>Per i test: la memoria è di processo.</summary>
    internal static void Dimentica() => Memoria.Clear();
}

/// <summary>
/// Quando una riga di SID/STAR risponde a ciò che si cerca. UNA regola per la vista live e per la barra di ricerca
/// (committente, 1 ottobre 2026): si trova cercando il codice (<c>ALAX7G</c>), il nome completo con o senza spazio
/// (<c>ALAXI 7G</c>, <c>ALAXI7G</c>), il fix o un punto della transition. Maiuscole e spazi non contano.
/// </summary>
public static class CercaProcedura
{
    public static bool Combacia(AirportSidRowView riga, string? cercato)
    {
        var q = (cercato ?? "").Trim();
        if (q.Length == 0) return true;
        if (riga.Fix.Contains(q, StringComparison.OrdinalIgnoreCase)
            || riga.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || riga.Transition.Contains(q, StringComparison.OrdinalIgnoreCase))
            return true;

        var c = Compatto(q);
        return c.Length > 0
               && (Compatto(riga.Name).Contains(c, StringComparison.Ordinal)
                   || Compatto(NomeCompleto(riga)).Contains(c, StringComparison.Ordinal)
                   || Compatto(riga.Transition).Contains(c, StringComparison.Ordinal));
    }

    /// <summary>Il nome come si dice in radio: <c>ALAX7G</c> → <c>ALAXI 7G</c> (vedi RiferimentiProcedura.NomeEsteso).</summary>
    public static string NomeCompleto(AirportSidRowView riga) => RiferimentiProcedura.NomeEsteso(riga.Fix, riga.Name);

    private static string Compatto(string s) =>
        string.Concat((s ?? "").Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static readonly string[] Cifre = { "zero", "one", "two", "tree", "four", "fife", "six", "seven", "eight", "niner" };

    private static readonly string[] Alfabeto =
    {
        "alfa", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india", "juliett", "kilo", "lima", "mike",
        "november", "oscar", "papa", "quebec", "romeo", "sierra", "tango", "uniform", "victor", "whiskey", "x-ray",
        "yankee", "zulu",
    };

    /// <summary>
    /// La fonia del nome completo, in inglese come in frequenza: <c>ALAXI 7G</c> → <c>ALAXI seven golf</c>. Null se il
    /// nome non è nella forma semplice «punto + cifra + lettera» (un composto o un nome militare).
    /// </summary>
    public static string? Fonia(string nomeCompleto)
    {
        var parti = (nomeCompleto ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parti.Length != 2 || parti[1].Length != 2 || !char.IsDigit(parti[1][0]) || !char.IsLetter(parti[1][1])) return null;
        var lettera = char.ToUpperInvariant(parti[1][1]);
        if (lettera is < 'A' or > 'Z') return null;
        return $"{parti[0]} {Cifre[parti[1][0] - '0']} {Alfabeto[lettera - 'A']}";
    }
}
