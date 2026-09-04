using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Application.Import;

/// <summary>
/// Il risolutore vero: quel che una cella incollata diventa dopo essere stata cercata sui <b>cataloghi di
/// divisione</b> — l'archivio degli scali e l'anagrafica delle radioassistenze.
///
/// <para>
/// ⚠️ <b>Non crea niente, mai.</b> Un codice che i cataloghi non conoscono torna «non letto» e la riga resta
/// fuori: l'import di <i>un</i> documento non aggiunge righe a un'anagrafica che e' di <i>tutti</i>. E' la
/// stessa ragione per cui in <c>MilNavaidsEditor</c> un campo che viene dalla sorgente non si modifica.
/// </para>
/// <para>
/// ⚠️ <b>Il valore vince sempre dal catalogo.</b> Chi incolla «LIBA Amendola» ha scritto un nome che potrebbe
/// essere vecchio, abbreviato o sbagliato: si tiene l'ICAO, e il nome lo mette l'archivio. Il testo incollato
/// resta visibile nell'anteprima, cosi' chi rilegge vede la differenza.
/// </para>
/// </summary>
public sealed class RisolutoreCelle : IRisolutoreCelle
{
    /// <summary>
    /// Quante volte al massimo si va a chiedere <b>alla sorgente</b> uno scalo che non e' in archivio.
    ///
    /// <para>⚠️ Gli alternati esteri (LGKR, LDDU) non sono in archivio per costruzione, e rifiutarli
    /// renderebbe inutile l'import proprio sulla tabella che l'ha motivato. Ma una chiamata di rete per riga
    /// su una tabella lunga e' un'altra cosa: oltre il tetto, le righe restano fuori con scritto perche'.</para>
    /// </summary>
    public const int MaxAllaSorgente = 25;

    private readonly IAirportNameLookup _scali;
    private readonly INavaidCatalog _radioassistenze;

    public RisolutoreCelle(IAirportNameLookup scali, INavaidCatalog radioassistenze)
    {
        _scali = scali;
        _radioassistenze = radioassistenze;
    }

    public async Task<IReadOnlyDictionary<string, EsitoRisoluzione>> RisolviAsync(
        TipoCella tipo, IReadOnlyCollection<string> valori, CancellationToken ct = default) => tipo switch
    {
        TipoCella.Aeroporto => await ScaliAsync(valori, ct).ConfigureAwait(false),
        TipoCella.Radioassistenza => await RadioassistenzeAsync(valori, ct).ConfigureAwait(false),
        _ => new Dictionary<string, EsitoRisoluzione>(),
    };

    // ---- scali ---------------------------------------------------------------------------------------

    private async Task<IReadOnlyDictionary<string, EsitoRisoluzione>> ScaliAsync(
        IReadOnlyCollection<string> valori, CancellationToken ct)
    {
        var perIcao = valori
            .Select(v => (Cella: v, Icao: Icao(v)))
            .Where(x => x.Icao.Length == 4)
            .ToList();

        var archivio = await _scali
            .NamesAsync(perIcao.Select(x => x.Icao).Distinct().ToList(), ct)
            .ConfigureAwait(false);

        var esiti = new Dictionary<string, EsitoRisoluzione>(StringComparer.OrdinalIgnoreCase);

        // ⚠️ Si chiede alla sorgente per ICAO, non per CELLA. La deduplica per cella non bastava: «LGKR» e
        // «LGKR Kerkyra» sono due celle diverse con lo stesso codice, e ognuna spendeva una chiamata di rete
        // E un colpo del tetto — su una tabella di alternati, dove lo stesso scalo compare piu' volte, si
        // arrivava a «troppi scali da verificare» avendone verificati molti meno di venticinque.
        var perCodice = new Dictionary<string, AirportName?>(StringComparer.OrdinalIgnoreCase);
        var allaSorgente = 0;

        foreach (var (cella, icao) in perIcao)
        {
            if (esiti.ContainsKey(cella)) continue;

            if (archivio.TryGetValue(icao, out var nome))
            {
                esiti[cella] = new EsitoRisoluzione($"{icao} {nome}", EsitoCella.Risolta, icao);
                continue;
            }

            if (!perCodice.TryGetValue(icao, out var trovato))
            {
                if (allaSorgente >= MaxAllaSorgente)
                {
                    esiti[cella] = new EsitoRisoluzione("", EsitoCella.NonLetta, null, "troppi scali da verificare");
                    continue;
                }

                allaSorgente++;
                trovato = await _scali.FindAsync(icao, ct).ConfigureAwait(false);
                perCodice[icao] = trovato;
            }

            esiti[cella] = trovato is null
                ? new EsitoRisoluzione("", EsitoCella.NonLetta, null, "scalo sconosciuto")
                : new EsitoRisoluzione($"{trovato.Icao} {trovato.Name}", EsitoCella.Risolta, trovato.Icao);
        }

        return esiti;
    }

    /// <summary>Il codice dentro la cella: «LIBA Amendola» e' comunque LIBA, e «(LIBA)» pure.</summary>
    private static string Icao(string cella)
    {
        var pulita = new string(cella.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();
        var primo = pulita.Split(' ').FirstOrDefault() ?? "";
        return primo.Length >= 4 ? primo.Substring(0, 4).ToUpperInvariant() : primo.ToUpperInvariant();
    }

    // ---- radioassistenze ------------------------------------------------------------------------------

    private async Task<IReadOnlyDictionary<string, EsitoRisoluzione>> RadioassistenzeAsync(
        IReadOnlyCollection<string> valori, CancellationToken ct)
    {
        var anagrafica = await _radioassistenze.ListAsync(ct).ConfigureAwait(false);
        var perCodice = anagrafica
            .GroupBy(n => n.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var esiti = new Dictionary<string, EsitoRisoluzione>(StringComparer.OrdinalIgnoreCase);
        foreach (var cella in valori)
        {
            if (esiti.ContainsKey(cella)) continue;

            var codice = Codice(cella);
            if (codice.Length == 0 || !perCodice.TryGetValue(codice, out var candidate))
            {
                esiti[cella] = new EsitoRisoluzione("", EsitoCella.NonLetta, null, "codice sconosciuto");
                continue;
            }

            // ⚠️ Un codice che e' di PIU' impianti non si rifiuta: si chiede quale. Su un campo militare
            // VORTAC e NDB condividono l'ident, e rifiutarli lascerebbe citabili quasi solo i codici unici.
            // Il canale che compare nel testo incollato pero' scioglie da solo quasi tutti i casi.
            var canale = Canale(cella);
            var scelte = canale.Length > 0
                ? candidate.Where(n => string.Equals(n.Channel, canale, StringComparison.OrdinalIgnoreCase)).ToList()
                : candidate;
            if (scelte.Count == 0) scelte = candidate;

            esiti[cella] = scelte.Count == 1
                ? new EsitoRisoluzione(Testo(scelte[0]), EsitoCella.Risolta, Chiave(scelte[0]))
                : new EsitoRisoluzione("", EsitoCella.DaScegliere, null, "piu' impianti con questo codice",
                    scelte.Select(n => new Candidato(Testo(n), Chiave(n))).ToList());
        }

        return esiti;
    }

    /// <summary>Il codice: la prima parola della cella, che e' come si scrive una radioassistenza citata.</summary>
    private static string Codice(string cella) =>
        (cella.Split(new[] { ' ', '\t', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "")
        .Trim().ToUpperInvariant();

    /// <summary>
    /// Il canale, se il testo incollato lo porta: la parola fatta di cifre piu' <c>X</c> o <c>Y</c> —
    /// <c>99Y</c>, <c>25X</c>. E' quel che distingue due impianti con lo stesso ident.
    /// </summary>
    private static string Canale(string cella) =>
        cella.Split(new[] { ' ', '\t', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToUpperInvariant())
            .FirstOrDefault(p => p.Length is >= 2 and <= 4
                                 && (p[p.Length - 1] == 'X' || p[p.Length - 1] == 'Y')
                                 && p.Take(p.Length - 1).All(char.IsDigit))
        ?? "";

    private static string Testo(NavaidRow n) => NavaidText.ConTipo(n.Code, n.Type, n.Channel, n.Frequency);

    /// <summary>L'identita' da riportare a chi applica: codice, natura e canale, come <c>NavaidKey</c>.</summary>
    private static string Chiave(NavaidRow n) => $"{n.Code}|{n.Kind}|{n.Channel}";
}
