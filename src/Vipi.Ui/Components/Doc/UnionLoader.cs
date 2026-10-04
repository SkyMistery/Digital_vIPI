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
/// <param name="HaMarcate">Vero se <b>questo</b> membro ha almeno una sezione marcata pilota/ATC.
/// <para>⚠️ Esce di qui perché la chip «Tutto · Pilota · ATC» è <b>una sola per pagina</b> e la disegna il
/// documento della PORTA: senza questo campo la domanda «c'è qualcosa da filtrare?» se la faceva lui e basta,
/// e un documento con le sezioni marcate finito in seconda posizione perdeva il selettore — il filtro
/// continuava a funzionare da URL (<c>?vista=</c> arriva ai membri), ma nessuno poteva più chiederlo con un
/// clic.</para></param>
/// <param name="Bloccata">La lingua in cui <b>questo</b> membro è bloccato, o null se segue chi legge.
/// <para>⚠️ Esce di qui per la stessa ragione di <c>HaMarcate</c>: il gettone «solo in inglese» della testata
/// è della porta, e un membro bloccato sotto una porta bilingue non lo diceva a nessuno.</para></param>
public sealed record MembroUnito(
    UnionMemberView Membro,
    string Titolo,
    IReadOnlyList<SectionView> Sezioni,
    bool HaMarcate,
    string? Bloccata,
    RenderFragment Corpo)
{
    /// <summary>L'ancora del gruppo di questo membro: è dove atterra chi arriva dalla sua vecchia URL.</summary>
    public string Ancora => AncoraDi(Membro.DocumentId);

    /// <summary>L'ancora del gruppo di un documento dentro una pagina unita.
    /// <para>⚠️ Sull'ID DEL DOCUMENTO e non sulla posizione: l'ordine dei membri si cambia con due frecce, e
    /// un'ancora che cambia insieme all'ordine è un collegamento salvato che un giorno porta altrove.</para></summary>
    public static string AncoraDi(int documentId) => $"doc-{documentId}";

    /// <summary>
    /// Se la chip «Tutto · Pilota · ATC» ha senso su <b>questa pagina</b>: la domanda è dell'unione intera,
    /// non del solo documento della porta.
    /// <para>⚠️ La chip è UNA per pagina e il filtro si applica a tutto ciò che la pagina disegna, membri
    /// compresi. Chiederlo al solo documento della porta faceva sparire il selettore quando le sezioni
    /// marcate stavano su un membro — e un documento che le aveva le perdeva unendosi, senza nessun errore.</para>
    /// <para>Sta qui e non nelle tre pagine perché i chiamanti sono tre: la stessa condizione scritta tre
    /// volte è la prima a divergere.</para>
    /// </summary>
    public static bool QualcunoHaMarcate(bool dellaPorta, IReadOnlyList<MembroUnito> altri) =>
        dellaPorta || altri.Any(a => a.HaMarcate);
}

/// <summary>
/// Il caricamento dei membri di un'unione di documenti (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §3): data l'unione e il documento della <b>porta</b> da
/// cui si è entrati, prepara gli <b>altri</b> — ognuno col caricatore della sua famiglia.
///
/// <para>⚠️ §13: la porta cambia da pagina a pagina, e con lei l'ordine. Chi chiama passa il <b>proprio</b>
/// documento, non un ospite deciso una volta per tutte: gli altri escono di qui nell'ordine memorizzato,
/// senza di lui. Nessuno reindirizza più a nessuno.</para>
///
/// <para>
/// ⚠️ <b>Si costruisce dallo scope di chi lo usa</b> (<c>ActivatorUtilities</c>) quando quella pagina ne ha
/// uno proprio: dentro istanzia i caricatori di famiglia, e uno di quelli — l'aeroporto — <b>deve</b> stare
/// sullo scope della pagina. Vedi il commento in testa a <see cref="AirportMemberLoader"/>.
/// </para>
/// </summary>
public sealed class UnionLoader
{
    /// <summary>
    /// Lo scope da cui si costruiscono i caricatori di famiglia.
    /// <para>⚠️ Tutti e tre con <c>ActivatorUtilities.CreateInstance</c>, e <b>non</b> con
    /// <c>GetRequiredService</c>: uno di loro — l'aeroporto — <b>non è registrato</b> di proposito, perché
    /// i nove servizi che interroga vanno presi dallo scope della pagina e non dal circuito. Un modo per
    /// famiglia è la strada per cui il quarto caricatore verrà preso dal posto sbagliato e nessuno se ne
    /// accorgerà: `CreateInstance` funziona sia che il tipo sia registrato sia che non lo sia.</para>
    /// </summary>
    private readonly IServiceProvider _sp;
    private readonly IReleaseService _releases;

    public UnionLoader(IServiceProvider sp, IReleaseService releases)
    {
        _sp = sp;
        _releases = releases;
    }

    // ⚠️ Qui stava `IndirizzoDellOspiteAsync`, il rimando della §4: la vista pubblica di un membro
    // non-ospite mandava alla pagina dell'ospite, ancorata al proprio gruppo. Cancellato dalla §13 — ogni
    // porta disegna l'unione da sé — e con lui se ne sono andati due problemi che erano SUOI: la guardia
    // «l'ospite deve avere qualcosa in pubblico» (un APP pubblicato spariva dal web sotto una vIPI in bozza)
    // e l'ACC da prendere all'ospite (due membri su ACC diversi davano un indirizzo che non esiste).
    // Con lui è uscito anche `IDocRoutesRegistry` dal costruttore: nessuno, qui dentro, calcola più un URL.

    /// <summary>
    /// Gli altri membri dell'unione, nell'ordine, già caricati. Vuoto se l'unione non c'è o se questo
    /// documento è l'unico che si sa disegnare.
    /// </summary>
    /// <param name="cicloDellaPorta">Il ciclo AIRAC che il documento della porta sta mostrando. ⚠️ Serve
    /// all'anteprima di release: <c>?as=rel:{id}</c> nomina <b>una</b> release, la sua, e gli altri membri
    /// devono mostrare la <b>propria</b> dello <b>stesso ciclo</b> — altrimenti la pagina unita metterebbe
    /// insieme due fotografie di due momenti diversi e non lo direbbe a nessuno.</param>
    public async Task<IReadOnlyList<MembroUnito>> AltriMembriAsync(
        UnionView unione, int documentoDellaPorta, PreviewMode mode, string? vista, string? cicloDellaPorta,
        ReadingLanguageContext? linguaDelCircuito = null, CancellationToken ct = default)
    {
        // ⚠️ La regola dell'ordine sta in `UnionView.AltriDa`, non qui: è pura, ed è pinnata porta per porta.
        var altri = unione.AltriDa(documentoDellaPorta);
        if (altri.Count == 0) return Array.Empty<MembroUnito>();

        var caricati = new List<MembroUnito>(altri.Count);
        // ⚠️ In SEQUENZA, mai in parallelo: i caricatori interrogano il database, e due catene sullo stesso
        // DbContext danno «A second operation was started on this context instance». È la stessa ragione per
        // cui il guscio degli editor ha un tornello.
        foreach (var m in altri)
        {
            var suo = await ModalitaDelMembroAsync(m, mode, cicloDellaPorta, ct).ConfigureAwait(false);
            if (await CaricaAsync(m, suo, vista, linguaDelCircuito, ct).ConfigureAwait(false) is { } caricato)
                caricati.Add(caricato);
        }
        return caricati;
    }

    /// <summary>
    /// Carica un membro <b>nella sua lingua</b>: ogni documento dell'unione tiene la propria regola — la vIPI
    /// di LIRP bilingue segue chi legge, il suo vSOP bloccato resta in inglese — qualunque sia la porta.
    ///
    /// <para>🔴 <b>Due tempi, due attrezzi.</b> Il <i>caricamento</i> (traduzione, titoli di catalogo, prosa
    /// generata, derivate) avviene qui, in sequenza: lo si chiude in un blocco
    /// <see cref="ReadingLanguageContext.Rendering"/> che parte dalla lingua di chi legge — così un membro
    /// bilingue sotto una porta bloccata non eredita la sua — e dentro il quale il membro bloccato fa il suo
    /// <c>Fissa</c>. Alla chiusura il blocco rimette la lingua della porta: la pagina resta sua. Il
    /// <i>disegno</i> invece avviene dopo, quando il blocco è chiuso da un pezzo: le etichette del corpo le
    /// porta la cascata <see cref="ComponenteDelDocumento.Cascata"/>, che vale solo sotto questo membro.</para>
    ///
    /// <para>⚠️ Fino al 1° ottobre 2026 i membri si caricavano senza imporre niente («la lingua della pagina
    /// è dell'ospite»): il testo di un membro bloccato restava suo, ma etichette, intestazioni e prosa
    /// generata seguivano la porta — e su LIRP il vSOP inglese aveva le tabelle in italiano.</para>
    /// </summary>
    private async Task<MembroUnito?> CaricaAsync(UnionMemberView m, PreviewMode mode, string? vista,
                                                 ReadingLanguageContext? linguaDelCircuito, CancellationToken ct)
    {
        var lettore = LinguaDiLettura.DelLettore();
        // ⚠️ I due contesti, se sono due: vedi `linguaDelCircuito` su AirportMemberLoader. Chiusi in ordine
        // inverso (`using var`), e se sono lo stesso oggetto l'annidamento li rimette a posto comunque.
        using var dellaPagina = _sp.GetRequiredService<ReadingLanguageContext>().Rendering(lettore);
        using var delCircuito = linguaDelCircuito?.Rendering(lettore);

        switch (m.Doc.ReleaseTarget)
        {
            case ReleaseTargetType.App:
            {
                var loader = ActivatorUtilities.CreateInstance<AppMemberLoader>(_sp);
                var doc = await loader.LoadAsync(m.Doc.ReleaseKey, mode, vista, ct).ConfigureAwait(false);
                return doc is null ? null : new MembroUnito(m, doc.DisplayName, doc.View.Sections, doc.HaMarcate,
                    doc.Bloccata, NellaSuaLingua<AppDocumentBody>(nameof(AppDocumentBody.Doc), doc, doc.Bloccata ?? lettore));
            }
            case ReleaseTargetType.Airport:
            {
                var loader = ActivatorUtilities.CreateInstance<AirportMemberLoader>(_sp);
                var doc = await loader.LoadAsync(m.Doc.ReleaseKey, mode, vista, linguaDelCircuito, ct)
                                      .ConfigureAwait(false);
                return doc is null ? null : new MembroUnito(m, doc.View.Title, doc.Sezioni, doc.HaMarcate,
                    doc.Bloccata, NellaSuaLingua<AirportDocumentBody>(nameof(AirportDocumentBody.Doc), doc, doc.Bloccata ?? lettore));
            }
            case ReleaseTargetType.AirportMil:
            {
                var loader = ActivatorUtilities.CreateInstance<MilMemberLoader>(_sp);
                var doc = await loader.LoadAsync(m.Doc.ReleaseKey, mode, vista, linguaDelCircuito, ct)
                                      .ConfigureAwait(false);
                return doc is null ? null : new MembroUnito(m, doc.View.Title, doc.View.Sections, doc.HaMarcate,
                    doc.Bloccata, NellaSuaLingua<MilDocumentBody>(nameof(MilDocumentBody.Doc), doc, doc.Bloccata ?? lettore));
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
    /// Il corpo di un membro dentro la cascata della sua lingua. Un posto solo per le tre famiglie: un corpo
    /// montato senza cascata non dà errore, si prende le etichette della porta.
    /// </summary>
    internal static RenderFragment NellaSuaLingua<TCorpo>(string parametro, object doc, string lingua)
        where TCorpo : IComponent => b =>
    {
        b.OpenComponent<CascadingValue<string>>(0);
        b.AddComponentParameter(1, nameof(CascadingValue<string>.Name), ComponenteDelDocumento.Cascata);
        b.AddComponentParameter(2, nameof(CascadingValue<string>.Value), lingua);
        b.AddComponentParameter(3, nameof(CascadingValue<string>.IsFixed), true);
        b.AddComponentParameter(4, nameof(CascadingValue<string>.ChildContent), (RenderFragment)(c =>
        {
            c.OpenComponent<TCorpo>(0);
            c.AddComponentParameter(1, parametro, doc);
            c.CloseComponent();
        }));
        b.CloseComponent();
    };

    /// <summary>
    /// In che modalità si mostra <b>questo</b> membro, dato quello che sta guardando il documento della porta.
    ///
    /// <list type="bullet">
    /// <item><b>Pubblica</b> e <b>bozza</b> passano identiche: sono modi di guardare, non un puntamento.</item>
    /// <item><b>Release</b> no: l'id nomina la release <i>del documento della porta</i>, e
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
                                                           string? cicloDellaPorta, CancellationToken ct)
    {
        if (mode.Kind != PreviewKind.Release || string.IsNullOrWhiteSpace(cicloDellaPorta)) return mode;

        var sue = await _releases.ListAsync(m.Doc.ReleaseTarget, m.Doc.ReleaseKey, ct).ConfigureAwait(false);
        // Fra due release dello stesso ciclo vince la piu' recente: e' la stessa regola con cui
        // RecomputeStatuses sceglie l'effettiva, e senza di essa si mostrerebbe una superata.
        var stessoCiclo = sue.Where(r => r.ReleaseAiracCycle == cicloDellaPorta)
                             .OrderByDescending(r => r.VersionNumber)
                             .FirstOrDefault();
        return stessoCiclo is null ? default : new PreviewMode(PreviewKind.Release, stessoCiclo.Id);
    }
}
