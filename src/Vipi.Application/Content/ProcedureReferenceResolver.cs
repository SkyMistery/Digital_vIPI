using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Da quale tabella prendere i nomi delle procedure citate nel testo — SID e STAR (§A73, carta
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
public interface IProcedureReferenceResolver
{
    /// <param name="sezioni">Le sezioni che la pagina sta per disegnare: si risolvono solo gli scali citati lì.</param>
    /// <param name="pubblica">Vero sulla pagina pubblica e nell'anteprima di una release; falso in bozza.</param>
    /// <param name="proprioIcao">Lo scalo del documento, se ne ha uno.</param>
    /// <param name="propriaTabella">La tabella SID che la pagina mostra per <paramref name="proprioIcao"/>.</param>
    /// <param name="propriaTabellaStar">La tabella STAR che la pagina mostra per <paramref name="proprioIcao"/>.
    /// ⚠️ Serve accanto all'altra e non al suo posto: un documento cita le due famiglie nello stesso testo, e
    /// prendere gli arrivi da una derivazione diversa da quella che la pagina disegna è il difetto già pagato
    /// con la vista rapida (§BR).</param>
    Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null,
        AirportSidView? propriaTabellaStar = null, CancellationToken ct = default);

    /// <summary>Come <see cref="PerVistaAsync"/>, su testi qualunque: le anteprime dell'editor, che hanno i
    /// blocchi di lavoro e non una vista. Sempre alla BOZZA — l'editor guarda l'editor.</summary>
    Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default);

    /// <summary>
    /// Le procedure di un verso che si possono citare di uno scalo, per il selettore dell'editor: una voce per
    /// NOME (una procedura su due piste è una voce sola, con le piste accanto), dalla tabella viva — quella che
    /// la bozza mostra.
    /// </summary>
    Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao, ProcedureKind kind = ProcedureKind.Sid,
        CancellationToken ct = default);
}

/// <summary>Una procedura che si può citare nel testo.</summary>
/// <param name="Kind">Il verso: decide la parola del riferimento e la tabella in cui si cercherà il nome.</param>
/// <param name="Icao">Lo scalo.</param>
/// <param name="Codice">Il nome nell'archivio, <c>BANA8A</c>: è quello che va nel riferimento.</param>
/// <param name="Esteso">Come si scrive nel testo, <c>BANAV 8A</c>.</param>
/// <param name="Piste">Le piste su cui vale, <c>07, 25</c>.</param>
public sealed record ProceduraCitabile(ProcedureKind Kind, string Icao, string Codice, string Esteso, string Piste)
{
    /// <summary>Il riferimento da inserire nel testo.</summary>
    public string Riferimento => RiferimentiProcedura.Scrivi(Kind, Icao, Codice);
}

/// <inheritdoc cref="IProcedureReferenceResolver"/>
public sealed class ProcedureReferenceResolver : IProcedureReferenceResolver
{
    private readonly IAirportSidDerivationService _sids;
    private readonly IFrozenSectionReader _frozen;

    public ProcedureReferenceResolver(IAirportSidDerivationService sids, IFrozenSectionReader frozen)
    {
        _sids = sids;
        _frozen = frozen;
    }

    public Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null,
        AirportSidView? propriaTabellaStar = null, CancellationToken ct = default) =>
        RisolviAsync(RiferimentiProcedura.TestiDi(sezioni), pubblica, proprioIcao, propriaTabella, propriaTabellaStar, ct);

    public Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
        RisolviAsync(testi, pubblica: false, null, null, null, ct);

    public async Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao,
        ProcedureKind kind = ProcedureKind.Sid, CancellationToken ct = default)
    {
        var scalo = RiferimentiProcedura.Norm(icao);
        if (scalo.Length != 4) return Array.Empty<ProceduraCitabile>();

        var tabella = await _sids.DeriveAsync(scalo, kind, null, ct);
        return tabella.Rows
            .Where(r => RiferimentiProcedura.Norm(r.Name).Length > 0)
            .GroupBy(r => RiferimentiProcedura.Norm(r.Name))
            .Select(g => new ProceduraCitabile(kind, scalo, g.Key, RiferimentiProcedura.NomeEsteso(g.First().Fix, g.Key),
                string.Join(", ", g.Select(r => r.Runway).Where(p => p != "—").Distinct().OrderBy(p => p, StringComparer.Ordinal))))
            // Solo nomi che il riferimento sa portare: uno che la regola non riconosce uscirebbe GREZZO in pagina,
            // e non lo proteggerebbe nemmeno la traduzione.
            .Where(s => RiferimentiProcedura.Contiene(s.Riferimento))
            .OrderBy(s => s.Esteso, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<NomiProcedura> RisolviAsync(IEnumerable<string?> testi, bool pubblica,
        string? proprioIcao, AirportSidView? propriaTabella, AirportSidView? propriaTabellaStar, CancellationToken ct)
    {
        var citate = RiferimentiProcedura.TabelleCitate(testi);
        // La via breve, ed è quella di quasi ogni pagina: nessun riferimento, nessuna query.
        if (citate.Count == 0) return NomiProcedura.Vuoto;

        var proprio = RiferimentiProcedura.Norm(proprioIcao);
        var tabelle = new Dictionary<(ProcedureKind Kind, string Icao), AirportSidView>();
        // ⚠️ In fila, non in parallelo: i servizi condividono il DbContext dello scope. E si chiede SOLO quel che
        // il testo cita: un documento che nomina tre SID non deve far derivare anche gli arrivi.
        foreach (var (kind, icao) in citate)
        {
            var propria = kind == ProcedureKind.Star ? propriaTabellaStar : propriaTabella;
            if (propria is not null && icao == proprio) { tabelle[(kind, icao)] = propria; continue; }

            AirportSidView? tabella = null;
            if (pubblica)
                tabella = (await _frozen.LoadAsync(ReleaseTargetType.Airport, icao, ct))
                    .Get<AirportSidView>(kind == ProcedureKind.Star ? "stars" : "sids");
            tabelle[(kind, icao)] = tabella ?? await _sids.DeriveAsync(icao, kind, null, ct);
        }
        return new NomiProcedura(tabelle);
    }
}
