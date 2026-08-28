using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// «Questa correzione tocca N documenti»: il numero che il pannello di revisione mostra <b>prima</b> che
/// si salvi.
///
/// <para>
/// ⚠️ <b>Perché è un numero che deve dire la verità.</b> Una correzione di traduzione tocca la <b>frase</b>,
/// non il documento: la stessa resa vale ovunque quella frase compaia, e chi corregge lo sta scoprendo
/// proprio in quel momento. Il conto è l'unico avviso che ha, e il pannello lo mostra solo <b>sopra il
/// primo documento</b> — quindi un conto che sbaglia per difetto non dà un numero impreciso: non dà
/// <b>nessun avviso</b>.
/// </para>
///
/// <para>
/// ⚠️ <b>Il difetto, com'era fino al 28 agosto 2026:</b> un <c>Body.Contains(testo)</c> su
/// <c>ContentBlock.Body</c> e basta. Una frase che sta in un <b>titolo di sezione</b> o in una <b>cella di
/// tabella</b> contava zero; e una che stava in un corpo con l'apostrofo tipografico non corrispondeva al
/// testo normalizzato che arriva dalla memoria, quindi contava zero anche lì. Il commento prometteva una
/// «conferma in memoria con la normalizzazione» che nel codice non c'era.
/// </para>
/// </summary>
public class DocumentiToccatiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfTranslationMemory _memoria = default!;

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

    /// <summary>Un documento con una sezione: titolo, prosa e celle di tabella si scelgono a piacere.</summary>
    private async Task<int> DocumentoAsync(
        string titoloSezione = "Sezione", string? prosa = null, string? bodyJson = null)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi,
            Title = "vIPI di prova",
            Language = Language.It,
            Status = DocumentStatus.Draft,
            LastUpdatedAiracCycle = "2609",
        };
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft,
            AiracCycle = "2609", CreatedUtc = DateTime.UtcNow,
        };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var sez = new DocumentSection
        {
            DocumentVersion = ver, Title = titoloSezione, Order = 1, Depth = 0,
            SectionKey = "operationaltechnique", RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(sez);
        await _db.SaveChangesAsync();

        if (prosa is not null || bodyJson is not null)
        {
            _db.ContentBlocks.Add(new ContentBlock
            {
                DocumentVersion = ver, Section = sez, Order = 1,
                Format = bodyJson is null ? BlockFormat.Prose : BlockFormat.Table,
                Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always,
                Body = prosa, BodyJson = bodyJson,
                RowVersion = Guid.NewGuid().ToByteArray(),
            });
            await _db.SaveChangesAsync();
        }

        return doc.Id;
    }

    // ---- I tre posti in cui una frase può stare -------------------------------------------------------

    [Fact]
    public async Task La_frase_nella_PROSA_si_conta()
    {
        await DocumentoAsync(prosa: "Riporta sottovento.");

        Assert.Equal(1, await _memoria.DocumentiToccatiAsync("Riporta sottovento."));
    }

    [Fact]
    public async Task La_frase_in_un_TITOLO_DI_SEZIONE_si_conta()
    {
        // ⚠️ Contava ZERO: la query guardava solo i corpi. E i titoli di sezione sono nel corpus, quindi la
        // memoria ne è piena — è il caso in cui la correzione è più diffusa e l'avviso non compariva.
        await DocumentoAsync(titoloSezione: "Regole piste");

        Assert.Equal(1, await _memoria.DocumentiToccatiAsync("Regole piste"));
    }

    [Fact]
    public async Task La_frase_in_una_CELLA_DI_TABELLA_si_conta()
    {
        // ⚠️ Contava ZERO: BodyJson non veniva guardato affatto. Le tabelle sono metà del contenuto di una
        // vIPI — frequenze, coordinamenti, regole pista.
        await DocumentoAsync(bodyJson: """{"columns":["Punto","Aeroporto"],"rows":[{"cells":["Attendere al punto attesa","LIRF"]}]}""");

        Assert.Equal(1, await _memoria.DocumentiToccatiAsync("Attendere al punto attesa"));
    }

    // ---- La grafia non conta --------------------------------------------------------------------------

    [Fact]
    public async Task L_apostrofo_TIPOGRAFICO_nel_documento_non_nasconde_il_documento()
    {
        // ⚠️ Il testo che arriva dalla memoria è NORMALIZZATO (apostrofo dritto), quello nel documento no:
        // l'editor lo scrive come glielo dà il programma di scrittura. Un confronto per contenuto li
        // faceva diversi, e il documento spariva dal conto. Il confronto è per IMPRONTA, e l'impronta
        // normalizza — è l'unica ragione per cui la memoria è indicizzata così.
        await DocumentoAsync(prosa: "L’aeroporto è chiuso.");

        Assert.Equal(1, await _memoria.DocumentiToccatiAsync("L'aeroporto è chiuso."));
    }

    [Fact]
    public async Task L_a_capo_di_Windows_non_nasconde_il_documento()
    {
        await DocumentoAsync(prosa: "Prima riga.\r\nSeconda riga.");

        Assert.Equal(1, await _memoria.DocumentiToccatiAsync("Prima riga.\nSeconda riga."));
    }

    // ---- Quel che NON si deve contare -----------------------------------------------------------------

    [Fact]
    public async Task Una_frase_che_CONTIENE_quella_cercata_non_si_conta()
    {
        // ⚠️ Il vecchio `Contains` contava per SOTTOSTRINGA: «Riporta sottovento.» faceva scattare anche
        // «Riporta sottovento. Poi contatta la torre.», che è un'altra frase con un'altra impronta e una
        // traduzione sua. Un conto che sbaglia per eccesso spaventa chi corregge tanto quanto uno che
        // sbaglia per difetto lo lascia all'oscuro.
        await DocumentoAsync(prosa: "Riporta sottovento. Poi contatta la torre.");

        Assert.Equal(0, await _memoria.DocumentiToccatiAsync("Riporta sottovento."));
    }

    [Fact]
    public async Task Un_documento_con_la_frase_in_PIU_posti_si_conta_UNA_volta()
    {
        // Il numero è di DOCUMENTI, non di occorrenze: «tocca 4 blocchi» non dice niente a nessuno.
        await DocumentoAsync(titoloSezione: "Avvicinamento", prosa: "Avvicinamento");

        Assert.Equal(1, await _memoria.DocumentiToccatiAsync("Avvicinamento"));
    }

    [Fact]
    public async Task Documenti_diversi_si_sommano()
    {
        // Il caso che dà senso all'avviso: la stessa frase in tre documenti, uno per ciascuno dei tre posti
        // in cui può stare.
        await DocumentoAsync(prosa: "Contatta la torre.");
        await DocumentoAsync(titoloSezione: "Contatta la torre.");
        await DocumentoAsync(bodyJson: """{"rows":[{"cells":["Contatta la torre."]}]}""");

        Assert.Equal(3, await _memoria.DocumentiToccatiAsync("Contatta la torre."));
    }

    [Fact]
    public async Task Una_frase_che_nessun_documento_contiene_vale_ZERO()
    {
        // Succede davvero: il testo è stato riscritto nell'editor e la riga di memoria è rimasta. Zero è la
        // risposta giusta, e la UI ha una frase sua per dirlo invece di scrivere «vale per 1 documenti».
        await DocumentoAsync(prosa: "Contatta la torre.");

        Assert.Equal(0, await _memoria.DocumentiToccatiAsync("Una frase che non c'è."));
    }

    [Fact]
    public async Task Il_testo_vuoto_vale_ZERO_e_non_conta_tutto()
    {
        // ⚠️ Senza la guardia, l'impronta della stringa vuota non corrisponde a niente — ma con il vecchio
        // `Contains("")` sarebbe stata VERA per ogni corpo, cioè «tocca tutti i documenti».
        await DocumentoAsync(prosa: "Contatta la torre.");

        Assert.Equal(0, await _memoria.DocumentiToccatiAsync(""));
        Assert.Equal(0, await _memoria.DocumentiToccatiAsync("   \n  "));
    }
}
