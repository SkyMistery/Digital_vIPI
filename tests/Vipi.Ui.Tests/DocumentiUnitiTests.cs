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
        var membro = new UnionMemberView(MemberId: documentId, Order: 1, IsHost: false, doc);
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

    // ---- La chip «Tutto · Pilota · ATC» è dell'UNIONE, non dell'ospite -------------------------------

    /// <summary>
    /// 🔴 Il difetto del 7 settembre 2026: unendo due documenti di cui <b>solo il secondo</b> ha sezioni
    /// marcate, i tre comandi sparivano dal viewer. La chip è una per pagina e la disegna l'ospite, che si
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

        Assert.True(MembroUnito.QualcunoHaMarcate(ospite: false, altri));
    }

    [Fact]
    public void Senza_marcate_da_NESSUNA_parte_la_chip_resta_spenta()
    {
        // L'altra metà della regola: la chip non deve comparire su ogni pagina unita solo perché è unita.
        var altri = new[] { Membro(3, ReleaseTargetType.App, "LIBA_APP", "Amendola Approach") };

        Assert.False(MembroUnito.QualcunoHaMarcate(ospite: false, altri));
        Assert.False(MembroUnito.QualcunoHaMarcate(ospite: false, Array.Empty<MembroUnito>()));
    }

    /// <summary>
    /// ⚠️ <b>La guardia che vale davvero</b>: le tre asserzioni qui sopra provano la regola, ma la regola
    /// non serve a niente se una pagina torna a chiederla al solo ospite. Quel ritorno non darebbe nessun
    /// errore — <c>_doc.HaMarcate</c> compila e vale <c>false</c> — e sarebbe di nuovo il difetto.
    /// <para>Le tre sedi sono quelle che possono OSPITARE un'unione: la vIPI ACC e la vLOA restano fuori
    /// dalle famiglie unibili, dichiarato in carta, e la loro chip guarda il proprio documento e basta.</para>
    /// </summary>
    [Theory]
    [InlineData("Pages/AeroportoPage.razor")]
    [InlineData("Pages/AppnPage.razor")]
    [InlineData("Pages/MilDocumentPage.razor")]
    public void Ogni_pagina_che_OSPITA_chiede_la_chip_all_unione_intera(string relativo)
    {
        var sorgente = Leggi(relativo);

        var chip = System.Text.RegularExpressions.Regex.Match(sorgente, @"<AudienceChip\b[^>]*>");
        Assert.True(chip.Success, $"{relativo}: nessuna <AudienceChip …>.");

        Assert.Contains("MembroUnito.QualcunoHaMarcate(", chip.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Visibile=\"_doc.HaMarcate\"", chip.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 Chi NON è l'ospite deve saperlo, e sapere dove andare (segnalazione del committente, 7 settembre
    /// 2026). Invertendo l'ordine dei membri l'ospite CAMBIA, e con lui si sposta l'editor unito: i corpi
    /// degli altri membri smettono di comparire in questa pagina. Senza il rimando sembra che l'unione si
    /// sia persa — e l'archivio invece è intatto.
    /// </summary>
    [Fact]
    public void Chi_non_e_l_OSPITE_lo_legge_e_ha_il_link_per_andarci()
    {
        var sorgente = Leggi("Components/Doc/UnionPanel.razor");

        Assert.Contains("@if (!SonoOspite)", sorgente, StringComparison.Ordinal);
        Assert.Contains("Union_NotHost", sorgente, StringComparison.Ordinal);
        // Il rimando è un LINK all'editor dell'ospite, non una frase che dice «vai di là»: chi legge deve
        // poterci arrivare senza sapere com'è fatto l'indirizzo.
        Assert.Contains("_indirizzoOspite", sorgente, StringComparison.Ordinal);
        Assert.Contains("Union_OpenHost", sorgente, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ La scheda delle sezioni in comune scrive sui documenti DEGLI ALTRI membri, e i loro lock li tiene
    /// l'editor dell'ospite: offerta altrove, cadrebbe con «bloccato da un altro redattore». Sta dove il
    /// gesto può riuscire.
    /// </summary>
    [Fact]
    public void La_scheda_delle_comuni_si_offre_SOLO_all_ospite()
    {
        var sorgente = Leggi("Components/Doc/UnionPanel.razor");

        Assert.Contains("@if (IsEditing && SonoOspite)", sorgente, StringComparison.Ordinal);
        // E chi sposta il primo dev'essere avvisato PRIMA di premere: sposta anche la pagina e l'editor.
        Assert.Contains("Union_FirstIsHost", sorgente, StringComparison.Ordinal);
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
    public void Le_marcate_dell_OSPITE_bastano_da_sole()
    {
        // Il caso che funzionava già, e che il rimedio non deve rompere: documento solo, o ospite marcato
        // con membri che non lo sono.
        Assert.True(MembroUnito.QualcunoHaMarcate(ospite: true, Array.Empty<MembroUnito>()));
        Assert.True(MembroUnito.QualcunoHaMarcate(
            ospite: true, new[] { Membro(3, ReleaseTargetType.App, "LIBA_APP", "Amendola Approach") }));
    }
}
