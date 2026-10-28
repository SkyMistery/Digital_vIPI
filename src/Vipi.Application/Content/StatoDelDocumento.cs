using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// La bozza di oggi contro la prossima release programmata (S133): che cosa porterebbe in più, e se sono cambiate
/// anche le derivate congelate (TORA, minimi LVP…), che non hanno righe di testo.
/// </summary>
/// <param name="Ciclo">Il ciclo AIRAC della programmata.</param>
/// <param name="EffettivaUtc">Quando entra in vigore.</param>
public sealed record ConfrontoProgrammata(string Ciclo, DateTime EffettivaUtc, IReadOnlyList<ReleaseDiffRow> Righe,
    bool DerivateCambiate)
{
    /// <summary>Vero se la programmata porta già la bozza di oggi.</summary>
    public bool Allineata => Righe.Count == 0 && !DerivateCambiate;
}

/// <summary>
/// La scheda di un documento (carta <c>docs/feature/2026-10-28-stato-dei-documenti.md</c> §1 D1, S133): che cosa
/// manca e che cosa è cambiato, in un posto solo. «Da rivedere» non è qui: sono le righe di «Da fare», e le legge
/// <c>IWorkListService</c> con le loro azioni.
/// </summary>
/// <param name="Sezioni">Le sezioni da scrivere; null se il documento non ne ha di contate.</param>
/// <param name="TraduzioniMancanti">Le frasi della bozza che la traduzione non ha ancora.</param>
/// <param name="CicloInVigore">Il ciclo della release che il pubblico legge oggi; null = mai pubblicato.</param>
/// <param name="RispettoAlPubblico">Bozza contro release in vigore. Vuoto anche se non c'è una release in vigore.</param>
/// <param name="Programmata">Bozza contro la prossima programmata; null = nessuna programmata.</param>
public sealed record SchedaDelDocumento(
    int DocumentId,
    StatoSezioni? Sezioni,
    int TraduzioniMancanti,
    string? CicloInVigore,
    IReadOnlyList<ReleaseDiffRow> RispettoAlPubblico,
    ConfrontoProgrammata? Programmata,
    DateTime CalcolataUtc)
{
    public int SezioniVuote => Sezioni?.Vuote ?? 0;

    /// <summary>Vero se c'è qualcosa da completare: sezioni vuote o frasi da tradurre.</summary>
    public bool DaCompletare => SezioniVuote > 0 || TraduzioniMancanti > 0;

    /// <summary>Vero se il pubblico legge qualcosa di diverso dalla bozza.</summary>
    public bool DaPubblicare => CicloInVigore is not null && RispettoAlPubblico.Count > 0;

    /// <summary>Vero se la prossima programmata non porta la bozza di oggi.</summary>
    public bool ProgrammataIndietro => Programmata is { Allineata: false };
}

public interface IStatoDelDocumentoService
{
    /// <summary>La scheda del documento di questo bersaglio, calcolata adesso; null se il bersaglio non risolve a un
    /// documento. Costa due snapshot (al ciclo corrente e a quello della programmata): si chiede per UN documento.</summary>
    Task<SchedaDelDocumento?> SchedaAsync(ReleaseTargetType type, string key, CancellationToken ct = default);
}

public sealed class StatoDelDocumentoService : IStatoDelDocumentoService
{
    private readonly IReleaseService _releases;
    private readonly IReleaseRepository _releaseRepo;
    private readonly IReleaseTargetRegistry _targets;
    private readonly ISezioniDaScrivereService _sezioni;
    private readonly IStatoTraduzione _traduzione;
    private readonly IConfrontiDocumentiRepository? _confronti;

    /// <param name="confronti">Se c'è, la scheda calcolata riscrive il riassunto della pagina Documenti: chi l'ha
    /// appena guardata ritrova nell'elenco gli stessi numeri.</param>
    public StatoDelDocumentoService(IReleaseService releases, IReleaseRepository releaseRepo,
        IReleaseTargetRegistry targets, ISezioniDaScrivereService sezioni, IStatoTraduzione traduzione,
        IConfrontiDocumentiRepository? confronti = null)
    {
        _confronti = confronti;
        _releases = releases;
        _releaseRepo = releaseRepo;
        _targets = targets;
        _sezioni = sezioni;
        _traduzione = traduzione;
    }

    public async Task<SchedaDelDocumento?> SchedaAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        int? id;
        try { id = await _targets.For(type).ResolveDocumentIdAsync(key, ct); }
        catch (KeyNotFoundException) { return null; }   // tipo di documento non registrato
        if (id is not int docId) return null;

        // ⚠️ In fila, non in parallelo: lo scope è uno, e il DbContext non regge due letture insieme.
        var sezioni = await _sezioni.DocumentoAsync(docId, ct);
        var traduzione = await _traduzione.DocumentoAsync(docId, ct);
        var effettiva = await _releaseRepo.GetEffectiveAsync(type, key, DateTime.UtcNow, ct);
        var pubblico = effettiva is null
            ? Array.Empty<ReleaseDiffRow>()
            : await _releases.DriftFromEffectiveAsync(type, key, ct: ct);
        var programmata = await _releases.DriftFromScheduledAsync(type, key, ct);

        var scheda = new SchedaDelDocumento(docId, sezioni, traduzione?.Bozza.Mancanti ?? 0, effettiva?.ReleaseAiracCycle,
            pubblico, programmata, DateTime.UtcNow);
        if (_confronti is not null && (scheda.CicloInVigore is not null || scheda.Programmata is not null))
            await _confronti.ScriviAsync(new[] { ConfrontoSalvato.Da(scheda) }, ct);
        return scheda;
    }
}
