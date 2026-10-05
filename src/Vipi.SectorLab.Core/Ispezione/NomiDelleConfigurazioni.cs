using System.Text.RegularExpressions;
using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Il nome di una configurazione, letto: la sigla dell'ACC, la parola, il numero e la lettera in coda.</summary>
public sealed record NomeDiConfigurazione(string Acc, string Parola, string Numero, string Coda);

/// <summary>
/// I nomi delle configurazioni nei confini (lotto «Subito» slice 13f, «file per file» K1): sul fork tre scritture —
/// <c>RR CONF1</c> in <c>lirr.hartcc</c>, <c>RR CNF1</c> in <c>lirr_tma.lartcc</c>, <c>MM CONF 1</c> in
/// <c>limm_tma.lartcc</c>. Il nome è la voce della finestra di selezione di Aurora: una forma sola si trova prima.
/// </summary>
/// <remarks>
/// <b>La forma giusta è un'impostazione dell'app</b>, non del codice (committente, 5 ottobre 2026: «la farei impostabile
/// dall'app, così se cambia non devo ricorrere al codice»): un modello con <c>{ACC}</c> e <c>{N}</c>, per esempio
/// <c>{ACC} CONF{N}</c> o <c>{ACC} CNF {N}</c>. Quello di base, <c>{ACC} CONF{N}</c>, è una scelta dell'agente (la
/// scrittura di <c>HI_AIRSPACE</c>, dove sta un file per ACC). La lettera in coda al numero (<c>CONF1M</c>) resta.
/// </remarks>
public static partial class NomiDelleConfigurazioni
{
    /// <summary>Il modello di base, finché l'AOD non ne sceglie un altro nelle impostazioni.</summary>
    public const string DiBase = "{ACC} CONF{N}";

    private const string Acc = "{ACC}";
    private const string N = "{N}";

    // Le parole che sul fork dicono «configurazione»; quella del modello scelto si aggiunge da sé.
    private static readonly string[] Parole = ["CONF", "CNF", "CONFIG", "CFG"];

    /// <summary>Perché un modello non va, o null: ci vogliono <c>{ACC}</c>, una parola e <c>{N}</c>, in quest'ordine.</summary>
    public static string? PercheNonVa(string? modello)
    {
        string m = (modello ?? string.Empty).Trim();
        int acc = m.IndexOf(Acc, StringComparison.OrdinalIgnoreCase), n = m.IndexOf(N, StringComparison.OrdinalIgnoreCase);
        if (acc != 0 || n < 0 || !m.EndsWith(N, StringComparison.OrdinalIgnoreCase))
            return $"Il modello comincia con {Acc} (la sigla dell'ACC: RR, MM) e finisce con {N} (il numero), per esempio «{DiBase}».";
        string mezzo = m[Acc.Length..n];
        if (mezzo.Trim().Length == 0 || !mezzo.Trim().All(char.IsAsciiLetter) || !mezzo.StartsWith(' '))
            return $"Fra {Acc} e {N} ci vuole uno spazio e la parola (solo lettere), per esempio «{DiBase}» o «{Acc} CNF {N}».";
        return null;
    }

    /// <summary>Il modello scritto pulito (segnaposto e parola in maiuscolo), o quello di base se non va.</summary>
    public static string Pulito(string? modello)
        => PercheNonVa(modello) is null ? modello!.Trim().ToUpperInvariant() : DiBase;

    /// <summary>
    /// Legge il nome di una voce come configurazione, o null: sigla, una delle parole (quelle note e quella del
    /// <paramref name="modello"/>), il numero (<c>1</c>, <c>2.1</c>) e una lettera in coda (<c>CONF1M</c>).
    /// </summary>
    public static NomeDiConfigurazione? Leggi(string? nome, string modello = DiBase)
    {
        if (Forma().Match((nome ?? string.Empty).Trim()) is not { Success: true } m)
            return null;
        string parola = m.Groups["parola"].Value.ToUpperInvariant();
        return Parole.Contains(parola) || parola == ParolaDi(modello)
            ? new NomeDiConfigurazione(m.Groups["acc"].Value, parola, m.Groups["numero"].Value, m.Groups["coda"].Value)
            : null;
    }

    /// <summary>Il nome come lo vuole il modello.</summary>
    public static string Scrivi(NomeDiConfigurazione nome, string modello)
    {
        ArgumentNullException.ThrowIfNull(nome);
        string m = Pulito(modello);
        return m.Replace(Acc, nome.Acc, StringComparison.Ordinal).Replace(N, nome.Numero + nome.Coda, StringComparison.Ordinal);
    }

    /// <summary>
    /// L'avviso <see cref="Regola.NomeDellaConfigurazione"/> per ogni voce di <c>.hartcc</c>/<c>.lartcc</c> che è una
    /// configurazione e non è scritta come il modello. Sulla prima riga della voce.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Problemi(SessioneAperta sessione, string modello)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        string pulito = Pulito(modello);
        foreach (var file in sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal))
        {
            if (PostoDelConfine.CartellaDi(file.Relativo) is null || file is not IFileConRecord conRecord)
                continue;
            // Una configurazione sta spesso in più pezzi con lo stesso nome (più record): un avviso per nome, sul primo.
            var gruppi = conRecord.RecordDelModello.Select((r, i) => (Gruppo: r as StaticBoundaryGroup, Indice: i))
                .Where(g => g.Gruppo is not null)
                .GroupBy(g => g.Gruppo!.Name.Trim(), StringComparer.Ordinal);
            foreach (var pezzi in gruppi)
            {
                if (Leggi(pezzi.Key, pulito) is not { } letto || string.Equals(Scrivi(letto, pulito), pezzi.Key, StringComparison.Ordinal))
                    continue;

                var riga = conRecord.RigheDelRecord(pezzi.First().Indice, 0).FirstOrDefault(r => r.DelRecord);
                int quanti = pezzi.Count();
                yield return new ProblemaDelSector(Regola.NomeDellaConfigurazione, file.Relativo, riga?.Numero ?? 0, riga?.Testo ?? "",
                    $"«{pezzi.Key}»{(quanti > 1 ? $" ({quanti} pezzi)" : "")}: le configurazioni si scrivono «{Scrivi(letto, pulito)}» (modello «{pulito}», nelle impostazioni del Lab) — il nome si cambia dalla scheda della voce");
            }
        }
    }

    private static string ParolaDi(string modello)
    {
        string m = Pulito(modello);
        return m[Acc.Length..m.IndexOf(N, StringComparison.Ordinal)].Trim();
    }

    [GeneratedRegex(@"^(?<acc>\S+)\s+(?<parola>[A-Za-z]+)\s*(?<numero>\d+(?:\.\d+)?)(?<coda>[A-Za-z]?)$")]
    private static partial Regex Forma();
}
