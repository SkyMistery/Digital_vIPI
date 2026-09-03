using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
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
    {
        var doc = new ManagedDoc(tipo, titolo, chiave, "LIRR", IsPublished: true, HasDraft: false,
                                 IsHidden: false, tipo, chiave, documentId);
        var membro = new UnionMemberView(MemberId: documentId, Order: 1, IsHost: false, doc);
        return new MembroUnito(membro, titolo, sezioni,
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

    [Fact]
    public void Un_INDICE_per_membro_intestato_col_titolo_del_suo_documento()
    {
        var cut = RenderComponent<UnionToc>(p => p.Add(x => x.Membri, new[]
        {
            Membro(24, ReleaseTargetType.AirportMil, "LIBV", "vSOP MIL — LIBV", Sez("s-9", "Dati generali")),
            Membro(3, ReleaseTargetType.App, "LIBV_APP", "Avvicinamento", Sez("s-1", "Separazioni")),
        }));

        // Un elenco di ventisei voci militari seguito da dieci d'avvicinamento, senza una riga che dica dove
        // finisce l'uno e comincia l'altro, è un indice che non aiuta a cercare — l'unico suo mestiere.
        var titoli = cut.FindAll("aside.toc p.toc-h").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "vSOP MIL — LIBV", "Avvicinamento" }, titoli);
    }

    [Fact]
    public void L_indice_di_un_membro_punta_all_ancora_che_usa_il_SUO_corpo()
    {
        var cut = RenderComponent<UnionToc>(p => p.Add(x => x.Membri, new[]
        {
            Membro(3, ReleaseTargetType.App, "LIBV_APP", "Avvicinamento", Sez("s-7", "Separazioni")),
        }));

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
        var toc = RenderComponent<UnionToc>(p => p.Add(x => x.Membri, Array.Empty<MembroUnito>()));
        var corpi = RenderComponent<UnionBodies>(p => p.Add(x => x.Membri, Array.Empty<MembroUnito>()));

        Assert.Empty(toc.Markup.Trim());
        Assert.Empty(corpi.Markup.Trim());
    }
}
