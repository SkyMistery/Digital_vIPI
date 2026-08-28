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
        var memoria = new MemoriaDiTraduzioneFinta()
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
        var memoria = new MemoriaDiTraduzioneFinta()
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
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Prima frase.", "First sentence.");
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
        var memoria = new MemoriaDiTraduzioneFinta()
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
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Titolo", "Title", true).Nota("Sezione", "Section", true);
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
        var memoria = new MemoriaDiTraduzioneFinta().Nota("A", "AA");
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
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Titolo", "Title");
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
        var memoria = new MemoriaDiTraduzioneFinta();
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
        var memoria = new MemoriaDiTraduzioneFinta();
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
        var memoria = new MemoriaDiTraduzioneFinta();
        var doc = Documento("T",
            Sezione("S1", Blocco(1, "a"), Blocco(2, "b")),
            Sezione("S2", Blocco(3, "c")));

        await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");
        Assert.Equal(1, memoria.Letture);
    }

    // ---- La lingua sorgente la dichiara il DOCUMENTO -------------------------------------------------
    //
    // ⚠️ Fino al 28 agosto 2026 ogni pagina bilingue scriveva la sorgente a mano: «it» il vSOP militare,
    // «en» la vLOA. Finché ogni famiglia nasce in una lingua sola la cosa regge — ma è un secondo posto che
    // dichiara la lingua, e un secondo posto può contraddire il primo. Il guasto non fa rumore: la memoria
    // viene cercata nella coppia sbagliata, torna vuota, e il lettore vede il documento intatto.

    [Fact]
    public void La_sorgente_e_quella_del_documento_quando_ce_l_ha()
    {
        Assert.Equal("en", DocumentTranslator.CodiceSorgente(Language.En, Language.It));
        Assert.Equal("it", DocumentTranslator.CodiceSorgente(Language.It, Language.En));
    }

    [Fact]
    public void Senza_lingua_sul_documento_vale_quella_in_cui_la_famiglia_nasce()
    {
        // I documenti salvati prima che il campo esistesse arrivano con la lingua nulla.
        Assert.Equal("it", DocumentTranslator.CodiceSorgente(null, Language.It));
        Assert.Equal("en", DocumentTranslator.CodiceSorgente(null, Language.En));
    }

    [Fact]
    public async Task La_lingua_del_documento_batte_quella_della_famiglia()
    {
        // Una vLOA (famiglia inglese) redatta in italiano: si traduce DALL'ITALIANO, o la memoria si
        // cercherebbe in «en→en» e il lettore vedrebbe il documento intatto senza capire perché.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Riporta sottovento.", "Report downwind.");
        var doc = new DocumentView
        {
            Title = "T",
            AiracCycle = "2609",
            Language = Language.It,
            Sections = new[] { Sezione("S", Blocco(1, "Riporta sottovento.")) },
        };

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, Language.En, "en");

        Assert.Equal("it", memoria.UltimaSorgente);
        Assert.Equal("en", memoria.UltimoBersaglio);
        Assert.Equal("Report downwind.", esito.View.Sections[0].Blocks[0].Body);
    }

    [Fact]
    public async Task Un_documento_letto_nella_sua_lingua_non_costa_una_query()
    {
        var memoria = new MemoriaDiTraduzioneFinta();
        var doc = new DocumentView
        {
            Title = "T",
            AiracCycle = "2609",
            Language = Language.En,
            Sections = new[] { Sezione("S", Blocco(1, "Contact the tower.")) },
        };

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, Language.It, "en");

        Assert.Equal(0, memoria.Letture);
        Assert.Same(doc, esito.View);
    }
}
