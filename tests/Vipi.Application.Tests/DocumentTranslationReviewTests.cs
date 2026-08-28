using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il correttore delle traduzioni <b>dentro l'editor</b>: le frasi di QUESTO documento, con la loro resa
/// nella lingua di chi legge (carta bilingue §5).
///
/// <para>
/// ⚠️ Il Registro admin elenca le frasi di tutta la divisione, in ordine di quanto sono state riviste: è il
/// posto giusto per un giro di revisione, e quello sbagliato per chi ha appena scritto un documento e vuole
/// sapere come viene letto. Chi scrive è l'unico che sa se «riporta sottovento» è diventato «report
/// downwind» o «bring it back downwind».
/// </para>
/// </summary>
public class DocumentTranslationReviewTests
{
    /// <summary>La faccia stretta dell'editing: il correttore legge il documento e basta.</summary>
    private sealed class EditingFinto : IDocumentForReview
    {
        private readonly EditableDocument? _doc;
        public EditingFinto(EditableDocument? doc) => _doc = doc;
        public Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult(_doc);
    }

    private sealed class AuthzFinto : IEditAuthorizationService
    {
        public bool Negato { get; set; }
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 42;
        public string? CurrentName => "chi corregge";
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) =>
            Negato ? throw new EditNotAllowedException() : Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int userId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }

    private static EditableDocument Documento(Language? lingua, params EditableSection[] sezioni) => new()
    {
        DocumentId = 7,
        VersionId = 70,
        VersionNumber = 1,
        VersionStatus = DocumentStatus.Draft,
        Title = "vIPI — LIBC Crotone",
        Sections = sezioni,
        Language = lingua,
    };

    private static EditableSection Sezione(string titolo, string? corpo = null, params EditableSection[] figlie) => new()
    {
        Id = 1, Title = titolo, SectionKey = "custom:abc", Depth = 0, Order = 1,
        Blocks = corpo is null
            ? Array.Empty<EditableBlock>()
            : new[] { new EditableBlock { Id = 1, Order = 1, Format = BlockFormat.Prose,
                                        Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, Body = corpo } },
        Children = figlie,
    };

    private static DocumentTranslationReview Servizio(
        EditableDocument? doc, MemoriaDiTraduzioneFinta memoria, AuthzFinto? authz = null) =>
        new(new EditingFinto(doc), memoria, authz ?? new AuthzFinto());

    [Fact]
    public async Task Elenca_le_frasi_del_documento_con_la_loro_resa()
    {
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Regole piste", "Slope rules");
        var doc = Documento(Language.It, Sezione("Regole piste", "Contatta la torre."));

        var righe = await Servizio(doc, memoria).RigheAsync(7, "en");

        Assert.Equal(2, righe.Count);
        var titolo = righe.Single(r => r.Sorgente == "Regole piste");
        Assert.Equal("Slope rules", titolo.Tradotto);
        Assert.False(titolo.Riletta);
        // ⚠️ Chi corregge deve sapere DOVE sta la frase, o si trova un elenco di frasi sciolte.
        Assert.Equal("Regole piste", titolo.Dove);

        // Quel che non ha traduzione compare comunque, vuoto: è la cosa che chi scrive deve poter riempire.
        Assert.Equal("", righe.Single(r => r.Sorgente == "Contatta la torre.").Tradotto);
    }

    [Fact]
    public async Task Il_titolo_del_documento_non_e_fra_le_frasi()
    {
        // R4: il titolo è il NOME del documento e non si traduce. Metterlo qui vorrebbe dire offrirlo a chi
        // corregge, cioè invitarlo a fare una cosa che il viewer poi ignora.
        var doc = Documento(Language.It, Sezione("Separazioni"));

        var righe = await Servizio(doc, new MemoriaDiTraduzioneFinta()).RigheAsync(7, "en");

        Assert.DoesNotContain(righe, r => r.Sorgente == "vIPI — LIBC Crotone");
    }

    [Fact]
    public async Task Nella_lingua_del_documento_non_c_e_niente_da_rivedere()
    {
        var doc = Documento(Language.It, Sezione("Separazioni"));

        Assert.Empty(await Servizio(doc, new MemoriaDiTraduzioneFinta()).RigheAsync(7, "it"));
    }

    [Fact]
    public async Task Una_vLOA_si_rivede_in_ITALIANO()
    {
        // ⚠️ Il verso opposto, e non è un caso di scuola: la vLOA nasce in inglese, quindi la traduzione da
        // rivedere è quella italiana. La lingua sorgente la dice il documento, non la pagina.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Purpose", "Scopo");
        var doc = Documento(Language.En, Sezione("Purpose"));

        var inItaliano = await Servizio(doc, memoria).RigheAsync(7, "it");
        var inInglese = await Servizio(doc, memoria).RigheAsync(7, "en");

        Assert.Equal("Scopo", Assert.Single(inItaliano).Tradotto);
        Assert.Empty(inInglese);
    }

    [Fact]
    public async Task La_correzione_si_salva_come_UMANA()
    {
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Regole piste", "Slope rules");
        var doc = Documento(Language.It, Sezione("Regole piste"));

        await Servizio(doc, memoria).CorreggiAsync(7, "en", "Regole piste", "Runway rules");

        Assert.Equal(("it", "en", "Regole piste", "Runway rules", 42), memoria.UltimaCorrezione);
    }

    [Fact]
    public async Task Non_si_corregge_verso_la_lingua_in_cui_il_documento_e_scritto()
    {
        // Sarebbe una modifica del documento travestita da traduzione: il testo sorgente si cambia
        // nell'editor, e da lì passa per la release come ogni altra edit.
        var doc = Documento(Language.It, Sezione("Regole piste"));

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => Servizio(doc, new MemoriaDiTraduzioneFinta()).CorreggiAsync(7, "it", "Regole piste", "Altro"));
    }

    [Fact]
    public async Task Chi_non_puo_scrivere_il_documento_non_ne_corregge_la_traduzione()
    {
        // ⚠️ Il permesso è quello del DOCUMENTO: ridire in un'altra lingua quel che un documento afferma è
        // un atto editoriale su quel documento.
        var memoria = new MemoriaDiTraduzioneFinta();
        var doc = Documento(Language.It, Sezione("Regole piste"));

        await Assert.ThrowsAsync<EditNotAllowedException>(
            () => Servizio(doc, memoria, new AuthzFinto { Negato = true })
                .CorreggiAsync(7, "en", "Regole piste", "Runway rules"));

        Assert.Null(memoria.UltimaCorrezione);
    }

    [Fact]
    public async Task La_memoria_si_interroga_UNA_volta_sola_per_tutto_il_documento()
    {
        var memoria = new MemoriaDiTraduzioneFinta();
        var doc = Documento(Language.It,
            Sezione("Uno", "a", Sezione("Figlia", "b")),
            Sezione("Due", "c"));

        await Servizio(doc, memoria).RigheAsync(7, "en");

        Assert.Equal(1, memoria.Letture);
    }
}
