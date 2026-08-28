using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Tests;

/// <summary>
/// La vista del documento nella lingua di chi legge (carta <c>2026-08-27-documenti-bilingue.md</c> §7).
///
/// <para>
/// ⚠️ <b>L'invariante del committente è provato qui, e in modo strutturale.</b> «Quel che è scritto in
/// italiano c'è in inglese e viceversa»: il traduttore non produce un secondo documento, rende <b>lo
/// stesso</b> — stesse sezioni, stesso ordine, stessi blocchi — e ogni pezzo di testo viene dalla stessa
/// impronta. Non c'è modo di far dire alla vista tradotta qualcosa che l'originale non dice.
/// </para>
/// </summary>
public class DocumentTranslatorTests
{
    private sealed class MemoriaFinta : ITranslationMemory
    {
        private readonly Dictionary<string, KnownTranslation> _note = new(StringComparer.Ordinal);
        public int Letture { get; private set; }

        public MemoriaFinta Nota(string sorgente, string bersaglio, bool riletta = false)
        {
            _note[TranslationText.Hash(sorgente)] =
                new KnownTranslation(bersaglio, riletta ? TranslationOrigin.Human : TranslationOrigin.Machine, riletta);
            return this;
        }

        public Task<IReadOnlyDictionary<string, KnownTranslation>> LookupAsync(
            string s, string t, IReadOnlyCollection<string> hashes, CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult<IReadOnlyDictionary<string, KnownTranslation>>(
                hashes.Where(_note.ContainsKey).ToDictionary(h => h, h => _note[h], StringComparer.Ordinal));
        }

        public Task<int> SaveMachineAsync(string s, string t, string e,
            IReadOnlyList<(string SourceText, string TargetText)> v, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveHumanAsync(string s, string t, string a, string b, int u, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TranslationReviewRow>> ListForReviewAsync(
            string s, string t, bool solo, int limite, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TranslationReviewRow>>(Array.Empty<TranslationReviewRow>());

        public Task<IReadOnlyDictionary<string, string>> LoadAllAsync(
            string s, string t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));

        public Task<IReadOnlySet<string>> LoadHumanHashesAsync(
            string s, string t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

        public Task<(int Totale, int DaRileggere)> ContaAsync(string s, string t, CancellationToken ct = default) =>
            Task.FromResult((0, 0));

        public Task<int> DocumentiToccatiAsync(string s, CancellationToken ct = default) => Task.FromResult(0);
        public Task<long> CaratteriSpesiStimatiAsync(string e, CancellationToken ct = default) => Task.FromResult(0L);
    }

    private static BlockView Blocco(int id, string? body = null, string? json = null) => new()
    {
        Id = id,
        Format = json is null ? BlockFormat.Prose : BlockFormat.Table,
        State = RenderState.Expanded,
        Body = body,
        BodyJson = json,
    };

    private static SectionView Sezione(string titolo, params BlockView[] blocchi) => new()
    {
        Id = "s-1",
        Title = titolo,
        Depth = 0,
        SectionKey = "custom:abc",
        Blocks = blocchi,
        Children = Array.Empty<SectionView>(),
    };

    private static DocumentView Documento(string titolo, params SectionView[] sezioni) => new()
    {
        Title = titolo,
        AiracCycle = "2609",
        Sections = sezioni,
    };

    // ---- Il caso normale -----------------------------------------------------------------------------

    [Fact]
    public async Task Titoli_e_corpi_passano_alla_lingua_di_chi_legge()
    {
        var memoria = new MemoriaFinta()
            .Nota("Procedure generali", "General procedures")
            .Nota("Separazioni", "Separations")
            .Nota("Contatta la torre.", "Contact the tower.");

        var doc = Documento("Procedure generali",
            Sezione("Separazioni", Blocco(1, "Contatta la torre.")));

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");

        Assert.Equal("General procedures", esito.View.Title);
        Assert.Equal("Separations", esito.View.Sections[0].Title);
        Assert.Equal("Contact the tower.", esito.View.Sections[0].Blocks[0].Body);
        Assert.Equal("2609", esito.View.AiracCycle);   // un ciclo AIRAC non si traduce
    }

    [Fact]
    public async Task Le_celle_di_una_tabella_si_traducono_e_la_struttura_resta()
    {
        const string tabella =
            """{"columns":["Item","Value"],"unified":false,"rows":[{"cells":["Review cycle","Annually"]}]}""";
        var memoria = new MemoriaFinta()
            .Nota("Review cycle", "Ciclo di revisione")
            .Nota("Item", "Voce");

        var doc = Documento("T", Sezione("S", Blocco(1, json: tabella)));
        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "en", "it");

        var reso = esito.View.Sections[0].Blocks[0].BodyJson!;
        Assert.Contains("Ciclo di revisione", reso);
        Assert.Contains("Voce", reso);
        Assert.Contains("\"unified\":false", reso);   // struttura intatta
        Assert.Contains("Annually", reso);            // non tradotta: resta com'era
    }

    // ---- Quel che manca ------------------------------------------------------------------------------

    [Fact]
    public async Task Cio_che_non_e_tradotto_resta_nella_lingua_sorgente_e_non_sparisce()
    {
        // ⚠️ Un documento a chiazze si legge male ma si legge; un documento con dei buchi MENTE.
        var memoria = new MemoriaFinta().Nota("Prima frase.", "First sentence.");
        var doc = Documento("Titolo mai tradotto",
            Sezione("Sezione mai tradotta", Blocco(1, "Prima frase.\n\nSeconda frase mai tradotta.")));

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");

        Assert.Equal("Titolo mai tradotto", esito.View.Title);
        Assert.Equal("Sezione mai tradotta", esito.View.Sections[0].Title);
        Assert.Equal("First sentence.\n\nSeconda frase mai tradotta.", esito.View.Sections[0].Blocks[0].Body);
    }

    [Fact]
    public async Task La_copertura_dice_quanto_manca_e_quanto_e_da_rileggere()
    {
        var memoria = new MemoriaFinta()
            .Nota("Uno.", "One.", riletta: true)
            .Nota("Due.", "Two.");                       // automatica, mai riletta

        var doc = Documento("Titolo", Sezione("Sezione", Blocco(1, "Uno.\n\nDue.\n\nTre.")));
        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");

        // 5 segmenti: titolo documento, titolo sezione, tre paragrafi.
        Assert.Equal(5, esito.Coverage.Segmenti);
        Assert.Equal(2, esito.Coverage.Tradotti);
        Assert.Equal(1, esito.Coverage.Riletti);
        Assert.Equal(3, esito.Coverage.Mancanti);
        Assert.False(esito.Coverage.Completa);
        Assert.True(esito.Coverage.DaRileggere);         // «Due.» non l'ha riletta nessuno
    }

    [Fact]
    public async Task Se_tutto_e_stato_riletto_la_vista_non_va_marcata()
    {
        var memoria = new MemoriaFinta().Nota("Titolo", "Title", true).Nota("Sezione", "Section", true);
        var doc = Documento("Titolo", Sezione("Sezione"));
        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");

        Assert.True(esito.Coverage.Completa);
        Assert.False(esito.Coverage.DaRileggere);
    }

    // ---- L'invariante --------------------------------------------------------------------------------

    [Fact]
    public async Task La_vista_tradotta_ha_LE_STESSE_sezioni_nello_stesso_ordine()
    {
        // «Quel che e' scritto in italiano c'e' in inglese e viceversa»: la divergenza qui non e' un rischio
        // da sorvegliare, e' IRRAPPRESENTABILE -- non esiste un percorso che aggiunga o tolga una sezione.
        var memoria = new MemoriaFinta().Nota("A", "AA");
        var doc = Documento("T",
            new SectionView
            {
                Id = "s-1", Title = "A", Depth = 0, SectionKey = "k1",
                Blocks = new[] { Blocco(1, "x"), Blocco(2, "y") },
                Children = new[] { Sezione("figlia") },
            });

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");

        Assert.Single(esito.View.Sections);
        Assert.Equal("AA", esito.View.Sections[0].Title);
        Assert.Equal(2, esito.View.Sections[0].Blocks.Count);
        Assert.Single(esito.View.Sections[0].Children);
        Assert.Equal(new[] { 1, 2 }, esito.View.Sections[0].Blocks.Select(b => b.Id));
    }

    [Fact]
    public async Task La_traduzione_non_azzera_i_flag_della_sezione()
    {
        // ⚠️ DIFETTO VERO, trovato da una prova live il 28 agosto 2026. Questa classe RICOSTRUISCE le
        // sezioni, e ogni flag per-sezione che non si ricopia viene azzerato dalla traduzione -- in
        // silenzio, perche' il default e' quello «buono» e la pagina continua a rendersi. Effetto: su un
        // documento tradotto la chip pilota/ATC non compariva mai e il filtro non filtrava, e nessun test
        // se ne accorgeva perche' nessuno guardava i flag DOPO la traduzione.
        var memoria = new MemoriaFinta().Nota("Titolo", "Title");
        var doc = Documento("Titolo", new SectionView
        {
            Id = "s-1", Title = "Sezione", Depth = 0, SectionKey = "coordination",
            Audience = SectionAudience.Controllers,
            IsHidden = true, BeforeParentBody = true, LeadSentence = true,
            Blocks = Array.Empty<BlockView>(), Children = Array.Empty<SectionView>(),
        });

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");
        var sez = esito.View.Sections[0];

        Assert.Equal(SectionAudience.Controllers, sez.Audience);
        Assert.True(sez.IsHidden);
        Assert.True(sez.BeforeParentBody);
        Assert.True(sez.LeadSentence);
    }

    // ---- Quel che NON si tocca -----------------------------------------------------------------------

    [Fact]
    public async Task Leggere_un_documento_nella_sua_lingua_non_costa_una_query()
    {
        var memoria = new MemoriaFinta();
        var doc = Documento("Titolo", Sezione("Sezione", Blocco(1, "Testo")));

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "it");

        Assert.Same(doc, esito.View);
        Assert.Equal(0, memoria.Letture);
    }

    [Fact]
    public async Task Una_sezione_resa_dalla_pagina_non_si_tocca()
    {
        // Le derivate e le strutturate non hanno corpo nel view — lo disegna il componente. La loro prosa e'
        // generata da codice e si localizza con le RISORSE, non col traduttore automatico.
        var memoria = new MemoriaFinta();
        var doc = Documento("T", new SectionView
        {
            Id = "s-1", Title = "AOR", Depth = 0, SectionKey = "aor",
            Blocks = Array.Empty<BlockView>(), Children = Array.Empty<SectionView>(),
        });

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");
        Assert.Empty(esito.View.Sections[0].Blocks);
    }

    [Fact]
    public async Task La_memoria_si_interroga_UNA_volta_sola_per_tutto_il_documento()
    {
        // ⚠️ Una query per segmento sarebbe una corsa sul DbContext del circuito Blazor: il guasto
        // «second operation» gia' pagato sei volte su questo prodotto.
        var memoria = new MemoriaFinta();
        var doc = Documento("T",
            Sezione("S1", Blocco(1, "a"), Blocco(2, "b")),
            Sezione("S2", Blocco(3, "c")));

        await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");
        Assert.Equal(1, memoria.Letture);
    }
}
