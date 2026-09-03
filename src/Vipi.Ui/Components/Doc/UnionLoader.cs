using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// Un membro di un'unione <b>già caricato e pronto da disegnare</b>: il titolo con cui si intesta il suo
/// gruppo, le sue sezioni (per l'indice) e il suo corpo (per la colonna centrale).
///
/// <para>⚠️ Il corpo è un <see cref="RenderFragment"/> già confezionato, e non un «tipo di famiglia» che la
/// pagina dovrebbe poi switchare: aggiungere una famiglia all'unione deve costare <b>un caso qui dentro</b>,
/// non un ramo in ogni pagina che ospita un'unione. È la Regola del 2 del <c>FEATURE-PROCESS</c> applicata
/// prima che il secondo switch nasca.</para>
/// </summary>
public sealed record MembroUnito(
    UnionMemberView Membro,
    string Titolo,
    IReadOnlyList<SectionView> Sezioni,
    RenderFragment Corpo)
{
    /// <summary>L'ancora del gruppo di questo membro: è dove atterra chi arriva dalla sua vecchia URL.</summary>
    public string Ancora => AncoraDi(Membro.DocumentId);

    /// <summary>L'ancora del gruppo di un documento dentro una pagina unita.
    /// <para>⚠️ Sull'ID DEL DOCUMENTO e non sulla posizione: l'ordine dei membri si cambia con due frecce, e
    /// un'ancora che cambia insieme all'ordine è un collegamento salvato che un giorno porta altrove.</para></summary>
    public static string AncoraDi(int documentId) => $"doc-{documentId}";
}

/// <summary>
/// Il caricamento dei membri di un'unione di documenti (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §3): data l'unione e il documento OSPITE, prepara gli
/// <b>altri</b> — ognuno col caricatore della sua famiglia.
///
/// <para>
/// ⚠️ <b>Si costruisce dallo scope di chi lo usa</b> (<c>ActivatorUtilities</c>) quando quella pagina ne ha
/// uno proprio: dentro istanzia i caricatori di famiglia, e uno di quelli — l'aeroporto — <b>deve</b> stare
/// sullo scope della pagina. Vedi il commento in testa a <see cref="AirportMemberLoader"/>.
/// </para>
/// </summary>
public sealed class UnionLoader
{
    private readonly IServiceProvider _sp;
    private readonly IReleaseService _releases;
    private readonly Vipi.Application.Routing.IDocRoutesRegistry _rotte;

    public UnionLoader(IServiceProvider sp, IReleaseService releases,
                       Vipi.Application.Routing.IDocRoutesRegistry rotte)
    {
        _sp = sp;
        _releases = releases;
        _rotte = rotte;
    }

    /// <summary>
    /// L'indirizzo <b>pubblico</b> della pagina unita, per chi ospite non è: la pagina dell'ospite, ancorata
    /// al gruppo di questo documento. null se questo documento <i>è</i> l'ospite, o se l'indirizzo non si
    /// risolve.
    ///
    /// <para>
    /// ⚠️ <b>Vale per la sola vista pubblica.</b> Le anteprime (<c>?as=rel:</c>, <c>?as=draft</c>) e gli
    /// editor restano dove sono: reindirizzare anche quelle renderebbe l'anteprima della release di un
    /// membro irraggiungibile, e chi la sta guardando la sta guardando apposta.
    /// </para>
    /// <para>
    /// ⚠️ L'ACC lo dice l'OSPITE (<c>ManagedDoc.AccCode</c>), non la pagina che sta reindirizzando: due
    /// documenti uniti possono stare su ACC diversi, e mandare al proprio codice darebbe un indirizzo che
    /// non esiste — o, peggio, uno che esiste e mostra un altro scalo.
    /// </para>
    /// </summary>
    public string? IndirizzoDellOspite(UnionView unione, ReleaseTargetType mioTipo, string miaChiave)
    {
        if (unione.IsHostTarget(mioTipo, miaChiave)) return null;

        var mio = unione.Members.FirstOrDefault(
            m => m.Doc.ReleaseTarget == mioTipo
                 && string.Equals(m.Doc.ReleaseKey, miaChiave, StringComparison.OrdinalIgnoreCase));
        if (mio is null) return null;

        var ospite = unione.Host;
        var acc = (ospite.Doc.AccCode ?? "").ToLowerInvariant();
        if (acc.Length == 0) return null;

        var url = _rotte.For(ospite.Doc.ReleaseTarget)
                        .PublicUrl(acc, ospite.Doc.ReleaseKey, ospite.Doc.NeighbourCode);
        // ⚠️ null è un valore previsto dal contratto delle rotte, e vuol dire «questo tipo non ha una pagina
        // pubblica». Chi chiama resta dov'è invece di mandare da nessuna parte.
        return url is null ? null : $"{url}#{MembroUnito.AncoraDi(mio.DocumentId)}";
    }

    /// <summary>
    /// Gli altri membri dell'unione, nell'ordine, già caricati. Vuoto se l'unione non c'è o se questo
    /// documento è l'unico che si sa disegnare.
    /// </summary>
    /// <param name="cicloOspite">Il ciclo AIRAC che l'ospite sta mostrando. ⚠️ Serve all'anteprima di
    /// release: <c>?as=rel:{id}</c> nomina <b>una</b> release, quella dell'ospite, e gli altri membri devono
    /// mostrare la <b>propria</b> dello <b>stesso ciclo</b> — altrimenti la pagina unita metterebbe insieme
    /// due fotografie di due momenti diversi e non lo direbbe a nessuno.</param>
    public async Task<IReadOnlyList<MembroUnito>> AltriMembriAsync(
        UnionView unione, int documentoOspite, PreviewMode mode, string? vista, string? cicloOspite,
        ReadingLanguageContext? linguaDelCircuito = null, CancellationToken ct = default)
    {
        var altri = unione.Members.Where(m => m.DocumentId != documentoOspite).ToList();
        if (altri.Count == 0) return Array.Empty<MembroUnito>();

        var caricati = new List<MembroUnito>(altri.Count);
        // ⚠️ In SEQUENZA, mai in parallelo: i caricatori interrogano il database, e due catene sullo stesso
        // DbContext danno «A second operation was started on this context instance». È la stessa ragione per
        // cui il guscio degli editor ha un tornello.
        foreach (var m in altri)
        {
            var suo = await ModalitaDelMembroAsync(m, mode, cicloOspite, ct).ConfigureAwait(false);
            if (await CaricaAsync(m, suo, vista, linguaDelCircuito, ct).ConfigureAwait(false) is { } caricato)
                caricati.Add(caricato);
        }
        return caricati;
    }

    /// <summary>
    /// Carica un membro. 🔴 <c>fissaLaPagina: false</c> su tutti e tre, ed è la riga che tiene insieme una
    /// pagina con DUE documenti: un documento a lingua <b>bloccata</b> chiama
    /// <c>ReadingLanguageContext.Fissa</c>, che non ha un blocco che lo chiuda e vale per il resto della
    /// richiesta. Con N membri, l'ULTIMO caricato che avesse la lingua bloccata deciderebbe la lingua delle
    /// etichette e della prosa generata di <b>tutta</b> la pagina — ospite compreso — e la deciderebbe in
    /// base all'ordine di caricamento. Nell'unione la lingua della pagina è quella dell'OSPITE.
    ///
    /// <para>⚠️ Il <i>contenuto</i> del membro resta nella sua lingua: traduzione, titoli di catalogo e
    /// derivate ricevono il codice come argomento, non dal contesto.</para>
    /// </summary>
    private async Task<MembroUnito?> CaricaAsync(UnionMemberView m, PreviewMode mode, string? vista,
                                                 ReadingLanguageContext? linguaDelCircuito, CancellationToken ct)
    {
        switch (m.Doc.ReleaseTarget)
        {
            case ReleaseTargetType.App:
            {
                var doc = await _sp.GetRequiredService<AppMemberLoader>()
                                   .LoadAsync(m.Doc.ReleaseKey, mode, vista, fissaLaPagina: false, ct)
                                   .ConfigureAwait(false);
                return doc is null ? null : new MembroUnito(m, doc.DisplayName, doc.View.Sections,
                    b => { b.OpenComponent<AppDocumentBody>(0); b.AddComponentParameter(1, nameof(AppDocumentBody.Doc), doc); b.CloseComponent(); });
            }
            case ReleaseTargetType.Airport:
            {
                var loader = ActivatorUtilities.CreateInstance<AirportMemberLoader>(_sp);
                var doc = await loader.LoadAsync(m.Doc.ReleaseKey, mode, vista, linguaDelCircuito,
                                                 fissaLaPagina: false, ct).ConfigureAwait(false);
                return doc is null ? null : new MembroUnito(m, doc.View.Title, doc.Sezioni,
                    b => { b.OpenComponent<AirportDocumentBody>(0); b.AddComponentParameter(1, nameof(AirportDocumentBody.Doc), doc); b.CloseComponent(); });
            }
            case ReleaseTargetType.AirportMil:
            {
                var loader = ActivatorUtilities.CreateInstance<MilMemberLoader>(_sp);
                var doc = await loader.LoadAsync(m.Doc.ReleaseKey, mode, vista, linguaDelCircuito,
                                                 fissaLaPagina: false, ct).ConfigureAwait(false);
                return doc is null ? null : new MembroUnito(m, doc.View.Title, doc.View.Sections,
                    b => { b.OpenComponent<MilDocumentBody>(0); b.AddComponentParameter(1, nameof(MilDocumentBody.Doc), doc); b.CloseComponent(); });
            }
            // ⚠️ Nessun `default` che disegna un segnaposto: le famiglie ammesse le decide
            // `DocumentUnionService.FamiglieAmmesse`, e un membro di un'altra famiglia non può esistere in
            // archivio. Se ci finisse, sparire in silenzio è meglio di disegnare una scatola vuota che
            // nessuno saprebbe spiegare — e `TidyAsync` chiuderà l'unione se resta sotto i due membri.
            default:
                return null;
        }
    }

    /// <summary>
    /// In che modalità si mostra <b>questo</b> membro, dato quello che sta guardando l'ospite.
    ///
    /// <list type="bullet">
    /// <item><b>Pubblica</b> e <b>bozza</b> passano identiche: sono modi di guardare, non un puntamento.</item>
    /// <item><b>Release</b> no: l'id nomina la release <i>dell'ospite</i>, e
    /// <c>GetPreviewAsync</c> la rifiuta per costruzione a un altro bersaglio (⚠️ e fa bene: chi può
    /// pubblicare due APP potrebbe altrimenti mostrare la release dell'uno sotto l'indirizzo dell'altro).
    /// Si cerca la release <b>di questo membro</b> allo <b>stesso ciclo AIRAC</b>.</item>
    /// </list>
    ///
    /// <para>⚠️ Se quel ciclo per questo membro non esiste, si ricade sulla <b>pubblica</b>, che è la verità:
    /// «questo documento a quel ciclo non è stato pubblicato». Fingere una release vicina metterebbe in una
    /// pagina sola due fotografie di momenti diversi.</para>
    /// </summary>
    private async Task<PreviewMode> ModalitaDelMembroAsync(UnionMemberView m, PreviewMode mode,
                                                           string? cicloOspite, CancellationToken ct)
    {
        if (mode.Kind != PreviewKind.Release || string.IsNullOrWhiteSpace(cicloOspite)) return mode;

        var sue = await _releases.ListAsync(m.Doc.ReleaseTarget, m.Doc.ReleaseKey, ct).ConfigureAwait(false);
        // Fra due release dello stesso ciclo vince la piu' recente: e' la stessa regola con cui
        // RecomputeStatuses sceglie l'effettiva, e senza di essa si mostrerebbe una superata.
        var stessoCiclo = sue.Where(r => r.ReleaseAiracCycle == cicloOspite)
                             .OrderByDescending(r => r.VersionNumber)
                             .FirstOrDefault();
        return stessoCiclo is null ? default : new PreviewMode(PreviewKind.Release, stessoCiclo.Id);
    }
}
