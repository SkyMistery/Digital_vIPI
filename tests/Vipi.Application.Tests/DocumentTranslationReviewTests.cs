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

        /// <summary>Editor ma non admin: scrive il documento, non il testo comune delle torri radar (S127).</summary>
        public bool SoloEditor { get; set; }
        public VipiRole Role => Negato ? VipiRole.DivisionStaff : SoloEditor ? VipiRole.Editor : VipiRole.Admin;
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
        EditableDocument? doc, MemoriaDiTraduzioneFinta memoria, AuthzFinto? authz = null,
        ITestoComuneStore? comune = null) =>
        new DocumentTranslationReview(new EditingFinto(doc), memoria, authz ?? new AuthzFinto(), comune);

    // ---- Il testo comune delle torri radar (S127) ------------------------------------------------------------------
    //
    // Il committente (22 ottobre 2026): «la parte comune di torre radar non sapevo dove si traducesse». La traduceva la
    // macchina, ma nessun pannello la mostrava: non sta nel documento. Ora la mostra il pannello di ogni documento che
    // ha la «Tecnica operativa», segnata, e la corregge solo un admin — come il testo stesso.

    /// <summary>Il deposito del testo comune, con i blocchi che gli si danno.</summary>
    private sealed class TestoComuneFinto : ITestoComuneStore
    {
        private readonly List<ExtraBlock> _blocchi;
        public TestoComuneFinto(params string[] paragrafi) =>
            _blocchi = paragrafi.Select(p => new ExtraBlock { Format = BlockFormat.Prose, Text = p }).ToList();

        public Task<IReadOnlyList<ExtraBlock>> LeggiAsync(string sezione, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExtraBlock>>(
                sezione == SectionKeys.RadarTowerTechnique ? _blocchi : new List<ExtraBlock>());

        public Task SalvaAsync(string sezione, IReadOnlyList<ExtraBlock> blocchi, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private const string FraseComune = "La torre non fornisce vettoramento.";

    /// <summary>Un documento d'aeroporto con la «Tecnica operativa» del radar di torre, dentro «Radar di torre».</summary>
    private static EditableDocument ConTecnica(Language lingua, string? fraseDelloScalo = null)
    {
        var base_ = Sezione("Tecnica operativa", fraseDelloScalo);
        var tecnica = new EditableSection
        {
            Id = 2, Title = base_.Title, SectionKey = SectionKeys.RadarTowerTechnique, Depth = 1, Order = 1,
            Blocks = base_.Blocks, Children = Array.Empty<EditableSection>(),
        };
        var radar = new EditableSection
        {
            Id = 1, Title = "Radar di torre", SectionKey = SectionKeys.RadarTower, Depth = 0, Order = 1,
            Blocks = Array.Empty<EditableBlock>(), Children = new[] { tecnica },
        };
        return Documento(lingua, radar);
    }

    [Fact]
    public async Task Il_testo_comune_compare_segnato_nel_documento_che_ha_la_tecnica_operativa()
    {
        var memoria = new MemoriaDiTraduzioneFinta().Nota(FraseComune, "The tower does not provide vectoring.");

        var righe = await Servizio(ConTecnica(Language.It), memoria, comune: new TestoComuneFinto(FraseComune))
            .RigheAsync(7, "en");

        var riga = Assert.Single(righe, r => r.Sorgente == FraseComune);
        Assert.True(riga.Comune);
        Assert.Equal("The tower does not provide vectoring.", riga.Tradotto);
        // Le frasi del documento restano sue.
        Assert.All(righe.Where(r => r.Sorgente != FraseComune), r => Assert.False(r.Comune));
    }

    [Fact]
    public async Task Senza_la_tecnica_operativa_il_testo_comune_non_c_e()
    {
        var righe = await Servizio(Documento(Language.It, Sezione("Piste", "Pista unica.")),
                new MemoriaDiTraduzioneFinta(), comune: new TestoComuneFinto(FraseComune))
            .RigheAsync(7, "en");

        Assert.DoesNotContain(righe, r => r.Sorgente == FraseComune);
    }

    /// <summary>Una frase che il documento ha già è sua: una riga sola, e la corregge l'Editor come sempre.</summary>
    [Fact]
    public async Task Una_frase_che_il_documento_ha_gia_non_si_ripete_e_resta_del_documento()
    {
        var righe = await Servizio(ConTecnica(Language.It, FraseComune), new MemoriaDiTraduzioneFinta(),
                comune: new TestoComuneFinto(FraseComune))
            .RigheAsync(7, "en");

        var riga = Assert.Single(righe, r => r.Sorgente == FraseComune);
        Assert.False(riga.Comune);
    }

    /// <summary>
    /// Il testo comune è scritto in italiano: in un documento inglese non è «la sua lingua», e la memoria si legge con la
    /// lingua del documento. Lì non compare; si rivede da un documento italiano, e la resa è la stessa per tutti.
    /// </summary>
    [Fact]
    public async Task In_un_documento_inglese_il_testo_comune_non_compare()
    {
        var righe = await Servizio(ConTecnica(Language.En), new MemoriaDiTraduzioneFinta(),
                comune: new TestoComuneFinto(FraseComune))
            .RigheAsync(7, "it");

        Assert.DoesNotContain(righe, r => r.Sorgente == FraseComune);
    }

    /// <summary>Una frase del testo comune la corregge solo un admin: cambia la resa di tutte le torri radar.</summary>
    [Fact]
    public async Task Una_frase_del_testo_comune_la_corregge_solo_un_admin()
    {
        var memoria = new MemoriaDiTraduzioneFinta();
        var comune = new TestoComuneFinto(FraseComune);

        await Assert.ThrowsAsync<EditNotAllowedException>(
            () => Servizio(ConTecnica(Language.It), memoria, new AuthzFinto { SoloEditor = true }, comune)
                .CorreggiAsync(7, "en", FraseComune, "The tower does not vector."));
        Assert.Null(memoria.UltimaCorrezione);

        await Servizio(ConTecnica(Language.It), memoria, new AuthzFinto(), comune)
            .CorreggiAsync(7, "en", FraseComune, "The tower does not vector.");
        Assert.Equal(FraseComune, memoria.UltimaCorrezione!.Value.Sorgente);
    }

    /// <summary>Le frasi del documento le corregge l'Editor anche dove c'è il testo comune: la regola nuova non le tocca.</summary>
    [Fact]
    public async Task Le_frasi_del_documento_le_corregge_ancora_l_Editor()
    {
        var memoria = new MemoriaDiTraduzioneFinta();

        await Servizio(ConTecnica(Language.It, "Separazione minima 3 NM."), memoria, new AuthzFinto { SoloEditor = true },
                new TestoComuneFinto(FraseComune))
            .CorreggiAsync(7, "en", "Separazione minima 3 NM.", "Minimum separation 3 NM.");

        Assert.Equal("Separazione minima 3 NM.", memoria.UltimaCorrezione!.Value.Sorgente);
    }

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
