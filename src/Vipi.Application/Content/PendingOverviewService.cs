using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un aeroporto che la sorgente non conferma più, con il verdetto sull'eliminazione già dato.</summary>
/// <param name="Eliminabile">Se la regola delle due chiamate lo consente adesso.</param>
/// <param name="MotivoDelNo">La frase che lo spiega quando non si può. <c>null</c> se si può.</param>
public sealed record PendingAirportRow(StaleAirportRow Riga, bool Eliminabile, string? MotivoDelNo);

/// <summary>
/// Il quadro d'insieme di ciò che <b>resta da fare</b>: quel che la sorgente non conferma più, e quel che
/// nei documenti è rimasto indietro.
///
/// <para><b>Perché una pagina e non tre angoli.</b> I pezzi c'erano già tutti, ma si leggevano uno per
/// volta: gli orfani in fondo alla Struttura di <b>una</b> ACC, gli impatti nel banner di <b>un</b>
/// documento aperto in modifica, e gli aeroporti spariti da nessuna parte. Chi voleva sapere «cosa devo
/// sistemare oggi» doveva aprire venti pagine e ricordarsele.</para>
///
/// <para>⚠️ Questa pagina è anche ciò che <b>dà il permesso</b> di eliminare: la regola è che si toglie solo
/// quel che la sorgente non manda da due giri, e qui si vede a colpo d'occhio chi ha già maturato quel
/// diritto e chi no — col motivo scritto accanto invece che scoperto premendo un tasto.</para>
/// </summary>
public sealed record PendingOverview(
    IReadOnlyList<OrphanSectorRow> Orfani,
    IReadOnlyList<DocumentImpactRow> Impatti,
    IReadOnlyList<PendingAirportRow> Aeroporti,
    IReadOnlyList<AffectedDoc> DocumentiFuoriElenco,
    DateTime? UltimoGiroAcc,
    DateTime? UltimoGiroAeroporti,
    DateTime? PenultimoGiroAeroporti)
{
    /// <summary>Quante cose chiedono attenzione in tutto: è il numero che va in cima e nella voce di menù.</summary>
    public int Totale => Orfani.Count + Impatti.Count + Aeroporti.Count + DocumentiFuoriElenco.Count;
}

/// <inheritdoc cref="PendingOverview"/>
public interface IPendingOverviewService
{
    Task<PendingOverview> LoadAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="PendingOverview"/>
public sealed class PendingOverviewService : IPendingOverviewService
{
    private readonly IOrphanSectorService _orfani;
    private readonly IOrphanSectorRepository _cataloghi;
    private readonly IDocumentImpactRepository _impatti;
    private readonly IImportStateStore _stati;
    private readonly IEditAuthorizationService _authz;
    private readonly IDocumentAdminService _gestione;
    private readonly IDeletionRepository _tutti;

    public PendingOverviewService(IOrphanSectorService orfani, IOrphanSectorRepository cataloghi,
        IDocumentImpactRepository impatti, IImportStateStore stati, IEditAuthorizationService authz,
        IDocumentAdminService gestione, IDeletionRepository tutti)
    {
        _orfani = orfani;
        _cataloghi = cataloghi;
        _impatti = impatti;
        _stati = stati;
        _authz = authz;
        _gestione = gestione;
        _tutti = tutti;
    }

    public async Task<PendingOverview> LoadAsync(CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);

        var ultimoAeroporti = await _stati.GetLastSuccessAsync(ImportCategories.AirportDirectory, ct);
        var penultimoAeroporti = await _stati.GetPrevSuccessAsync(ImportCategories.AirportDirectory, ct);
        var ultimoAcc = await _stati.GetLastSuccessAsync(ImportCategories.Acc, ct);

        // La soglia degli scali è quella del LORO giro, col solito margine: un giro può slittare, e senza
        // margine la prima notte storta farebbe comparire l'intera anagrafica come «sparita».
        var aeroporti = ultimoAeroporti is { } u
            ? await _cataloghi.ListStaleAirportsAsync(u - SogliaTimbro.Margine, ct)
            : Array.Empty<StaleAirportRow>();

        var righe = aeroporti
            .Select(a => new PendingAirportRow(a,
                SogliaEliminazione.Consentita(a.LastSeenUtc, penultimoAeroporti, isManual: false),
                SogliaEliminazione.MotivoDelRifiuto(a.LastSeenUtc, penultimoAeroporti, isManual: false)))
            .ToList();

        // ⚠️ I documenti SENZA UNA CHIAVE. Un documento vIPI che nessun descrittore riconosce come ACC o APP
        // finisce nel catch-all dell'aeroporto — e se un aeroporto non ce l'ha, la sua chiave di release nasce
        // VUOTA. Da lì in poi è un fantasma con la residenza: compare nell'elenco documenti senza nome di
        // scalo, non si pubblica (la chiave non risolve niente), la tendina degli incarichi lo nasconde di
        // proposito («un collegamento che nasce già rotto»), e nessun rilievo lo nomina.
        //
        // Come ci si finisce, misurato sul vipi.db vero: una «vIPI Roma» scritta il 10 luglio su
        // LIRR_ES_CTR, che allora era una radice. Un import l'ha poi infilato sotto LIRR_SU_CTR — e la vIPI
        // di ACC vuole un CTR RADICE. Il documento non è cambiato: gli è cambiato il terreno sotto.
        var gestiti = await _gestione.ListAsync(ct);
        var conId = gestiti.Where(d => d.DocumentId is not null).ToList();
        var fuori = conId
            .Where(d => string.IsNullOrWhiteSpace(d.ReleaseKey))
            .Select(d => new AffectedDoc(d.DocumentId!.Value, d.Title))
            .ToList();

        // E la rete di sicurezza: se un giorno un descrittore smettesse di catturare, un documento
        // sparirebbe dagli elenchi del tutto. Oggi non ne esistono — il catch-all li prende tutti — ma
        // costa una query e il giorno che succede si vede qui invece che da nessuna parte.
        var noti = conId.Select(d => d.DocumentId!.Value).ToHashSet();
        fuori.AddRange((await _tutti.AllDocumentsAsync(ct)).Where(d => !noti.Contains(d.Id)));

        return new PendingOverview(
            await _orfani.ListAsync(null, ct),
            await _impatti.ListAllOpenAsync(ct),
            righe,
            fuori,
            ultimoAcc, ultimoAeroporti, penultimoAeroporti);
    }
}
