using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La pagina «Fraseologia e traduzioni» (1 settembre 2026): la <b>ricerca</b>, il <b>«carica altre»</b> e il
/// <b>«dove si usa»</b>.
///
/// <para>
/// ⚠️ <b>Perché la ricerca deve stare sul database.</b> Il registro mostra un lotto per volta — cento righe
/// su centosettantasei, misurate — e un filtro applicato al lotto direbbe «non c'è» di una frase che sta
/// alla riga 101. Cioè mentirebbe esattamente nel caso in cui la ricerca serve: quando la memoria è lunga.
/// </para>
///
/// <para>
/// ⚠️ <b>Perché l'ordinamento dev'essere totale.</b> «Carica altre» è uno <c>Skip</c>: se due righe hanno
/// la stessa chiave d'ordine, il database è libero di metterle in ordine diverso fra due query, e il secondo
/// lotto salta una riga e ne ripete un'altra. Il difetto non si vede in una pagina piccola.
/// </para>
/// </summary>
public class RicercaEDoveSiUsaTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfTranslationMemory _memoria = default!;
    private EfGlossaryStore _glossario = default!;

    private const string It = "it";
    private const string En = "en";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _memoria = new EfTranslationMemory(_db);
        _glossario = new EfGlossaryStore(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private Task Macchina(params (string Sorgente, string Resa)[] righe) =>
        _memoria.SaveMachineAsync(It, En, "prova", righe.Select(r => (r.Sorgente, r.Resa)).ToList());

    // ---- La ricerca ----------------------------------------------------------------------------------

    [Fact]
    public async Task La_ricerca_guarda_la_frase_E_la_sua_resa()
    {
        await Macchina(
            ("Riporta sottovento pista 16.", "Report downwind runway 16."),
            ("Contatta la torre.", "Contact the tower."),
            ("Mantieni la quota.", "Maintain altitude."));

        // La parola sta nella SORGENTE di una riga...
        var perSorgente = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 50, "sottovento");
        Assert.Equal("Riporta sottovento pista 16.", Assert.Single(perSorgente).SourceText);

        // ...e nella RESA di un'altra. Chi rivede ricorda a volte l'una e a volte l'altra.
        var perResa = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 50, "tower");
        Assert.Equal("Contatta la torre.", Assert.Single(perResa).SourceText);
    }

    [Fact]
    public async Task La_ricerca_non_distingue_le_maiuscole()
    {
        await Macchina(("Riporta SOTTOVENTO.", "Report downwind."));

        Assert.Single(await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 50, "sottovento"));
        Assert.Single(await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 50, "SottoVento"));
    }

    [Fact]
    public async Task Il_conteggio_risponde_agli_STESSI_filtri_dell_elenco()
    {
        // ⚠️ È il «M» di «N di M»: se contasse un'altra cosa, il piede prometterebbe righe che non si
        // possono scorrere — o nasconderebbe un tasto «carica altre» che invece serviva.
        await Macchina(
            ("Riporta sottovento pista 16.", "Report downwind runway 16."),
            ("Riporta sottovento pista 34.", "Report downwind runway 34."),
            ("Contatta la torre.", "Contact the tower."));

        var quante = await _memoria.ContaPerRevisioneAsync(It, En, soloDaRileggere: false, "sottovento");
        var righe = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 50, "sottovento");

        Assert.Equal(2, quante);
        Assert.Equal(quante, righe.Count);
    }

    [Fact]
    public async Task Senza_testo_cercato_non_si_filtra_niente()
    {
        await Macchina(("Una.", "One."), ("Due.", "Two."));

        Assert.Equal(2, (await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 50)).Count);
        Assert.Equal(2, (await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 50, "   ")).Count);
    }

    // ---- «Carica altre» ------------------------------------------------------------------------------

    [Fact]
    public async Task I_due_lotti_non_saltano_e_non_ripetono_nessuna_riga()
    {
        var righe = Enumerable.Range(1, 25)
            .Select(i => ($"Frase numero {i:00}.", $"Phrase number {i:00}."))
            .ToList();
        await _memoria.SaveMachineAsync(It, En, "prova", righe);

        var primo = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 10);
        var secondo = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 10, salta: 10);
        var terzo = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 10, salta: 20);

        var tutte = primo.Concat(secondo).Concat(terzo).Select(r => r.Id).ToList();
        Assert.Equal(25, tutte.Count);
        Assert.Equal(25, tutte.Distinct().Count());
    }

    [Fact]
    public async Task La_ricerca_vale_anche_per_i_lotti_successivi()
    {
        // ⚠️ Il difetto che questo test chiude: una ricerca applicata al solo primo lotto. Qui la parola sta
        // in righe che il primo lotto NON contiene.
        var righe = Enumerable.Range(1, 20)
            .Select(i => (i % 5 == 0 ? $"Riporta sottovento {i:00}." : $"Frase {i:00}.", $"Phrase {i:00}."))
            .ToList();
        await _memoria.SaveMachineAsync(It, En, "prova", righe);

        var quante = await _memoria.ContaPerRevisioneAsync(It, En, soloDaRileggere: false, "sottovento");
        var primo = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 2, "sottovento");
        var secondo = await _memoria.ListForReviewAsync(It, En, soloDaRileggere: false, 2, "sottovento", salta: 2);

        Assert.Equal(4, quante);
        Assert.All(primo.Concat(secondo), r => Assert.Contains("sottovento", r.SourceText));
        Assert.Equal(4, primo.Concat(secondo).Select(r => r.Id).Distinct().Count());
    }

    // ---- «Dove si usa» -------------------------------------------------------------------------------

    /// <summary>Un documento con un titolo di sezione, un po' di prosa e una tabella.</summary>
    private async Task<int> DocumentoAsync(
        string titolo, string titoloSezione = "Sezione", string? prosa = null, string? bodyJson = null)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi,
            Title = titolo,
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

    [Fact]
    public async Task Dice_QUALI_documenti_contengono_la_frase_e_in_che_veste()
    {
        await DocumentoAsync("vIPI Roma", prosa: "Riporta sottovento.");
        await DocumentoAsync("vIPI Milano", titoloSezione: "Riporta sottovento.");

        var usi = await _memoria.DoveSiUsanoAsync(new[] { "Riporta sottovento." });
        var righe = usi[TranslationText.Hash("Riporta sottovento.")];

        Assert.Equal(2, righe.Count);
        Assert.Contains(righe, u => u.Titolo == "vIPI Roma" && u.Dove == UsoDelTesto.Prosa);
        Assert.Contains(righe, u => u.Titolo == "vIPI Milano" && u.Dove == UsoDelTesto.Titolo);
    }

    [Fact]
    public async Task Una_cella_di_tabella_conta_come_gli_altri_due_posti()
    {
        await DocumentoAsync("vIPI Brindisi",
            bodyJson: """{"columns":["Punto","Nota"],"rows":[{"cells":["Attendere al punto attesa","Hold short"]}]}""");

        var usi = await _memoria.DoveSiUsanoAsync(new[] { "Attendere al punto attesa" });
        var riga = Assert.Single(usi[TranslationText.Hash("Attendere al punto attesa")]);

        Assert.Equal("vIPI Brindisi", riga.Titolo);
        Assert.Equal(UsoDelTesto.Tabella, riga.Dove);
    }

    [Fact]
    public async Task Un_documento_che_la_contiene_TRE_volte_resta_una_riga_sola()
    {
        // La domanda è «quali documenti tocco», non «quante volte»: la pastiglia dice 1, e il pannello
        // apre una riga. Se contasse le occorrenze, i due numeri direbbero cose diverse.
        await DocumentoAsync("vIPI Roma", titoloSezione: "Riporta sottovento.", prosa: "Riporta sottovento.");

        var usi = await _memoria.DoveSiUsanoAsync(new[] { "Riporta sottovento." });
        Assert.Single(usi[TranslationText.Hash("Riporta sottovento.")]);
    }

    [Fact]
    public async Task Una_frase_che_nessun_documento_contiene_torna_lo_stesso_con_zero()
    {
        // ⚠️ «Zero» è una risposta: una chiave mancante costringerebbe ogni chiamante a distinguere
        // «non l'ho chiesto» da «non c'è» — e il registro mostra proprio quella differenza («nessun
        // documento»: la frase è in memoria ma il testo è cambiato).
        await DocumentoAsync("vIPI Roma", prosa: "Tutt'altra cosa.");

        var usi = await _memoria.DoveSiUsanoAsync(new[] { "Riporta sottovento." });
        Assert.Empty(usi[TranslationText.Hash("Riporta sottovento.")]);
    }

    [Fact]
    public async Task Il_conto_di_una_frase_sola_e_lo_STESSO_dell_elenco()
    {
        // Due strade per la stessa domanda divergono: qui la prima poggia sulla seconda, e il test lo fissa.
        await DocumentoAsync("vIPI Roma", prosa: "Riporta sottovento.");
        await DocumentoAsync("vIPI Milano", prosa: "Riporta sottovento.");

        var quanti = await _memoria.DocumentiToccatiAsync("Riporta sottovento.");
        var usi = await _memoria.DoveSiUsanoAsync(new[] { "Riporta sottovento." });

        Assert.Equal(2, quanti);
        Assert.Equal(quanti, usi[TranslationText.Hash("Riporta sottovento.")].Count);
    }

    [Fact]
    public async Task Si_chiede_di_PIU_frasi_in_una_volta()
    {
        await DocumentoAsync("vIPI Roma", prosa: "Prima frase.");
        await DocumentoAsync("vIPI Milano", prosa: "Seconda frase.");

        var usi = await _memoria.DoveSiUsanoAsync(new[] { "Prima frase.", "Seconda frase.", "Terza frase." });

        Assert.Equal(3, usi.Count);
        Assert.Equal("vIPI Roma", Assert.Single(usi[TranslationText.Hash("Prima frase.")]).Titolo);
        Assert.Equal("vIPI Milano", Assert.Single(usi[TranslationText.Hash("Seconda frase.")]).Titolo);
        Assert.Empty(usi[TranslationText.Hash("Terza frase.")]);
    }

    // ---- Il glossario: la formula e le sue frasi -----------------------------------------------------

    [Fact]
    public async Task Il_glossario_si_cerca_nei_due_lati()
    {
        await _glossario.UpsertAsync(It, En, "riporta sottovento", "report downwind", 1);
        await _glossario.UpsertAsync(It, En, "contatta la torre", "contact the tower", 1);

        Assert.Equal("riporta sottovento",
            Assert.Single(await _glossario.ListAsync(It, En, "sottovento")).SourceText);
        Assert.Equal("contatta la torre",
            Assert.Single(await _glossario.ListAsync(It, En, "TOWER")).SourceText);
        Assert.Equal(2, (await _glossario.ListAsync(It, En)).Count);
    }

    [Fact]
    public async Task Le_frasi_che_contengono_una_formula_comprendono_ANCHE_quelle_umane()
    {
        // ⚠️ Domanda diversa da ContaConLaFormulaAsync, che guarda le sole automatiche perché chiede «quante
        // si rifarebbero». Qui si chiede DOVE compare la formula, e una frase corretta a mano la contiene
        // esattamente come una tradotta dalla macchina.
        await Macchina(("Riporta sottovento pista 16.", "Report downwind runway 16."));
        await _memoria.SaveHumanAsync(It, En, "Poi riporta sottovento.", "Then report downwind.", 999);

        var frasi = await _memoria.FrasiConLaFormulaAsync(It, En, "riporta sottovento", 10);

        Assert.Equal(2, frasi.Count);
        Assert.Contains(frasi, f => f.Origin == TranslationOrigin.Human);
        Assert.Contains(frasi, f => f.Origin == TranslationOrigin.Machine);

        // E il conto «quante si rifarebbero» resta l'altro, con l'umana esclusa.
        Assert.Equal(1, await _memoria.ContaConLaFormulaAsync(It, En, "riporta sottovento"));
    }

    [Fact]
    public async Task Il_conto_per_formula_torna_zero_per_quelle_che_non_compaiono()
    {
        await Macchina(("Riporta sottovento pista 16.", "Report downwind runway 16."));

        var conti = await _memoria.ContaFrasiPerFormuleAsync(
            It, En, new[] { "riporta sottovento", "armamento e disarmo" });

        Assert.Equal(1, conti["riporta sottovento"]);
        Assert.Equal(0, conti["armamento e disarmo"]);
    }
}
