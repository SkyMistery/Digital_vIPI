using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// «Chi cita questa voce di biblioteca»: la lettura da cui dipendono la guardia alla cancellazione e la
/// conferma di sostituzione.
///
/// <para>Presidia le quattro cose che a occhio non si vedono: una citazione dentro una <b>release
/// pubblicata</b> ha un nome e un link anche se la release non porta un id di documento; lo stesso documento
/// che cita dieci volte è <b>una</b> riga; il pubblicato viene <b>prima</b>; e senza nessun riferimento non si
/// legge nemmeno l'elenco dei documenti.</para>
/// </summary>
public class AttachmentUsageServiceTests
{
    // ---- impalcatura -----------------------------------------------------------------------------------

    private sealed class TestiFinti : IAttachmentTextSource
    {
        private readonly AttachmentText[] _testi;
        public TestiFinti(params AttachmentText[] testi) => _testi = testi;

        public Task<IReadOnlyList<AttachmentText>> ReadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AttachmentText>>(_testi);
    }

    private sealed class DocumentiFinti : IDocumentAdminRepository
    {
        private readonly ManagedDoc[] _docs;
        public DocumentiFinti(params ManagedDoc[] docs) => _docs = docs;

        /// <summary>Quante volte l'elenco è stato letto: è il conto che difende «niente query se non serve».</summary>
        public int Letture { get; private set; }

        public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult<IReadOnlyList<ManagedDoc>>(_docs);
        }

        public Task<IReadOnlyDictionary<int, ManagedDoc>> DescribeAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, ManagedDoc>>(
                _docs.Where(d => d.DocumentId is not null && documentIds.Contains(d.DocumentId.Value))
                     .ToDictionary(d => d.DocumentId!.Value));

        public Task<IReadOnlyDictionary<int, string>> GetTitlesAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DocumentLanguageState?> GetLanguageAsync(ManagedDocRef doc, CancellationToken ct = default) =>
            Task.FromResult<DocumentLanguageState?>(null);
        public Task SetLanguageAsync(ManagedDocRef doc, Vipi.Domain.Language language, bool locked, int actorUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task SetHiddenAsync(ManagedDocRef doc, bool hidden, int actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(ManagedDocRef doc, int actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static IDocRoutesRegistry Rotte() => new DocRoutesRegistry(new IDocKindRoutes[]
    {
        new AccVipiDocRoutes(), new AirportDocRoutes(), new AppDocRoutes(), new VloaDocRoutes(),
    });

    private static ManagedDoc Doc(string titolo, string chiave, int? id, bool pubblicato = false,
        string? ciclo = null, string acc = "LIRR",
        ReleaseTargetType tipo = ReleaseTargetType.Airport) =>
        new(tipo, titolo, chiave, acc, IsPublished: pubblicato, HasDraft: false, IsHidden: false,
            tipo, chiave, id, null, EffectiveCycle: ciclo);

    private static AttachmentUsageService Servizio(IAttachmentTextSource testi, params ManagedDoc[] docs) =>
        new(testi, new DocumentiFinti(docs), Rotte());

    // ---- i casi ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Una_citazione_in_un_blocco_porta_il_titolo_del_documento()
    {
        var uso = await Servizio(
            new TestiFinti(new AttachmentText("allegato:loa-lirr-lfmm", AttachmentCitationSource.Document, 7)),
            Doc("vIPI Fiumicino", "LIRF", 7)).AllAsync();

        var citazioni = Assert.Contains("loa-lirr-lfmm", (IDictionary<string, AttachmentUsage>)uso).Citations;
        Assert.Equal("vIPI Fiumicino", Assert.Single(citazioni).Title);
    }

    /// <summary>
    /// ⚠️ Una release <b>non porta un DocumentId</b>: si identifica con la coppia (tipo, chiave). Se il
    /// servizio cercasse solo per id, ogni citazione dentro un documento pubblicato — cioè quella che il
    /// lettore sta guardando adesso — resterebbe senza nome e senza link.
    /// </summary>
    [Fact]
    public async Task Una_citazione_in_una_release_si_attribuisce_per_tipo_e_chiave()
    {
        var uso = await Servizio(
            new TestiFinti(new AttachmentText("allegato:loa-lirr-lfmm", AttachmentCitationSource.Release,
                null, null, ReleaseTargetType.Airport, "LIRF")),
            Doc("vIPI Fiumicino", "LIRF", 7, pubblicato: true, ciclo: "2609")).AllAsync();

        var c = Assert.Single(uso["loa-lirr-lfmm"].Citations);
        Assert.Equal("vIPI Fiumicino", c.Title);
        Assert.Equal("2609", c.EffectiveCycle);
        Assert.True(c.IsPublished);
        Assert.False(string.IsNullOrEmpty(c.Url));
    }

    /// <summary>Lo stesso documento che lo cita in dieci blocchi è <b>una</b> riga: a chi decide interessa
    /// quali documenti cambiano, non quante volte. Dieci righe uguali renderebbero illeggibile proprio la
    /// schermata che esiste per far decidere.</summary>
    [Fact]
    public async Task Lo_stesso_documento_che_cita_dieci_volte_e_una_riga_sola()
    {
        var testi = Enumerable.Range(0, 10)
            .Select(_ => new AttachmentText("allegato:loa-lirr-lfmm", AttachmentCitationSource.Document, 7))
            .ToArray();

        var uso = await Servizio(new TestiFinti(testi), Doc("vIPI Fiumicino", "LIRF", 7)).AllAsync();

        Assert.Single(uso["loa-lirr-lfmm"].Citations);
    }

    /// <summary>Il pubblicato prima: è quel che il lettore vede adesso, quindi quel che pesa sulla decisione.
    /// Una bozza si corregge prima di pubblicarla.</summary>
    [Fact]
    public async Task Le_citazioni_pubblicate_vengono_prima()
    {
        var uso = await Servizio(new TestiFinti(
                new AttachmentText("allegato:x-y", AttachmentCitationSource.Document, 1),
                new AttachmentText("allegato:x-y", AttachmentCitationSource.Release,
                    null, null, ReleaseTargetType.Airport, "LIRF")),
            Doc("Bozza", "LIME", 1),
            Doc("Pubblicato", "LIRF", 7, pubblicato: true, ciclo: "2609")).AllAsync();

        var citazioni = uso["x-y"].Citations;
        Assert.Equal("Pubblicato", citazioni[0].Title);
        Assert.Equal("Bozza", citazioni[1].Title);
    }

    /// <summary>Un posto che non appartiene a un documento — una sezione extra, un blocco condiviso — porta
    /// almeno la sua etichetta: una riga senza nome non si può né capire né andare a correggere.</summary>
    [Fact]
    public async Task Cio_che_non_ha_un_documento_porta_la_sua_etichetta()
    {
        var uso = await Servizio(new TestiFinti(
            new AttachmentText("allegato:x-y", AttachmentCitationSource.SharedBlock, null, "minime-generali"))).AllAsync();

        var c = Assert.Single(uso["x-y"].Citations);
        Assert.Equal("minime-generali", c.Title);
        Assert.Null(c.Url);
    }

    /// <summary>
    /// ⚠️ Senza nemmeno un riferimento non si legge l'elenco dei documenti. È il caso normale finché la
    /// biblioteca è nuova, ed è anche quello in cui la pagina si apre più spesso: una query in meno per ogni
    /// apertura, e nessun rischio di sfiorare il <c>DbContext</c> del circuito mentre la pagina rende.
    /// </summary>
    [Fact]
    public async Task Senza_riferimenti_non_si_disturba_lelenco_dei_documenti()
    {
        var documenti = new DocumentiFinti(Doc("vIPI Fiumicino", "LIRF", 7));
        var servizio = new AttachmentUsageService(
            new TestiFinti(new AttachmentText("nessun riferimento", AttachmentCitationSource.Document, 7)),
            documenti, Rotte());

        Assert.Empty(await servizio.AllAsync());
        Assert.Equal(0, documenti.Letture);
    }

    [Fact]
    public async Task Dove_usato_torna_le_citazioni_di_quello_slug()
    {
        var servizio = Servizio(new TestiFinti(
                new AttachmentText("allegato:loa-lirr-lfmm", AttachmentCitationSource.Document, 7),
                new AttachmentText("allegato:circolare-01", AttachmentCitationSource.Document, 7)),
            Doc("vIPI Fiumicino", "LIRF", 7));

        Assert.Single(await servizio.WhereUsedAsync("loa-lirr-lfmm"));
        Assert.Empty(await servizio.WhereUsedAsync("mai-citata"));
    }

    [Fact]
    public async Task Dove_usato_non_bada_a_spazi_e_maiuscole()
    {
        var servizio = Servizio(
            new TestiFinti(new AttachmentText("allegato:loa-lirr-lfmm", AttachmentCitationSource.Document, 7)),
            Doc("vIPI Fiumicino", "LIRF", 7));

        Assert.Single(await servizio.WhereUsedAsync("  LOA-LIRR-LFMM  "));
    }
}
