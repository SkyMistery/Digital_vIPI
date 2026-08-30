using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La sostituzione di un allegato e quel che ne consegue.
///
/// <para>Il punto di questa slice non è scrivere una versione nuova — quello lo fa la biblioteca in tre
/// righe — ma <b>lasciare una traccia</b>: il link segue sempre la versione corrente, quindi un documento
/// già <b>pubblicato</b> mostra il file nuovo <i>senza che nessuno l'abbia toccato</i>. Chi lo cura deve
/// saperlo, e queste prove presidiano proprio quello.</para>
/// </summary>
public class AttachmentCurationServiceTests
{
    // ---- impalcatura -----------------------------------------------------------------------------------

    private sealed class BibliotecaFinta : IAttachmentLibrary
    {
        private readonly AttachmentReplace _esito;
        public BibliotecaFinta(AttachmentReplace esito = AttachmentReplace.Ok) => _esito = esito;


        public string? Slug { get; private set; }
        public string? Link { get; private set; }
        public string? Nota { get; private set; }

        public Task<(AttachmentReplace Esito, AttachmentRow? Riga)> ReplaceAsync(
            string slug, string link, string? note, int userId, CancellationToken ct = default)
        {
            Slug = slug; Link = link; Nota = note;

            var riga = _esito == AttachmentReplace.Ok
                ? new AttachmentRow(1, slug, "LoA Roma-Marseille", AttachmentKind.Loa, AttachmentScope.Division,
                    null, null, 3, 3, AttachmentProvider.Drive, "AAAAAAAAAAAAAA",
                    DateTime.UnixEpoch, DateTime.UnixEpoch)
                : null;
            return Task.FromResult((_esito, riga));
        }

        public Task<IReadOnlyList<AttachmentRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AttachmentRow>>(Array.Empty<AttachmentRow>());
        public Task<AttachmentRow?> BySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult<AttachmentRow?>(null);
        public Task<(AttachmentCreate Esito, AttachmentRow? Riga)> CreateAsync(
            AttachmentDraft draft, int userId, CancellationToken ct = default) => throw new NotSupportedException();

        public AttachmentDelete EsitoDelete { get; init; } = AttachmentDelete.Ok;
        public string? Eliminato { get; private set; }

        public Task<AttachmentDelete> DeleteAsync(string slug, int userId, CancellationToken ct = default)
        {
            Eliminato = slug;
            return Task.FromResult(EsitoDelete);
        }
    }

    private sealed class UsoFinto : IAttachmentUsage
    {
        private readonly AttachmentCitation[] _citazioni;
        public UsoFinto(params AttachmentCitation[] citazioni) => _citazioni = citazioni;

        /// <summary>Quando è stato letto l'elenco: serve a dimostrare che si legge PRIMA di scrivere.</summary>
        public int Letture { get; private set; }

        public Task<IReadOnlyDictionary<string, AttachmentUsage>> AllAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AttachmentCitation>> WhereUsedAsync(string slug, CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult<IReadOnlyList<AttachmentCitation>>(_citazioni);
        }
    }

    private sealed class ImpattiFinti : IDocumentImpactService
    {
        public List<(ImpactKind Kind, int[] Documenti, string SourceKey, string[] Args)> Aperti { get; } = new();

        public Task<int> RaiseForDocumentsAsync(ImpactKind kind, IReadOnlyCollection<int> documentIds,
            string sourceKey, IReadOnlyList<string> args, CancellationToken ct = default)
        {
            Aperti.Add((kind, documentIds.ToArray(), sourceKey, args.ToArray()));
            return Task.FromResult(documentIds.Count);
        }

        public Task<int> RaiseForSectorAsync(ImpactKind kind, string composePosition, string accCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RaiseImpactInput>> PrepareForSectorAsync(ImpactKind kind, string composePosition, string accCode, IReadOnlyList<string> args, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> RaiseForAreaAsync(ImpactKind kind, string ivaoId, string areaName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> ClearBySourceAsync(IReadOnlyCollection<ImpactKind> kinds, string sourceKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentImpactRow>> ListOpenAsync(int documentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> ListOpenByKindCountAsync(ImpactKind kind, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, ImpactBadge>> CountOpenAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ClearAsync(int impactId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(int Aperti, int Chiusi)> ReconcileAsync(ImpactKind kind, IReadOnlyCollection<RaiseImpactInput> attuali, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PruneClearedBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static AttachmentCitation Cita(string titolo, int? documentId, bool pubblicato = true) =>
        new(AttachmentCitationSource.Release, titolo, null, pubblicato, pubblicato ? "2609" : null, documentId);

    // ---- i casi ----------------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ La riga «da rivedere» si apre <b>anche se non c'è niente di rotto</b>, ed è il punto: la copia
    /// pubblicata mostra già il file nuovo, e chi cura quel documento deve saperlo invece di scoprirlo.
    /// </summary>
    [Fact]
    public async Task Ogni_documento_che_la_cita_riceve_la_riga_da_rivedere()
    {
        var impatti = new ImpattiFinti();
        var servizio = new AttachmentCurationService(
            new BibliotecaFinta(), new UsoFinto(Cita("vIPI Fiumicino", 7), Cita("vLOA LIRR-LFMM", 9)), impatti);

        await servizio.ReplaceAsync("loa-lirr-lfmm", "https://drive.google.com/file/d/AAAAAAAAAAAA/view", null, 704798);

        var aperto = Assert.Single(impatti.Aperti);
        Assert.Equal(ImpactKind.AttachmentReplaced, aperto.Kind);
        Assert.Equal(new[] { 7, 9 }, aperto.Documenti);
        // L'origine è lo SLUG: due sostituzioni della stessa voce non fanno due righe sullo stesso documento.
        Assert.Equal("loa-lirr-lfmm", aperto.SourceKey);
        // L'argomento della frase è il TITOLO, che è quel che chi legge riconosce.
        Assert.Equal(new[] { "LoA Roma-Marseille" }, aperto.Args);
    }

    /// <summary>Lo stesso documento che la cita in due punti riceve <b>una</b> riga: la casella deduplica su
    /// (documento, tipo, origine), ma passargli due volte lo stesso id sarebbe comunque un doppione.</summary>
    [Fact]
    public async Task Un_documento_citato_due_volte_riceve_una_riga_sola()
    {
        var impatti = new ImpattiFinti();
        var servizio = new AttachmentCurationService(
            new BibliotecaFinta(), new UsoFinto(Cita("vIPI Fiumicino", 7), Cita("vIPI Fiumicino", 7)), impatti);

        await servizio.ReplaceAsync("loa-lirr-lfmm", "https://drive.google.com/file/d/AAAAAAAAAAAA/view", null, 1);

        Assert.Equal(new[] { 7 }, Assert.Single(impatti.Aperti).Documenti);
    }

    /// <summary>Un posto che non appartiene a un documento — un blocco condiviso, una sezione extra — non
    /// produce righe: non c'è un documento a cui attaccarle.</summary>
    [Fact]
    public async Task Una_citazione_senza_documento_non_apre_niente()
    {
        var impatti = new ImpattiFinti();
        var servizio = new AttachmentCurationService(
            new BibliotecaFinta(), new UsoFinto(Cita("minime-generali", null)), impatti);

        await servizio.ReplaceAsync("loa-lirr-lfmm", "https://drive.google.com/file/d/AAAAAAAAAAAA/view", null, 1);

        Assert.Empty(impatti.Aperti);
    }

    [Fact]
    public async Task Se_non_la_cita_nessuno_non_si_apre_niente()
    {
        var impatti = new ImpattiFinti();
        var servizio = new AttachmentCurationService(new BibliotecaFinta(), new UsoFinto(), impatti);

        var esito = await servizio.ReplaceAsync("loa-lirr-lfmm", "https://drive.google.com/file/d/AAAAAAAAAAAA/view", null, 1);

        Assert.Equal(AttachmentReplace.Ok, esito.Esito);
        Assert.Empty(impatti.Aperti);
    }

    /// <summary>
    /// ⚠️ Se la sostituzione <b>non</b> è andata a buon fine non si segnala niente. Il caso che conta è
    /// <c>Invariato</c>: rimettere lo stesso file non è un errore, ma mandare delle persone a rileggere un
    /// documento che non è cambiato è peggio di non dire niente.
    /// </summary>
    [Theory]
    [InlineData(AttachmentReplace.Invariato)]
    [InlineData(AttachmentReplace.LinkNonValido)]
    [InlineData(AttachmentReplace.NonTrovata)]
    public async Task Una_sostituzione_non_riuscita_non_segnala_niente(AttachmentReplace esito)
    {
        var impatti = new ImpattiFinti();
        var servizio = new AttachmentCurationService(
            new BibliotecaFinta(esito), new UsoFinto(Cita("vIPI Fiumicino", 7)), impatti);

        var risultato = await servizio.ReplaceAsync("loa-lirr-lfmm", "qualunque", null, 1);

        Assert.Equal(esito, risultato.Esito);
        Assert.Empty(impatti.Aperti);
    }

    /// <summary>L'anteprima è la stessa lettura della conferma: quel che la schermata mostra è quel che poi
    /// riceve la segnalazione.</summary>
    [Fact]
    public async Task Lanteprima_dice_chi_cambia()
    {
        var servizio = new AttachmentCurationService(
            new BibliotecaFinta(), new UsoFinto(Cita("vIPI Fiumicino", 7)), new ImpattiFinti());

        var citazioni = await servizio.ImpactPreviewAsync("loa-lirr-lfmm");

        Assert.Equal("vIPI Fiumicino", Assert.Single(citazioni).Title);
    }

    /// <summary>Il link e la nota arrivano alla biblioteca come li ha scritti chi conferma: qui non si
    /// normalizza niente, o ci sarebbero due posti che decidono che cos'è un link valido.</summary>
    [Fact]
    public async Task Link_e_nota_arrivano_alla_biblioteca()
    {
        var biblioteca = new BibliotecaFinta();
        var servizio = new AttachmentCurationService(biblioteca, new UsoFinto(), new ImpattiFinti());

        await servizio.ReplaceAsync("loa-lirr-lfmm", "https://drive.google.com/file/d/AAAAAAAAAAAA/view",
            "rifirmata dopo modifica CoP", 704798);

        Assert.Equal("loa-lirr-lfmm", biblioteca.Slug);
        Assert.Equal("https://drive.google.com/file/d/AAAAAAAAAAAA/view", biblioteca.Link);
        Assert.Equal("rifirmata dopo modifica CoP", biblioteca.Nota);
    }

    // ---- eliminazione ----------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ <b>Eliminare una voce citata non si rifiuta</b>, e la scelta è deliberata: rifiutare avrebbe senso
    /// se ci fosse un modo automatico di rimediare, e non c'è — le citazioni stanno dentro testo scritto da
    /// persone. Quel che si può garantire è che quei documenti non restino col link morto <i>in silenzio</i>.
    /// </summary>
    [Fact]
    public async Task Eliminare_una_voce_citata_segnala_i_documenti_rimasti_col_link_morto()
    {
        var impatti = new ImpattiFinti();
        var servizio = new AttachmentCurationService(
            new BibliotecaFinta(), new UsoFinto(Cita("vIPI Fiumicino", 7), Cita("vLOA LIRR-LFMM", 9)), impatti);

        var esito = await servizio.DeleteAsync("loa-lirr-lfmm", 704798);

        Assert.Equal(AttachmentDelete.Ok, esito.Esito);
        var aperto = Assert.Single(impatti.Aperti);
        // ⚠️ Un tipo suo, non AttachmentReplaced: là il link funziona e mostra un file diverso, qui è MORTO.
        Assert.Equal(ImpactKind.AttachmentDeleted, aperto.Kind);
        Assert.Equal(new[] { 7, 9 }, aperto.Documenti);
        Assert.Equal("loa-lirr-lfmm", aperto.SourceKey);
    }

    /// <summary>Gli orfani tornano al chiamante: sono quelli che la conferma aveva mostrato, e il messaggio
    /// finale li conta.</summary>
    [Fact]
    public async Task Leliminazione_torna_chi_resta_da_correggere()
    {
        var servizio = new AttachmentCurationService(
            new BibliotecaFinta(), new UsoFinto(Cita("vIPI Fiumicino", 7)), new ImpattiFinti());

        var esito = await servizio.DeleteAsync("loa-lirr-lfmm", 1);

        Assert.Equal("vIPI Fiumicino", Assert.Single(esito.Orfani).Title);
    }

    [Fact]
    public async Task Eliminare_una_voce_che_non_cita_nessuno_non_segnala_niente()
    {
        var impatti = new ImpattiFinti();
        var servizio = new AttachmentCurationService(new BibliotecaFinta(), new UsoFinto(), impatti);

        Assert.Equal(AttachmentDelete.Ok, (await servizio.DeleteAsync("loa-lirr-lfmm", 1)).Esito);
        Assert.Empty(impatti.Aperti);
    }

    /// <summary>Se la voce non c'era più, non si segnala niente: il link morto ce l'ha già fatto qualcun altro,
    /// e una riga in più direbbe che è successo adesso.</summary>
    [Fact]
    public async Task Se_la_voce_non_ce_piu_non_si_segnala_niente()
    {
        var impatti = new ImpattiFinti();
        var servizio = new AttachmentCurationService(
            new BibliotecaFinta { EsitoDelete = AttachmentDelete.NonTrovata },
            new UsoFinto(Cita("vIPI Fiumicino", 7)), impatti);

        var esito = await servizio.DeleteAsync("loa-lirr-lfmm", 1);

        Assert.Equal(AttachmentDelete.NonTrovata, esito.Esito);
        Assert.Empty(impatti.Aperti);
    }

    /// <summary>
    /// ⚠️ Chi cita si legge <b>una volta sola</b>, prima di scrivere. Rileggere dopo vorrebbe dire che un
    /// salvataggio in un'altra scheda cambia l'elenco fra la conferma e la segnalazione: chi ha premuto
    /// avrebbe deciso su un elenco, e la traccia resterebbe su un altro.
    /// </summary>
    [Fact]
    public async Task Chi_cita_si_legge_una_volta_sola_e_prima_di_scrivere()
    {
        var uso = new UsoFinto(Cita("vIPI Fiumicino", 7));
        var servizio = new AttachmentCurationService(new BibliotecaFinta(), uso, new ImpattiFinti());

        await servizio.ReplaceAsync("loa-lirr-lfmm", "https://drive.google.com/file/d/AAAAAAAAAAAA/view", null, 1);

        Assert.Equal(1, uso.Letture);
    }
}
