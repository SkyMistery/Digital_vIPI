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

        /// <summary>Quante volte il documento è stato caricato. È il conto che rende visibile la lettura doppia.</summary>
        public int Letture { get; private set; }

        public Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult(_doc);
        }
    }

    private sealed class AuthzFinto : IEditAuthorizationService
    {
        // «Negato» resta il nome che i test usano, ma ora dice un LIVELLO: chi non arriva a Editor non
        // corregge una traduzione, come non scrive il documento.
        public bool Negato { get; set; }
        public VipiRole Role => Negato ? VipiRole.DivisionStaff : VipiRole.Admin;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 42;
        public string? CurrentName => "chi corregge";
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

    // ⚠️ Il tipo di ritorno è l'INTERFACCIA, non la classe: `RigheAsync` ha un'implementazione di default
    // (è `RevisioneAsync` privata della lingua sorgente), e le implementazioni di default si vedono solo
    // attraverso l'interfaccia. Chiamarla sulla classe concreta non compila.
    private static IDocumentTranslationReview Servizio(
        EditableDocument? doc, MemoriaDiTraduzioneFinta memoria, AuthzFinto? authz = null) =>
        new DocumentTranslationReview(new EditingFinto(doc), memoria, authz ?? new AuthzFinto());

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

    // ────────────────────────────────────────────────────────────────────────────────────────────────
    // 31 agosto 2026 — la lettura era DOPPIA, e il vuoto si leggeva per esclusione
    // ────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Il documento si carica <b>una volta sola</b> per risposta.
    ///
    /// <para>⚠️ Fino al 31 agosto 2026 il pannello dell'editor chiamava <c>RigheAsync</c>, e quando tornava
    /// vuoto la richiamava con l'<b>altra</b> lingua per capire se il vuoto significasse «stessa lingua»:
    /// due <c>LoadForEditAsync</c> — cioè due letture del documento intero — a ogni ridisegno dell'editor,
    /// su un pannello che è chiuso di suo. Questo test è il presidio di quel conto.</para>
    /// </summary>
    [Fact]
    public async Task La_revisione_carica_il_documento_una_volta_sola()
    {
        var doc = Documento(Language.It, Sezione("Regole piste", "Contatta la torre."));
        var editing = new EditingFinto(doc);
        var servizio = new DocumentTranslationReview(editing, new MemoriaDiTraduzioneFinta(), new AuthzFinto());

        await servizio.RevisioneAsync(7, "en");

        Assert.Equal(1, editing.Letture);
    }

    /// <summary>La lingua sorgente si <b>dice</b>, non si deduce da un secondo giro.</summary>
    [Fact]
    public async Task La_revisione_dice_in_che_lingua_e_scritto_il_documento()
    {
        var doc = Documento(Language.It, Sezione("Regole piste", "Contatta la torre."));
        var servizio = Servizio(doc, new MemoriaDiTraduzioneFinta());

        var inInglese = await servizio.RevisioneAsync(7, "en");
        Assert.Equal("it", inInglese.LinguaSorgente);
        Assert.False(inInglese.StessaLingua("en"));
        Assert.NotEmpty(inInglese.Righe);

        var inItaliano = await servizio.RevisioneAsync(7, "it");
        Assert.True(inItaliano.StessaLingua("it"));
        Assert.Empty(inItaliano.Righe);
    }

    /// <summary>
    /// ⚠️ Il caso che la vecchia deduzione sbagliava: un documento <b>senza niente da tradurre</b>. Lì
    /// nessuna delle due lingue ha righe, quindi «se l'altra lingua ne ha, allora è stessa lingua»
    /// rispondeva <b>no</b> anche a chi stava leggendo proprio nella lingua del documento.
    /// </summary>
    [Fact]
    public async Task Un_documento_senza_frasi_dice_lo_stesso_la_sua_lingua()
    {
        var doc = Documento(Language.It);   // nessuna sezione: niente da tradurre in nessuna lingua
        var servizio = Servizio(doc, new MemoriaDiTraduzioneFinta());

        Assert.True((await servizio.RevisioneAsync(7, "it")).StessaLingua("it"));
        Assert.False((await servizio.RevisioneAsync(7, "en")).StessaLingua("en"));
    }
}
