using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Da quale tabella SID prendere i nomi delle SID citate nel testo (§A73, carta
/// <c>2026-09-18-riferimenti-sid-nel-testo.md</c> §4, deciso dal committente il 18 settembre 2026):
/// <b>la pubblica guarda la pubblica, l'editor guarda l'editor</b>.
///
/// <list type="bullet">
/// <item><b>Bozza</b>: la tabella viva, al ciclo corrente — quella che la bozza mostra.</item>
/// <item><b>Pubblica</b> (e anteprima di release): la tabella SID <b>pubblica</b> dello scalo citato — le righe
///   congelate nella sua release in vigore se la sua sezione SID è Freeze, altrimenti la derivazione di adesso.
///   Così testo e tabella pubblici dicono sempre lo stesso nome.</item>
/// <item>Lo scalo del documento stesso: la tabella che la pagina <b>sta già mostrando</b>, passata dal chiamante.
///   È anche il solo modo giusto per un'anteprima di release (tabella al ciclo della release) e per un vSOP
///   militare (tabella della release MILITARE, non di quella civile).</item>
/// </list>
///
/// <para>⚠️ Nessuna seconda derivazione: i nomi vengono da <see cref="IAirportSidDerivationService"/> e dalle
/// righe congelate, cioè dalle stesse righe della tabella. Una tabella derivata due volte in due modi è il
/// difetto già pagato con la vista rapida (§BR).</para>
/// </summary>
public interface ISidReferenceResolver
{
    /// <param name="sezioni">Le sezioni che la pagina sta per disegnare: si risolvono solo gli scali citati lì.</param>
    /// <param name="pubblica">Vero sulla pagina pubblica e nell'anteprima di una release; falso in bozza.</param>
    /// <param name="proprioIcao">Lo scalo del documento, se ne ha uno.</param>
    /// <param name="propriaTabella">La tabella SID che la pagina mostra per <paramref name="proprioIcao"/>.</param>
    Task<NomiSid> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null, CancellationToken ct = default);

    /// <summary>Come <see cref="PerVistaAsync"/>, su testi qualunque: le anteprime dell'editor, che hanno i
    /// blocchi di lavoro e non una vista. Sempre alla BOZZA — l'editor guarda l'editor.</summary>
    Task<NomiSid> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default);

    /// <summary>
    /// Le SID che si possono citare di uno scalo, per il selettore dell'editor: una voce per NOME (una SID su
    /// due piste è una voce sola, con le piste accanto), dalla tabella viva — quella che la bozza mostra.
    /// </summary>
    Task<IReadOnlyList<SidCitabile>> ElencoAsync(string icao, CancellationToken ct = default);
}

/// <summary>Una SID che si può citare nel testo.</summary>
/// <param name="Icao">Lo scalo.</param>
/// <param name="Codice">Il nome nell'archivio, <c>BANA8A</c>: è quello che va nel riferimento.</param>
/// <param name="Esteso">Come si scrive nel testo, <c>BANAV 8A</c>.</param>
/// <param name="Piste">Le piste su cui vale, <c>07, 25</c>.</param>
public sealed record SidCitabile(string Icao, string Codice, string Esteso, string Piste)
{
    /// <summary>Il riferimento da inserire nel testo.</summary>
    public string Riferimento => RiferimentiSid.Scrivi(Icao, Codice);
}

/// <inheritdoc cref="ISidReferenceResolver"/>
public sealed class SidReferenceResolver : ISidReferenceResolver
{
    private readonly IAirportSidDerivationService _sids;
    private readonly IFrozenSectionReader _frozen;

    public SidReferenceResolver(IAirportSidDerivationService sids, IFrozenSectionReader frozen)
    {
        _sids = sids;
        _frozen = frozen;
    }

    public Task<NomiSid> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null, CancellationToken ct = default) =>
        RisolviAsync(RiferimentiSid.TestiDi(sezioni), pubblica, proprioIcao, propriaTabella, ct);

    public Task<NomiSid> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
        RisolviAsync(testi, pubblica: false, null, null, ct);

    public async Task<IReadOnlyList<SidCitabile>> ElencoAsync(string icao, CancellationToken ct = default)
    {
        var scalo = RiferimentiSid.Norm(icao);
        if (scalo.Length != 4) return Array.Empty<SidCitabile>();

        var tabella = await _sids.DeriveAsync(scalo, ProcedureKind.Sid, null, ct);
        return tabella.Rows
            .Where(r => RiferimentiSid.Norm(r.Name).Length > 0)
            .GroupBy(r => RiferimentiSid.Norm(r.Name))
            .Select(g => new SidCitabile(scalo, g.Key, RiferimentiSid.NomeEsteso(g.First().Fix, g.Key),
                string.Join(", ", g.Select(r => r.Runway).Where(p => p != "—").Distinct().OrderBy(p => p, StringComparer.Ordinal))))
            // Solo nomi che il riferimento sa portare: uno che la regola non riconosce uscirebbe GREZZO in pagina,
            // e non lo proteggerebbe nemmeno la traduzione.
            .Where(s => RiferimentiSid.Contiene(s.Riferimento))
            .OrderBy(s => s.Esteso, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<NomiSid> RisolviAsync(IEnumerable<string?> testi, bool pubblica,
        string? proprioIcao, AirportSidView? propriaTabella, CancellationToken ct)
    {
        var scali = RiferimentiSid.ScaliCitati(testi);
        // La via breve, ed è quella di quasi ogni pagina: nessun riferimento, nessuna query.
        if (scali.Count == 0) return NomiSid.Vuoto;

        var proprio = RiferimentiSid.Norm(proprioIcao);
        var tabelle = new Dictionary<string, AirportSidView>(StringComparer.Ordinal);
        // ⚠️ In fila, non in parallelo: i servizi condividono il DbContext dello scope.
        foreach (var icao in scali)
        {
            if (propriaTabella is not null && icao == proprio) { tabelle[icao] = propriaTabella; continue; }

            AirportSidView? tabella = null;
            if (pubblica)
                tabella = (await _frozen.LoadAsync(ReleaseTargetType.Airport, icao, ct)).Get<AirportSidView>("sids");
            tabelle[icao] = tabella ?? await _sids.DeriveAsync(icao, ProcedureKind.Sid, null, ct);
        }
        return new NomiSid(tabelle);
    }
}
