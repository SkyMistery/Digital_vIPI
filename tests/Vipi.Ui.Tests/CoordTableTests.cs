using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Tabella dei coordinamenti condivisa (<c>CoordTable</c>): quali colonne compaiono e come si rendono i gruppi
/// di varianti. Sono due regole che vivono solo nel markup — nessun test di dominio le vedrebbe — e sono
/// esattamente quelle che, sbagliate, cambiano il documento pubblicato senza rompere niente.
/// </summary>
public class CoordTableTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa: le asserzioni parlano di chiavi, non di traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public CoordTableTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    private static AppCoordRow Plain(string cop = "VALMA") =>
        new(cop, "FL200", "LIRR_CTR", TransferFlowKind.Arrival);

    private IRenderedComponent<CoordTable> Render(params AppCoordRow[] rows) =>
        RenderComponent<CoordTable>(p => p.Add(x => x.Rows, rows));

    [Fact]
    public void A_plain_table_keeps_the_four_historic_columns()
    {
        // L'invariante che protegge le decine di tabelle ACC↔ACC già pubblicate: senza faccetta, niente cambia.
        var headers = Render(Plain()).FindAll("thead th").Select(th => th.TextContent).ToList();
        Assert.Equal(new[] { "CoP", "AppCoord_Level", "AppCoord_Next" }, headers);
    }

    [Fact]
    public void Facet_columns_appear_only_when_a_row_fills_them()
    {
        var conFaccetta = Plain() with { Handoff = "al confine dell'AoR", HandoffLevel = "passando FL110" };
        var headers = Render(conFaccetta).FindAll("thead th").Select(th => th.TextContent).ToList();

        Assert.Contains("Coord_Handoff", headers);
        Assert.Contains("Coord_HandoffLevel", headers);
        // Con due livelli, «Livello» non basta più a dire quale: diventa «Autorizzato».
        Assert.Contains("Coord_Cleared", headers);
        Assert.DoesNotContain("AppCoord_Level", headers);
        // Velocità e comunicazioni non le compila nessuno: non devono comparire come colonne vuote.
        Assert.DoesNotContain("Coord_Speed", headers);
        Assert.DoesNotContain("Coord_Comms", headers);
    }

    [Fact]
    public void Cop_header_becomes_via_when_the_transfer_is_elsewhere()
    {
        // Senza faccetta il CoP è ingresso E trasferimento; con la faccetta è solo l'ingresso, cioè il «via».
        var headers = Render(Plain() with { Handoff = "su AVN" }).FindAll("thead th").Select(th => th.TextContent).ToList();
        Assert.Contains("Coord_Via", headers);
        Assert.DoesNotContain("CoP", headers);
    }

    [Fact]
    public void Variant_rows_share_cop_and_receiver_in_one_cell()
    {
        var head = Plain("BIRSU") with { FlowId = 7, VariantGroup = 1, ConditionLabel = "07", Level = "FL150-" };
        var exc = Plain("BIRSU") with
        {
            FlowId = 7, VariantGroup = 1, VariantDepth = 1, ConditionLabel = "R403B", Level = "FL130-",
        };

        var t = Render(head, exc);
        var rows = t.FindAll("tbody tr").ToList();
        Assert.Equal(2, rows.Count);

        // CoP e ricevente scritti una volta sola, in rowspan: ciò che resta per riga è il delta.
        var capofila = rows[0].QuerySelectorAll("td").ToList();
        Assert.Equal("2", capofila[0].GetAttribute("rowspan"));      // CoP
        Assert.Equal("BIRSU", capofila[0].TextContent);
        Assert.Equal("2", capofila[2].GetAttribute("rowspan"));      // ricevente
        // L'eccezione porta solo le celle che cambiano, e rientra.
        Assert.Equal(2, rows[1].QuerySelectorAll("td").Count());
        Assert.Contains("coord-variant", rows[1].GetAttribute("class"));
        Assert.Contains("padding-left", rows[1].QuerySelectorAll("td").Last().GetAttribute("style") ?? "");
    }

    [Fact]
    public void Peer_alternatives_are_not_indented_and_not_tinted()
    {
        // Pista 07 e pista 25 sono pari-grado: nessuna è lo standard dell'altra, quindi nessuna delle due
        // deve sembrare la continuazione dell'altra.
        var a = Plain("BIRSU") with { FlowId = 7, VariantGroup = 1, ConditionLabel = "07", Level = "FL150-" };
        var b = Plain("BIRSU") with { FlowId = 7, VariantGroup = 1, ConditionLabel = "25", Level = "FL130-" };

        var rows = Render(a, b).FindAll("tbody tr").ToList();
        Assert.Contains("coord-variant-head", rows[0].GetAttribute("class"));
        Assert.Contains("coord-variant-alt", rows[1].GetAttribute("class"));
        Assert.Null(rows[1].QuerySelectorAll("td").Last().GetAttribute("style"));
    }

    [Fact]
    public void The_group_wide_row_is_rendered_last_whatever_the_order()
    {
        // Chi scavalca le alternative non appartiene a nessuna: si legge dopo i casi che scavalca, anche se
        // nel flusso sta prima. Il marcatore arriva già scritto in ConditionLabel, nella lingua del template.
        var wide = Plain("BIRSU") with
        {
            FlowId = 7, VariantGroup = 1, IsGroupWide = true, Level = "FL90-", ConditionLabel = "in ogni caso · notte",
        };
        var head = Plain("BIRSU") with { FlowId = 7, VariantGroup = 1, ConditionLabel = "07", Level = "FL150-" };

        var t = Render(wide, head);
        var rows = t.FindAll("tbody tr").ToList();
        var celle = rows.Select(r => r.QuerySelectorAll("td").Last().TextContent).ToList();
        Assert.Equal(new[] { "07", "in ogni caso · notte" }, celle);
        Assert.Contains("coord-variant-wide", rows[1].GetAttribute("class"));
    }

    [Fact]
    public void Nesting_order_inside_a_block_is_never_reordered()
    {
        // ⚠️ In un outline l'ordine È la struttura: riordinare una riga la riassegnerebbe a un'altra capofila.
        // Solo le righe che scavalcano si spostano, perché non appartengono a nessuna.
        var a = Plain("BIRSU") with { FlowId = 7, VariantGroup = 1, ConditionLabel = "07", Level = "FL150-" };
        var aExc = Plain("BIRSU") with { FlowId = 7, VariantGroup = 1, VariantDepth = 1, ConditionLabel = "R403B", Level = "FL130-" };
        var b = Plain("BIRSU") with { FlowId = 7, VariantGroup = 1, ConditionLabel = "25", Level = "FL130-" };

        var celle = Render(a, aExc, b).FindAll("tbody tr")
            .Select(r => r.QuerySelectorAll("td").Last().TextContent).ToList();
        Assert.Equal(new[] { "07", "R403B", "25" }, celle);
    }

    [Fact]
    public void Same_group_number_in_different_flows_stays_separate()
    {
        // Il numero di gruppo è progressivo PER FLUSSO: senza la chiave (flusso, gruppo) due accordi diversi
        // finirebbero fusi in un unico blocco con un rowspan che copre righe che non c'entrano.
        var a = Plain("BIRSU") with { FlowId = 7, VariantGroup = 1 };
        var b = Plain("PISIP") with { FlowId = 9, VariantGroup = 1 };

        var rows = Render(a, b).FindAll("tbody tr");
        Assert.All(rows, r => Assert.Null(r.QuerySelectorAll("td")[0].GetAttribute("rowspan")));
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void English_labels_ignore_the_localizer()
    {
        // Le vLOA sono in inglese a prescindere dalla cultura della pagina: il localizer qui non deve entrarci.
        var t = RenderComponent<CoordTable>(p => p
            .Add(x => x.Rows, new[] { Plain() with { Speed = "at 250 kt or less" } })
            .Add(x => x.English, true));

        var headers = t.FindAll("thead th").Select(th => th.TextContent).ToList();
        Assert.Contains("Speed", headers);
        Assert.Contains("Level", headers);
        Assert.DoesNotContain(headers, h => h.StartsWith("Coord_") || h.StartsWith("AppCoord_"));
    }

    // ---- prosa: nasce chiusa, sopra la tabella ----

    private static AppCoordRow Said(string cop, int clauseId, string sentence, string? lead = null) =>
        new(cop, "FL200", "LIRR_CTR", TransferFlowKind.Arrival) { ClauseId = clauseId, Sentence = sentence, LeadSentence = lead };

    [Fact]
    public void Prose_is_collapsed_and_holds_one_paragraph_per_clause()
    {
        // Chi consulta il documento in cuffia legge la TABELLA: la prosa distesa lo obbligava a scorrere oltre
        // decine di paragrafi per arrivarci. Resta a un clic, e il riassunto dice quanta ce n'e'.
        var cut = Render(Said("VALMA", 1, "Prima frase."), Said("PISIP", 2, "Seconda frase."));

        var prosa = cut.Find("details.coord-prose");
        Assert.Null(prosa.GetAttribute("open"));
        Assert.Equal(new[] { "Prima frase.", "Seconda frase." },
                     cut.FindAll("details.coord-prose p.coord-sentence").Select(x => x.TextContent));
        Assert.Contains("Coord_Prose", prosa.QuerySelector("summary")!.TextContent);
    }

    [Fact]
    public void One_sentence_asks_for_the_singular_key()
    {
        // ⚠️ Una sola forma plurale sbaglia sempre sull'uno, in tutte e due le lingue.
        var cut = Render(Said("VALMA", 1, "Frase sola."));
        Assert.Contains("Coord_Prose_One", cut.Find("details.coord-prose summary").TextContent);
    }

    [Fact]
    public void A_table_without_sentences_has_no_prose_block()
    {
        // Nessuna frase = nessun blocco: un riassunto che apre il vuoto e' un invito a un clic sprecato.
        Assert.Empty(Render(Plain()).FindAll("details.coord-prose"));
    }

    [Fact]
    public void Lead_mode_collapses_the_single_leading_sentence()
    {
        // In modo capofila la prosa e' una frase sola: il blocco e' lo stesso, cosi' i due modi non divergono.
        var cut = RenderComponent<CoordTable>(p => p
            .Add(x => x.Rows, new[] { Said("VALMA", 1, "Distesa.", "Capofila."), Said("PISIP", 2, "Distesa due.", "Capofila.") })
            .Add(x => x.LeadSentence, true));

        var frasi = cut.FindAll("details.coord-prose p.coord-sentence");
        Assert.Equal("Capofila.", Assert.Single(frasi).TextContent);
    }

    [Fact]
    public void English_prose_summary_never_falls_back_to_the_ui_culture()
    {
        // Nelle vLOA l'intestazione e' inglese a prescindere dalla cultura: il riassunto non fa eccezione.
        var cut = RenderComponent<CoordTable>(p => p
            .Add(x => x.Rows, new[] { Said("VALMA", 1, "Una."), Said("PISIP", 2, "Due.") })
            .Add(x => x.English, true));

        Assert.Equal("Full text (2 sentences)", cut.Find("details.coord-prose summary").TextContent.Trim('▸', ' '));
    }

    [Fact]
    public void The_title_shares_the_line_with_the_prose_cue()
    {
        // Il punto del titolo dentro il cartiglio: una riga sola dove prima ce n'erano due, e in un documento
        // con decine di tabelle le righe risparmiate sono decine.
        var cut = RenderComponent<CoordTable>(p => p
            .Add(x => x.Rows, new[] { Said("VALMA", 1, "Una.") })
            .Add(x => x.Title, "Arrivi"));

        var summary = cut.Find("details.coord-prose > summary");
        Assert.Equal("Arrivi", summary.QuerySelector(".coord-kind")!.TextContent);
        Assert.Contains("Coord_Prose_One", summary.QuerySelector(".prose-cue")!.TextContent);
        Assert.Empty(cut.FindAll("p.coord-kind"));   // niente paragrafo per sé
    }

    [Fact]
    public void Without_prose_the_title_survives_on_its_own()
    {
        // Una tabella che perde l'intestazione perche' la sua prosa e' vuota sarebbe un effetto collaterale.
        var cut = RenderComponent<CoordTable>(p => p
            .Add(x => x.Rows, new[] { Plain() })
            .Add(x => x.Title, "Partenze"));

        Assert.Empty(cut.FindAll("details.coord-prose"));
        Assert.Equal("Partenze", cut.Find("p.coord-kind").TextContent);
    }

    // ---- il verso: chi cede e chi riceve (24 agosto 2026) ----
    //
    // L'albero dei coordinamenti raggruppa per settore → ACC → aeroporto/tipo, e la direzione non è una chiave
    // di raggruppamento: un nodo può portare i due versi insieme. Misurato sui flussi veri: «Sorvoli · Zagabria»
    // del blocco LIBB porta 8 righe entranti e 6 uscenti.

    private static AppCoordRow Incoming(string cop = "AIOSA") =>
        Plain(cop) with { IsIncoming = true };

    [Fact]
    public void An_outgoing_only_table_is_untouched()
    {
        // L'invariante che protegge le tabelle già pubblicate: dove il verso è uno solo non cambia niente,
        // intestazione compresa.
        var cut = Render(Plain("VALMA"), Plain("PISIP"));

        Assert.Single(cut.FindAll("table.coord-table"));
        Assert.Equal("AppCoord_Next", cut.FindAll("thead th").Last().TextContent);
    }

    [Fact]
    public void An_incoming_only_table_says_who_hands_the_traffic_over()
    {
        // La cella porta chi CONSEGNA: sotto «Prossimo» diceva il contrario di quello che c'è scritto.
        var cut = Render(Incoming("AIOSA"), Incoming("BEVIS"));

        Assert.Single(cut.FindAll("table.coord-table"));
        Assert.Equal("AppCoord_From", cut.FindAll("thead th").Last().TextContent);
    }

    [Fact]
    public void A_mixed_node_splits_into_two_tables_one_per_direction()
    {
        var cut = Render(Plain("VALMA"), Incoming("AIOSA"), Plain("PISIP"));

        var tabelle = cut.FindAll("table.coord-table").ToList();
        Assert.Equal(2, tabelle.Count);

        // Prima ciò che cediamo, poi ciò che riceviamo, ognuna con la propria intestazione.
        Assert.Equal("AppCoord_Next", tabelle[0].QuerySelectorAll("thead th").Last().TextContent);
        Assert.Equal("AppCoord_From", tabelle[1].QuerySelectorAll("thead th").Last().TextContent);

        // Le righe non si mescolano: due uscenti nella prima, una entrante nella seconda.
        Assert.Equal(2, tabelle[0].QuerySelectorAll("tbody tr").Length);
        Assert.Single(tabelle[1].QuerySelectorAll("tbody tr"));
    }

    [Fact]
    public void A_split_node_names_the_two_directions_in_the_existing_title_row()
    {
        // Il titolo sta già dentro il cartiglio della prosa: la parola del verso non si prende una riga per sé,
        // e il taglio costa UNA riga in tutto — il secondo <summary>.
        var cut = RenderComponent<CoordTable>(p => p
            .Add(x => x.Rows, new[] { Said("VALMA", 1, "Cediamo."), Said("AIOSA", 2, "Riceviamo.") with { IsIncoming = true } })
            .Add(x => x.Title, "Arrivi"));

        var titoli = cut.FindAll("details.coord-prose > summary .coord-kind").Select(x => x.TextContent).ToList();
        Assert.Equal(new[] { "Arrivi · Coord_WeHandOver", "Arrivi · Coord_WeReceive" }, titoli);
        Assert.Empty(cut.FindAll("p.coord-kind"));   // nessun paragrafo di titolo in più
    }

    [Fact]
    public void Without_a_title_the_direction_word_stands_alone_capitalised()
    {
        // I nodi «Sorvoli» hanno già il nome nel proprio <summary> e non passano un titolo: lì la parola del
        // verso è tutto il titolo, e deve leggersi come tale.
        var cut = RenderComponent<CoordTable>(p => p
            .Add(x => x.Rows, new[] { Said("VALMA", 1, "Cediamo."), Said("AIOSA", 2, "Riceviamo.") with { IsIncoming = true } })
            .Add(x => x.English, true));

        var titoli = cut.FindAll("details.coord-prose > summary .coord-kind").Select(x => x.TextContent).ToList();
        Assert.Equal(new[] { "We hand over", "We receive" }, titoli);
    }

    [Fact]
    public void Each_direction_computes_its_own_optional_columns()
    {
        // Le colonne si mostrano per presenza di dati, e la presenza è quella della SEZIONE: una colonna che
        // riempiono solo le righe entranti comparirebbe vuota in tutta la tabella delle uscenti.
        var cut = Render(Plain("VALMA"), Incoming("AIOSA") with { Speed = "a 250 kt o inferiore" });

        var tabelle = cut.FindAll("table.coord-table").ToList();
        Assert.DoesNotContain("Coord_Speed", tabelle[0].QuerySelectorAll("thead th").Select(th => th.TextContent));
        Assert.Contains("Coord_Speed", tabelle[1].QuerySelectorAll("thead th").Select(th => th.TextContent));
    }

    [Fact]
    public void Each_direction_gets_its_own_lead_sentence()
    {
        // La capofila introduce la tabella: una sola per un nodo misto ne annuncerebbe un verso e mentirebbe
        // sull'altro.
        var cut = RenderComponent<CoordTable>(p => p
            .Add(x => x.Rows, new[]
            {
                Said("VALMA", 1, "Distesa uscente.", "TS trasferisce a ES."),
                Said("AIOSA", 2, "Distesa entrante.", "TS riceve da ES.") with { IsIncoming = true },
            })
            .Add(x => x.LeadSentence, true));

        Assert.Equal(new[] { "TS trasferisce a ES.", "TS riceve da ES." },
                     cut.FindAll("p.coord-sentence").Select(x => x.TextContent));
    }
}
