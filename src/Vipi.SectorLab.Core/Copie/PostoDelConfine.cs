using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>Un confine con la forma del settore: dov'è, e se sta nella cartella del tipo del settore.</summary>
public sealed record ConfineDelSettore(string File, int Record, string Etichetta, bool Uguale, bool AlSuoPosto);

/// <summary>Dove va il confine di un settore dinamico, e i confini che hanno la sua forma (anche nel posto sbagliato).</summary>
public sealed record PostoDelSettore(string Cartella, string Perche, IReadOnlyList<ConfineDelSettore> Confini);

/// <summary>
/// Il file giusto per un confine (lotto «Subito» slice 13e, «file per file» J5): i settori di aerovia (<c>…_CTR</c>,
/// e le FIC <c>…_FSS</c>) stanno in <c>HI_AIRSPACE</c>, quelli di avvicinamento (<c>…_APP</c>) in <c>LOW_AIRSPACE</c>.
/// </summary>
/// <remarks>
/// I nomi delle voci di <c>.hartcc</c>/<c>.lartcc</c> non sono nomi di posizione (<c>RR NE</c>, <c>LIBB CS0</c>): il tipo
/// non si legge dal nome. La voce si lega al settore dinamico che ha la sua forma (<see cref="FormeUguali"/>: uguale, o
/// 9 vertici su 10), e il tipo è quello delle posizioni di quel settore (committente, 5 ottobre 2026). Misura sul fork
/// del 4 ottobre: 25 voci su 43 hanno un settore della stessa forma, e nessuna è nel file sbagliato. Una voce che
/// somiglia a settori dei due tipi, o a nessuno, non si segnala; e nemmeno un settore con posizioni dei due tipi.
/// </remarks>
public static class PostoDelConfine
{
    public const string Alta = "HI_AIRSPACE";
    public const string Bassa = "LOW_AIRSPACE";

    /// <summary>La cartella del confine di un settore, dal tipo delle sue posizioni; null se il tipo non lo dice.</summary>
    public static string? CartellaPer(TflSector settore)
    {
        ArgumentNullException.ThrowIfNull(settore);
        var cartelle = settore.Posizioni().Select(CartellaDellaPosizione).Distinct().ToList();
        return cartelle is [{ } sola] ? sola : null;
    }

    private static string? CartellaDellaPosizione(string posizione)
        => posizione[(posizione.LastIndexOf('_') + 1)..].ToUpperInvariant() switch
        {
            "CTR" or "FSS" => Alta,
            "APP" => Bassa,
            _ => null,
        };

    /// <summary>La cartella in cui sta un file di confini, dall'estensione; null per gli altri file.</summary>
    public static string? CartellaDi(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".hartcc" => Alta,
        ".lartcc" => Bassa,
        _ => null,
    };

    /// <summary>
    /// Per la scheda di un settore dinamico: dove va il suo confine e quali confini hanno la sua forma. Null se il
    /// record non è un settore o se le sue posizioni non dicono il tipo (una torre, tipi misti).
    /// </summary>
    public static PostoDelSettore? Di(SessioneAperta sessione, FormeUguali forme, string file, int record)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(forme);
        if (Settore(sessione, file, record) is not { } settore || CartellaPer(settore) is not { } cartella)
            return null;

        var confini = forme.Di(file, record).SelectMany(p => p.Copie)
            .Where(c => CartellaDi(c.Dove.File) is not null)
            .GroupBy(c => (c.Dove.File.ToUpperInvariant(), c.Dove.Record))
            .Select(g => g.OrderByDescending(c => c.Uguale).First())
            .Select(c => new ConfineDelSettore(c.Dove.File, c.Dove.Record, c.Dove.Etichetta, c.Uguale, CartellaDi(c.Dove.File) == cartella))
            .OrderByDescending(c => c.AlSuoPosto).ThenBy(c => c.File, StringComparer.Ordinal).ThenBy(c => c.Record)
            .ToList();
        string perche = cartella == Alta
            ? "un settore di aerovia (…_CTR, …_FSS) ha il confine in HI_AIRSPACE (.hartcc)"
            : "un settore di avvicinamento (…_APP) ha il confine in LOW_AIRSPACE (.lartcc)";
        return new PostoDelSettore(cartella, perche, confini);
    }

    /// <summary>
    /// L'avviso <see cref="Regola.ConfineNelFileSbagliato"/> per ogni voce che ha la forma di settori di un tipo solo, e
    /// sta nella cartella dell'altro. Sulla prima riga della voce, coi nomi dei file della sessione.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Problemi(SessioneAperta sessione, FormeUguali forme)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(forme);
        foreach (var file in sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal))
        {
            if (CartellaDi(file.Relativo) is not { } qui || file is not IFileConRecord conRecord)
                continue;
            for (int i = 0; i < conRecord.RecordDelModello.Count; i++)
            {
                var settori = forme.Di(file.Relativo, i).SelectMany(p => p.Copie)
                    .Select(c => (Copia: c, Settore: Settore(sessione, c.Dove.File, c.Dove.Record)))
                    .Where(c => c.Settore is not null)
                    .Select(c => (c.Copia, Settore: c.Settore!, Cartella: CartellaPer(c.Settore!)))
                    .Where(c => c.Cartella is not null)
                    .ToList();
                if (settori.Count == 0 || settori.Any(s => s.Cartella == qui))
                    continue;

                var riga = conRecord.RigheDelRecord(i, 0).FirstOrDefault(r => r.DelRecord);
                string elenco = string.Join(", ", settori.Select(s => $"{s.Settore.SectorCode.Trim()} ({NomeDelFile(s.Copia.Dove.File)})").Distinct().Take(3));
                string dettaglio = qui == Alta
                    ? $"Ha la forma di {elenco}: un settore di avvicinamento (…_APP) va in {Bassa} (.lartcc), non in {Alta}"
                    : $"Ha la forma di {elenco}: un settore di aerovia (…_CTR, …_FSS) va in {Alta} (.hartcc), non in {Bassa}";
                yield return new ProblemaDelSector(Regola.ConfineNelFileSbagliato, file.Relativo, riga?.Numero ?? 0, riga?.Testo ?? "", dettaglio);
            }
        }
    }

    private static TflSector? Settore(SessioneAperta sessione, string file, int record)
        => sessione.File.GetValueOrDefault(file) is IFileConRecord conRecord && record >= 0 && record < conRecord.RecordDelModello.Count
            ? conRecord.RecordDelModello[record] as TflSector
            : null;

    private static string NomeDelFile(string relativo) => relativo[(relativo.LastIndexOf('/') + 1)..];
}
