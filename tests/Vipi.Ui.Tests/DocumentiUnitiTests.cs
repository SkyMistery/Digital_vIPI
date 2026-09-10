using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Components;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La resa di una pagina UNITA (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c> §3): più documenti
/// in una pagina sola, ognuno col proprio indice e la propria intestazione.
///
/// <para>⚠️ Le due cose che questi test tengono ferme sono <b>l'intestazione</b> e <b>l'ancora</b>. La prima
/// non è decorazione: le sezioni con la stessa chiave restano tutte e due — «Frequenze ATC/CRC» di un vSOP
/// militare e «Frequenze» di un avvicinamento non sono la stessa cosa — ed è l'intestazione a dire di quale
/// documento sono. La seconda è dove atterra chi arriva da una vecchia URL, e un'ancora sbagliata non dà
/// errore: porta in cima alla pagina e basta.</para>
/// </summary>
public class DocumentiUnitiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public DocumentiUnitiTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
    }

    private static SectionView Sez(string id, string titolo) => new()
    {
        Id = id,
        Title = titolo,
        Depth = 0,
        SectionKey = titolo.ToLowerInvariant(),
        Blocks = Array.Empty<BlockView>(),
        Children = Array.Empty<SectionView>(),
    };

    private static MembroUnito Membro(int documentId, ReleaseTargetType tipo, string chiave, string titolo,
                                      params SectionView[] sezioni)
        => Membro(documentId, tipo, chiave, titolo, haMarcate: false, sezioni);

    private static MembroUnito Membro(int documentId, ReleaseTargetType tipo, string chiave, string titolo,
                                      bool haMarcate, params SectionView[] sezioni)
    {
        var doc = new ManagedDoc(tipo, titolo, chiave, "LIRR", IsPublished: true, HasDraft: false,
                                 IsHidden: false, tipo, chiave, documentId);
        var membro = new UnionMemberView(MemberId: documentId, Order: 1, doc);
        return new MembroUnito(membro, titolo, sezioni, haMarcate,
            b => b.AddMarkupContent(0, $"<p class=\"corpo\">{titolo}</p>"));
    }

    [Fact]
    public void Ogni_membro_porta_la_sua_INTESTAZIONE_e_il_suo_corpo()
    {
        var cut = RenderComponent<UnionBodies>(p => p.Add(x => x.Membri, new[]
        {
            Membro(3, ReleaseTargetType.App, "LIBV_APP", "Gioia del Colle Approach", Sez("s-1", "Frequenze")),
            Membro(5, ReleaseTargetType.App, "LIBV_G_APP", "Gioia del Colle Approach G", Sez("s-2", "Frequenze")),
        }));

        var intestazioni = cut.FindAll("h2.union-part-h").Select(h => h.TextContent.Trim()).ToArray();
        // Due sezioni «Frequenze» nella stessa pagina non sono un doppione da togliere: sono di due
        // documenti diversi, e a dirlo è l'intestazione.
        Assert.Equal(new[] { "Gioia del Colle Approach", "Gioia del Colle Approach G" }, intestazioni);
        Assert.Equal(2, cut.FindAll("p.corpo").Count);
    }

    [Fact]
    public void L_ancora_del_gruppo_e_l_ID_DEL_DOCUMENTO()
    {
        var cut = RenderComponent<UnionBodies>(p => p.Add(x => x.Membri, new[]
        {
            Membro(3, ReleaseTargetType.App, "LIBV_APP", "Avvicinamento"),
        }));

        // ⚠️ Sull'id del DOCUMENTO e non sulla posizione: l'ordine dei membri si cambia con due frecce, e
        // un'ancora che cambia insieme all'ordine è un collegamento salvato che un giorno porta altrove.
        Assert.NotNull(cut.Find("section#doc-3"));
        Assert.Equal("doc-3", MembroUnito.AncoraDi(3));
    }

    /// <summary>
    /// Un GRUPPO per membro, intestato col titolo del suo documento.
    ///
    /// <para>⚠️ Erano <b>indici impilati</b>, uno per membro, ognuno col proprio
    /// <c>&lt;aside class="toc"&gt;</c>. Dal 6 settembre 2026 sono gruppi dentro l'<b>unico</b> sommario, e
    /// non è estetica: due riquadri impilati vanno avvolti in un <c>&lt;div&gt;</c>, quel <c>&lt;div&gt;</c>
    /// diventa il genitore del <c>position:sticky</c> ed è alto quanto la barra — quindi il sommario se ne
    /// andava in cima appena si scorreva. Misurato su tre documenti su cinque.</para>
    /// </summary>
    [Fact]
    public void Un_GRUPPO_per_membro_intestato_col_titolo_del_suo_documento()
    {
        var gruppi = TocGruppiUnione.Di(new[]
        {
            Membro(24, ReleaseTargetType.AirportMil, "LIBV", "vSOP MIL — LIBV", Sez("s-9", "Dati generali")),
            Membro(3, ReleaseTargetType.App, "LIBV_APP", "Avvicinamento", Sez("s-1", "Separazioni")),
        }, bozza: false).ToList();

        var cut = RenderComponent<DocumentToc>(p => p.Add(x => x.Gruppi, gruppi));

        // Un elenco di ventisei voci militari seguito da dieci d'avvicinamento, senza una riga che dica dove
        // finisce l'uno e comincia l'altro, è un indice che non aiuta a cercare — l'unico suo mestiere.
        var titoli = cut.FindAll("p.toc-grp").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "vSOP MIL — LIBV", "Avvicinamento" }, titoli);

        // 🔴 E un riquadro SOLO: è la metà che conta.
        Assert.Single(cut.FindAll("aside.toc"));
    }

    [Fact]
    public void L_indice_di_un_membro_punta_all_ancora_che_usa_il_SUO_corpo()
    {
        var gruppi = TocGruppiUnione.Di(new[]
        {
            Membro(3, ReleaseTargetType.App, "LIBV_APP", "Avvicinamento", Sez("s-7", "Separazioni")),
        }, bozza: false).ToList();

        var cut = RenderComponent<DocumentToc>(p => p.Add(x => x.Gruppi, gruppi));

        // ⚠️ Indice e corpo devono usare la STESSA ancora, o le voci puntano a un id che non esiste e non
        // fanno niente — senza errori. Qui si prova che l'indice la chiede al componente-corpo della sua
        // famiglia invece di ricopiarne la formula.
        var href = cut.Find("aside.toc a").GetAttribute("href");
        Assert.Equal("#" + AppDocumentBody.AnchorOf(Sez("s-7", "Separazioni")), href);
    }

    [Fact]
    public void Senza_membri_non_si_disegna_NIENTE()
    {
        // Il caso normale è «documento solo»: la pagina unita non deve lasciare un contenitore vuoto in
        // fondo a ogni documento del sito.
        var corpi = RenderComponent<UnionBodies>(p => p.Add(x => x.Membri, Array.Empty<MembroUnito>()));

        Assert.Empty(TocGruppiUnione.Di(Array.Empty<MembroUnito>(), bozza: false));
        Assert.Empty(corpi.Markup.Trim());
    }

    // ---- La chip «Tutto · Pilota · ATC» è dell'UNIONE, non del solo documento della porta -------------------------------

    /// <summary>
    /// 🔴 Il difetto del 7 settembre 2026: unendo due documenti di cui <b>solo il secondo</b> ha sezioni
    /// marcate, i tre comandi sparivano dal viewer. La chip è una per pagina e la disegna il documento della porta, che si
    /// chiedeva «ho sezioni marcate?» invece di «ce n'è qualcuna in questa pagina?».
    ///
    /// <para>⚠️ Non c'era nessun errore e nessun rosso: il filtro continuava a funzionare scrivendo
    /// <c>?vista=atc</c> a mano — <c>Vista</c> arriva ai membri — e mancava il solo modo di chiederlo.
    /// È la stessa forma dei tre difetti seri della supervisione: una cosa <b>falsa a schermo</b>.</para>
    /// </summary>
    [Fact]
    public void Le_sezioni_marcate_di_un_MEMBRO_tengono_la_chip_accesa()
    {
        var altri = new[]
        {
            Membro(3, ReleaseTargetType.App, "LIBA_APP", "Amendola Approach", haMarcate: true,
                   Sez("s-1", "Separazioni")),
        };

        Assert.True(MembroUnito.QualcunoHaMarcate(dellaPorta: false, altri));
    }

    [Fact]
    public void Senza_marcate_da_NESSUNA_parte_la_chip_resta_spenta()
    {
        // L'altra metà della regola: la chip non deve comparire su ogni pagina unita solo perché è unita.
        var altri = new[] { Membro(3, ReleaseTargetType.App, "LIBA_APP", "Amendola Approach") };

        Assert.False(MembroUnito.QualcunoHaMarcate(dellaPorta: false, altri));
        Assert.False(MembroUnito.QualcunoHaMarcate(dellaPorta: false, Array.Empty<MembroUnito>()));
    }

    /// <summary>
    /// ⚠️ <b>La guardia che vale davvero</b>: le tre asserzioni qui sopra provano la regola, ma la regola
    /// non serve a niente se una pagina torna a chiederla al solo documento della porta. Quel ritorno non darebbe nessun
    /// errore — <c>_doc.HaMarcate</c> compila e vale <c>false</c> — e sarebbe di nuovo il difetto.
    /// <para>Le tre sedi sono quelle che possono disegnare un'unione: la vIPI ACC e la vLOA restano fuori
    /// dalle famiglie unibili, dichiarato in carta, e la loro chip guarda il proprio documento e basta.</para>
    /// </summary>
    [Theory]
    [InlineData("Pages/AeroportoPage.razor")]
    [InlineData("Pages/AppnPage.razor")]
    [InlineData("Pages/MilDocumentPage.razor")]
    public void Ogni_pagina_unibile_chiede_la_chip_all_unione_intera(string relativo)
    {
        var sorgente = Leggi(relativo);

        var chip = System.Text.RegularExpressions.Regex.Match(sorgente, @"<AudienceChip\b[^>]*>");
        Assert.True(chip.Success, $"{relativo}: nessuna <AudienceChip …>.");

        Assert.Contains("MembroUnito.QualcunoHaMarcate(", chip.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Visibile=\"_doc.HaMarcate\"", chip.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ §13 (10 settembre 2026): <b>il concetto di ospite non esiste più</b>, e questa è la guardia che lo
    /// tiene morto. Il pannello non deve tornare a distinguere un membro dagli altri: niente pastiglia
    /// «ospite», niente avviso «non sei tu», niente link per andare altrove — e soprattutto <b>nessuna</b>
    /// delle cinque chiavi di traduzione che quelle righe usavano, che senza questa rete resterebbero in
    /// archivio a descrivere un meccanismo sparito.
    /// </summary>
    [Fact]
    public void Il_pannello_non_nomina_piu_nessun_OSPITE()
    {
        var sorgente = Leggi("Components/Doc/UnionPanel.razor");

        foreach (var morta in new[]
                 {
                     "SonoOspite", "_indirizzoOspite",
                     "Union_NotHost", "Union_OpenHost", "Union_Host", "Union_HostHint", "Union_FirstIsHost",
                 })
            Assert.DoesNotContain(morta, sorgente, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ La scheda delle sezioni in comune si offre da <b>ogni</b> porta, non più dal solo ospite: da §13
    /// ogni membro ha il suo editor unito, prende i lock di tutti e può quindi scrivere sugli altri.
    /// L'unica condizione che resta è quella vera — <b>essere in modifica</b>.
    /// </summary>
    [Fact]
    public void La_scheda_delle_comuni_si_offre_da_OGNI_porta()
    {
        var sorgente = Leggi("Components/Doc/UnionPanel.razor");

        // Il comando c'è, e il suo cancello è il solo `IsEditing`: la riga che lo apriva citando l'ospite è
        // già vietata dalla guardia qui sopra, quindi basta pinnare che il cancello nudo esista.
        Assert.Contains("Union_Common_Open", sorgente, StringComparison.Ordinal);
        Assert.Matches(@"@if \(IsEditing\)\s*\r?\n\s*\{", sorgente);
    }

    /// <summary>
    /// 🔴 <b>La guardia che conta di §13</b>: nessuna delle tre pagine pubbliche deve più reindirizzare a un
    /// altro membro. Le tre asserzioni sulla regola (l'ordine, qui sopra) resterebbero verdi anche se una
    /// pagina tornasse a rimbalzare, perché il rimbalzo sta nella pagina e non nel pezzo puro — e un
    /// documento che sparisce dal proprio indirizzo non lascia traccia da nessuna parte.
    ///
    /// <para>⚠️ Si guarda il SORGENTE delle tre pagine, che è dove il difetto vivrebbe. Le famiglie sono
    /// quelle unibili: la vIPI ACC e la vLOA restano fuori, dichiarato in carta.</para>
    /// </summary>
    [Theory]
    [InlineData("Pages/AeroportoPage.razor")]
    [InlineData("Pages/AppnPage.razor")]
    [InlineData("Pages/MilDocumentPage.razor")]
    public void Nessuna_pagina_rimanda_piu_a_un_altro_membro(string relativo)
    {
        var sorgente = Leggi(relativo);

        Assert.DoesNotContain("IndirizzoDellOspite", sorgente, StringComparison.Ordinal);
        Assert.DoesNotContain("IsHostTarget", sorgente, StringComparison.Ordinal);
        // E la porta si riconosce da FAMIGLIA E CHIAVE insieme: `Di(tipo, chiave)`. Un aeroporto e il suo
        // vSOP militare hanno la stessa chiave di release e si distinguono per il solo tipo.
        Assert.Contains(".Di(ReleaseTargetType.", sorgente, StringComparison.Ordinal);
    }

    private static string Leggi(string relativo) =>
        File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }

    [Fact]
    public void Le_marcate_della_PORTA_bastano_da_sole()
    {
        // Il caso che funzionava già, e che il rimedio non deve rompere: documento solo, o documento della porta marcato
        // con membri che non lo sono.
        Assert.True(MembroUnito.QualcunoHaMarcate(dellaPorta: true, Array.Empty<MembroUnito>()));
        Assert.True(MembroUnito.QualcunoHaMarcate(
            dellaPorta: true, new[] { Membro(3, ReleaseTargetType.App, "LIBA_APP", "Amendola Approach") }));
    }
}
