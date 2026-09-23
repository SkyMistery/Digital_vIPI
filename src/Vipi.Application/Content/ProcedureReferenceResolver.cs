using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

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
    /// I nomi di oggi di tabelle date, senza passare da un testo: i punti dei trasferimenti (21 settembre 2026).
    /// Tabelle VIVE, come la bozza: la frase di un accordo si congela nella release che la pubblica.
    /// <para>Il corpo di ripiego («nessun nome») serve ai finti dei test, che guardano il testo e non i
    /// trasferimenti: con lui un punto esce come è scritto, cioè il comportamento di prima.</para>
    /// </summary>
    Task<NomiProcedura> PerTabelleAsync(IReadOnlySet<(ProcedureKind Kind, string Icao)> tabelle,
        CancellationToken ct = default) => Task.FromResult(NomiProcedura.Vuoto);

    /// <summary>
    /// Le procedure di un verso che si possono citare di uno scalo, per il selettore dell'editor e per i punti
    /// dei trasferimenti: una voce per NOME (una procedura su due piste è una voce sola, con le piste accanto),
    /// dalla tabella viva — <b>vista dal ciclo entrante</b>.
    /// <para>🔴 Dal ciclo ENTRANTE e non da quello di oggi (§S3 del filone sito, 23 settembre 2026): chi scrive un
    /// accordo o cita una procedura scrive per i giorni che vengono. Guardando a oggi, ERIKA 1A di LIRN non si
    /// trovava fra i punti: le STAR il sito le legge dal 21 settembre, al primo import sono righe NUOVE e
    /// prendono il ciclo che il sectorfile dichiara (2610, in vigore dal 1° ottobre) — tutte fuori fino ad allora.
    /// Dal ciclo entrante si vede anche tutto quel che vale oggi: <c>IsPublicAt</c> confronta con <c>&gt;=</c>, e una
    /// procedura tolta dalla sorgente esce dalla tabella già adesso.</para>
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

    /// <summary>Per il ciclo entrante di <see cref="ElencoAsync"/>. Facoltativo come in <c>ProcedureImporter</c>:
    /// senza, l'elenco guarda al ciclo di oggi — il comportamento di prima, che serve ai finti dei test.</summary>
    private readonly IAiracService? _airac;

    public ProcedureReferenceResolver(IAirportSidDerivationService sids, IFrozenSectionReader frozen,
        IAiracService? airac = null)
    {
        _sids = sids;
        _frozen = frozen;
        _airac = airac;
    }

    public Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
        string? proprioIcao = null, AirportSidView? propriaTabella = null,
        AirportSidView? propriaTabellaStar = null, CancellationToken ct = default) =>
        RisolviAsync(RiferimentiProcedura.TestiDi(sezioni), pubblica, proprioIcao, propriaTabella, propriaTabellaStar, ct);

    public Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
        RisolviAsync(testi, pubblica: false, null, null, null, ct);

    public async Task<NomiProcedura> PerTabelleAsync(IReadOnlySet<(ProcedureKind Kind, string Icao)> tabelle,
        CancellationToken ct = default)
    {
        if (tabelle.Count == 0) return NomiProcedura.Vuoto;
        var viste = new Dictionary<(ProcedureKind Kind, string Icao), AirportSidView>();
        // In fila: i servizi condividono il DbContext dello scope (vedi RisolviAsync).
        foreach (var (kind, icao) in tabelle)
        {
            var scalo = RiferimentiProcedura.Norm(icao);
            if (scalo.Length != 4) continue;
            viste[(kind, scalo)] = await _sids.DeriveAsync(scalo, kind, null, ct);
        }
        return new NomiProcedura(viste);
    }

    public async Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao,
        ProcedureKind kind = ProcedureKind.Sid, CancellationToken ct = default)
    {
        var scalo = RiferimentiProcedura.Norm(icao);
        if (scalo.Length != 4) return Array.Empty<ProceduraCitabile>();

        var entrante = _airac?.NextCycles(DateTime.UtcNow, 2)[1].Cycle;
        var tabella = await _sids.DeriveAsync(scalo, kind, entrante, ct);
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
