using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// A che punto è la traduzione (carta <c>docs/feature/2026-09-04-stato-traduzione.md</c>).
///
/// <para>
/// ⚠️ <b>I test che contano sono due</b>, e sono i due che la prima stesura della carta avrebbe sbagliato:
/// <see cref="Il_pubblicato_ricade_sulla_memoria_dove_lo_snapshot_non_congela"/> — che è il guasto §Q18
/// visto dal lato di chi <i>conta</i> invece che di chi legge — e
/// <see cref="Un_documento_a_lingua_bloccata_non_e_allo_zero_per_cento"/>, perché «bloccata» e «non
/// tradotta» a schermo si somigliano e vogliono dire cose opposte.
/// </para>
/// </summary>
public class EfStatoTraduzioneTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfTranslationMemory _memoria = default!;
    private readonly ArchivioDocumentiFinto _gestiti = new();

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _memoria = new EfTranslationMemory(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private EfStatoTraduzione Stato() =>
        new(_db, _memoria, new EfGlossaryStore(_db), _gestiti);

    // ---- Il banco ---------------------------------------------------------------------------------------

    /// <summary>Un documento con una versione di lavoro, le sue sezioni e un blocco di prosa.</summary>
    private async Task<int> ScriviDocumentoAsync(
        string titolo, Language lingua, string prosa, bool bloccata = false,
        DocumentStatus stato = DocumentStatus.Published)
    {
        var doc = new Document
        {
            Title = titolo,
            Type = DocumentType.Vipi,
            Status = stato,
            Language = lingua,
            LanguageLocked = bloccata,
            LastUpdatedAiracCycle = "2609",
            LastUpdatedUtc = DateTime.UtcNow,
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var versione = new DocumentVersion
        {
            DocumentId = doc.Id,
            VersionNumber = 1,
            Status = stato,
            CreatedByUserId = 1,
            CreatedUtc = DateTime.UtcNow,
            AiracCycle = "2609",
        };
        _db.DocumentVersions.Add(versione);
        await _db.SaveChangesAsync();

        doc.CurrentVersionId = versione.Id;

        var sezione = new DocumentSection
        {
            DocumentVersionId = versione.Id,
            Title = "Generalità",
            Order = 1,
            Depth = 0,
            SectionKey = "general",
        };
        _db.DocumentSections.Add(sezione);
        await _db.SaveChangesAsync();

        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = versione.Id,
            SectionId = sezione.Id,
            Order = 1,
            Tier = BlockTier.Reduced,
            Format = BlockFormat.Prose,
            Visibility = BlockVisibility.Always,
            Body = prosa,
        });
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    /// <summary>Una release in vigore con lo snapshot indicato.</summary>
    private async Task PubblicaAsync(int documentId, string chiave, RawDocument snapshot)
    {
        _gestiti.Aggiungi(documentId, chiave);
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.Airport,
            TargetKey = chiave,
            VersionNumber = 1,
            ReleaseAiracCycle = "2609",
            ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-1),
            Status = ReleaseStatus.Effective,
            PayloadJson = JsonSerializer.Serialize(new DocReleasePayload { Doc = snapshot }),
            CreatedByUserId = 1,
            CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private static RawDocument Snapshot(string prosa, Language? lingua = null,
        Dictionary<string, Dictionary<string, FrozenTranslation>>? congelate = null) => new()
        {
            Title = "vIPI — LIBC Crotone",
            AiracCycle = "2609",
            Language = lingua,
            Translations = congelate,
            Roots = new List<RawSection>
            {
                new()
                {
                    Id = 1, Title = "Generalità", Depth = 0, SectionKey = "general", Order = 1,
                    Blocks =
                    {
                        new RawBlock
                        {
                            Id = 1, Order = 1, Format = BlockFormat.Prose,
                            Visibility = BlockVisibility.Always, Tier = BlockTier.Reduced, Body = prosa,
                        },
                    },
                },
            },
        };

    // ---- I due che contano ------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 Il guasto §Q18, dal lato di chi conta. Lo snapshot non porta niente di congelato — è il caso di
    /// TUTTE e 17 le release efficaci misurate il 4 settembre 2026 — e il lettore, lì, vede la memoria viva.
    /// Una tabella che contasse solo il congelato direbbe «0%» di una pagina che a schermo è intera.
    /// </summary>
    [Fact]
    public async Task Il_pubblicato_ricade_sulla_memoria_dove_lo_snapshot_non_congela()
    {
        var id = await ScriviDocumentoAsync("vIPI — LIBC Crotone", Language.It, "Riporta sottovento.");
        await PubblicaAsync(id, "LIBC", Snapshot("Riporta sottovento."));
        // ⚠️ Anche il TITOLO della sezione: è un segmento come gli altri, e dimenticarlo qui darebbe 50%
        // — che è esattamente il modo in cui una copertura «quasi giusta» non si fa notare.
        await _memoria.SaveMachineAsync("it", "en", "azure",
            new[] { ("Riporta sottovento.", "Report downwind."), ("Generalità", "General") });

        var riga = await Stato().DocumentoAsync(id);

        Assert.NotNull(riga);
        Assert.True(riga!.HaReleaseEfficace);
        Assert.False(riga.ReleaseCongela);          // ⚠️ e si DICE: è il dato che oggi non si vede da nessuna parte
        Assert.Equal(100, riga.PercentualePubblicato);
        Assert.Equal(100, riga.PercentualeBozza);
    }

    /// <summary>
    /// ⚠️ Bloccata non è «non tradotta»: è un terzo vuoto. Detto con le parole degli altri due, chi guarda
    /// la tabella crede che il documento sia indietro e va a cercare un lavoro che non esiste.
    /// </summary>
    [Fact]
    public async Task Un_documento_a_lingua_bloccata_non_e_allo_zero_per_cento()
    {
        var id = await ScriviDocumentoAsync("vSOP MIL — LIBG", Language.En, "Contact the tower.", bloccata: true);

        var riga = await Stato().DocumentoAsync(id);

        Assert.NotNull(riga);
        Assert.Equal(StatoTraduzione.Bloccata, riga!.Stato);
        Assert.Equal(0, riga.Bozza.Segmenti);       // fuori dal corpus: non c'è niente da contare
        Assert.Equal(0, riga.AMano);
    }

    // ---- Il resto ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Il_congelato_vince_dove_c_e_e_porta_il_suo_timbro()
    {
        var id = await ScriviDocumentoAsync("vIPI — LIBC Crotone", Language.It, "Riporta sottovento.");
        var congelate = new Dictionary<string, Dictionary<string, FrozenTranslation>>
        {
            ["en"] = new()
            {
                [TranslationText.Hash("Riporta sottovento.")] = new FrozenTranslation("Report downwind.", true),
            },
        };
        await PubblicaAsync(id, "LIBC", Snapshot("Riporta sottovento.", Language.It, congelate));

        var riga = await Stato().DocumentoAsync(id);

        Assert.True(riga!.ReleaseCongela);
        Assert.Equal(1, riga.Pubblicato.Tradotti);
        Assert.Equal(1, riga.Pubblicato.Riletti);       // il timbro viaggia: non è «da rileggere» per sempre
        Assert.False(riga.Pubblicato.DaRileggere);
        // ⚠️ E la BOZZA resta scoperta: il congelato è della release, non della memoria. Sono due domande.
        Assert.Equal(0, riga.Bozza.Tradotti);
        Assert.Equal(StatoTraduzione.NonCominciata, riga.Stato);
    }

    [Fact]
    public async Task Senza_release_il_pubblicato_non_e_zero_ma_ASSENTE()
    {
        var id = await ScriviDocumentoAsync("vIPI Milano", Language.It, "Riporta sottovento.",
            stato: DocumentStatus.Draft);

        var riga = await Stato().DocumentoAsync(id);

        Assert.False(riga!.HaReleaseEfficace);
        Assert.Equal(0, riga.Pubblicato.Segmenti);   // chi mostra dirà «—», non «0%»
    }

    /// <summary>
    /// 🔴 <b>Un VID non rende un segmento «da fare a mano»</b>, ed è la cosa che si sbaglia per prima: il
    /// protettore lo mette dentro un segnaposto e la frase parte lo stesso. «A mano» è il cancello
    /// <i>fail closed</i> — scatta su ciò che il protettore <b>non sa chiudere</b> — e sul corpus vero è
    /// zero: misurato il 4 settembre 2026, <b>0 non sicuri su 104 segmenti distinti</b>.
    ///
    /// <para>Il test presidia quel numero nei due versi: che un dato personale non blocchi la traduzione
    /// (sarebbe una funzione che si spegne da sola), e che il conto separato esista comunque — il giorno
    /// che il cancello scatta, chi guarda deve sapere che quel segmento aspetta <b>una persona</b> e non il
    /// prossimo quarto d'ora.</para>
    /// </summary>
    [Fact]
    public async Task Un_dato_personale_non_diventa_lavoro_a_mano_il_protettore_lo_chiude()
    {
        var id = await ScriviDocumentoAsync("vIPI — LIBD Bari", Language.It,
            "Scrivere a Mario Rossi, VID 123456, per l'accesso.");

        var riga = await Stato().DocumentoAsync(id);

        Assert.Equal(StatoTraduzione.NonCominciata, riga!.Stato);
        Assert.Equal(0, riga.AMano);
        Assert.Equal(riga.Bozza.Mancanti, riga.InAttesa);
    }

    [Fact]
    public async Task Il_quadro_conta_i_documenti_che_aspettano_la_macchina()
    {
        await ScriviDocumentoAsync("Uno", Language.It, "Riporta sottovento.");
        await ScriviDocumentoAsync("Due", Language.It, "Chiamare la torre.");
        await _memoria.SaveMachineAsync("it", "en", "azure",
            new[] { ("Riporta sottovento.", "Report downwind."), ("Generalità", "General") });

        var quadro = await Stato().QuadroAsync();

        Assert.Equal(2, quadro.Righe.Count);
        // Il primo è a posto; al secondo manca la sua frase (il titolo lo condividono, ed è in memoria).
        Assert.Equal(1, quadro.DocumentiInAttesa);
        Assert.Equal(0, quadro.DocumentiAMano);
    }

    /// <summary>Le due lingue sono un verso solo per documento: quella in cui è scritto, e l'altra.</summary>
    [Fact]
    public async Task La_direzione_la_dice_il_documento_non_chi_guarda()
    {
        var it = await ScriviDocumentoAsync("vIPI", Language.It, "Riporta sottovento.");
        var en = await ScriviDocumentoAsync("vLOA", Language.En, "Report downwind.");

        var stato = Stato();
        Assert.Equal("en", (await stato.DocumentoAsync(it))!.LinguaLettura);
        Assert.Equal("it", (await stato.DocumentoAsync(en))!.LinguaLettura);
    }

    /// <summary>Un archivio dei documenti gestiti che conosce solo la coppia (documento, chiave di release).</summary>
    private sealed class ArchivioDocumentiFinto : IDocumentAdminRepository
    {
        private readonly List<ManagedDoc> _docs = new();

        public void Aggiungi(int documentId, string chiave) => _docs.Add(new ManagedDoc(
            ReleaseTargetType.Airport, "—", "—", null, true, false, false,
            ReleaseTargetType.Airport, chiave, documentId));

        public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ManagedDoc>>(_docs);

        public Task<IReadOnlyDictionary<int, ManagedDoc>> DescribeAsync(
            IReadOnlyCollection<int> documentIds, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, string>> GetTitlesAsync(
            IReadOnlyCollection<int> documentIds, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DocumentLanguageState?> GetLanguageAsync(ManagedDocRef doc, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SetLanguageAsync(ManagedDocRef doc, Language language, bool locked, int actorUserId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task SetHiddenAsync(ManagedDocRef doc, bool hidden, int actorUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(ManagedDocRef doc, int actorUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
