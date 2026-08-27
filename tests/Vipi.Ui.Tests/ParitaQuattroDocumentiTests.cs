using Microsoft.AspNetCore.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>La prova di parità dei quattro documenti</b> (doc 14 §5), e vale più degli otto passi che la precedono.
///
/// <para>
/// Il catalogo delle sezioni aveva già invarianti provate su TUTTI i profili, ed è la ragione per cui quella
/// parte del sistema non è divergente: una decisione sbagliata su un profilo diventa rossa subito. Per il
/// <b>comportamento</b> non esisteva l'equivalente. Nessun test chiedeva alle quattro famiglie la stessa cosa,
/// e ogni divergenza trovata dall'audit del 27 agosto 2026 era passata attraverso una suite verde.
/// </para>
///
/// <para>
/// Queste prove girano sullo stesso componente con i quattro profili, così una regola che valesse solo per tre
/// non può più restare nascosta. Chi aggiungesse un quinto documento le eredita: basta aggiungere il profilo
/// a <see cref="Profili"/>.
/// </para>
/// </summary>
public class ParitaQuattroDocumentiTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public ParitaQuattroDocumentiTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());

    /// <summary>I profili dei quattro documenti previsti da direttiva. La vIPI ACC ne ha due — è l'unica a
    /// blocchi — e ci sono tutti e due: quel che vale per un documento deve valere per entrambi i suoi.</summary>
    public static TheoryData<SectionProfile> Profili => new()
    {
        SectionProfile.AccAerovia,
        SectionProfile.AccAppBlock,
        SectionProfile.App,
        SectionProfile.Vloa,
        SectionProfile.Airport,
    };

    private static SectionView Sezione(string key, string titolo, bool nascosta = false,
        IReadOnlyList<BlockView>? blocchi = null) => new()
        {
            Id = $"s-{key}",
            Title = titolo,
            Depth = 0,
            SectionKey = key,
            IsHidden = nascosta,
            Blocks = blocchi ?? Array.Empty<BlockView>(),
            Children = Array.Empty<SectionView>(),
        };

    private static BlockView Prosa(string testo) => new()
    {
        Id = 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = testo,
    };

    private IRenderedComponent<DocumentSectionsView> Rendi(SectionProfile profilo,
        IReadOnlyList<SectionView> sezioni, bool bozza = false) =>
        RenderComponent<DocumentSectionsView>(p => p
            .Add(x => x.Sections, sezioni)
            .Add(x => x.Profile, profilo)
            .Add(x => x.IsDraft, bozza)
            .Add(x => x.DerivedContent, (RenderFragment<SectionView>)(s => b =>
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "class", "corpo-derivato");
                b.AddContent(2, $"corpo di {s.SectionKey}");
                b.CloseElement();
            })));

    /// <summary>La prima chiave che il catalogo dichiara «resa dalla pagina» per questo profilo.</summary>
    private static string ChiaveResaDallaPagina(SectionProfile p) =>
        SectionCatalog.For(p).First(d => SectionCatalog.IsHostRendered(p, d.Key)).Key;

    // ---------------------------------------------------------------------------------------------------
    // 1. Una sezione nascosta sta fuori dal pubblico, e in bozza si vede marcata.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Profili))]
    public void Una_sezione_nascosta_non_esce_in_pubblico(SectionProfile profilo)
    {
        var cut = Rendi(profilo, new[] { Sezione("libera-1", "Segreta", nascosta: true, blocchi: new[] { Prosa("testo riservato") }) });

        Assert.DoesNotContain("Segreta", cut.Markup);
        Assert.DoesNotContain("testo riservato", cut.Markup);
    }

    [Theory]
    [MemberData(nameof(Profili))]
    public void Una_sezione_nascosta_in_bozza_si_vede_MARCATA(SectionProfile profilo)
    {
        var cut = Rendi(profilo, new[] { Sezione("libera-1", "Segreta", nascosta: true, blocchi: new[] { Prosa("testo riservato") }) }, bozza: true);

        Assert.Contains("Segreta", cut.Markup);
        Assert.Contains("Common_HiddenNotPublic", cut.Markup);   // la pill che dice «fuori dal pubblico»
        Assert.Contains("opacity:.65", cut.Markup);
    }

    [Theory]
    [MemberData(nameof(Profili))]
    public void Anche_una_sezione_RESA_DALLA_PAGINA_se_nascosta_sparisce(SectionProfile profilo)
    {
        // ⚠️ Il caso che sfuggiva più facilmente: le derivate hanno un corpo che viene da fuori il documento,
        // e un ramo che le disegnasse prima di guardare «nascosta» le pubblicherebbe lo stesso.
        var chiave = ChiaveResaDallaPagina(profilo);
        var cut = Rendi(profilo, new[] { Sezione(chiave, "Derivata", nascosta: true) });

        Assert.DoesNotContain("corpo-derivato", cut.Markup);
    }

    // ---------------------------------------------------------------------------------------------------
    // 2. Chi rende il corpo lo dice il CATALOGO, per profilo — non la pagina.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Profili))]
    public void Ogni_sezione_del_catalogo_resa_dalla_pagina_riceve_il_corpo_derivato(SectionProfile profilo)
    {
        // È la prova che chiude il difetto della vLOA: là le chiavi rese dalla pagina erano tre, scritte a mano
        // in una catena di `if`, e una quarta aggiunta al profilo sarebbe comparsa nell'editor ma non nel
        // documento pubblicato. Qui si chiede al catalogo, e si pretende che il corpo arrivi per TUTTE.
        foreach (var d in SectionCatalog.For(profilo).Where(d => SectionCatalog.IsHostRendered(profilo, d.Key)))
        {
            var cut = Rendi(profilo, new[] { Sezione(d.Key, d.Title) });
            Assert.Contains($"corpo di {d.Key}", cut.Markup);
        }
    }

    [Theory]
    [MemberData(nameof(Profili))]
    public void Una_sezione_libera_rende_i_suoi_blocchi_e_non_il_corpo_derivato(SectionProfile profilo)
    {
        var cut = Rendi(profilo, new[] { Sezione("libera-1", "Note di reparto", blocchi: new[] { Prosa("prosa scritta a mano") }) });

        Assert.Contains("prosa scritta a mano", cut.Markup);
        Assert.DoesNotContain("corpo-derivato", cut.Markup);
    }

    // ---------------------------------------------------------------------------------------------------
    // 3. «Validità e revisione»: scheda dalla pagina E testo scritto a mano, in tutte e quattro.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Profili))]
    public void La_validita_ha_la_scheda_E_i_blocchi_in_tutte_le_famiglie(SectionProfile profilo)
    {
        var cut = Rendi(profilo, new[] { Sezione("validity", "Validità", blocchi: new[] { Prosa("firmatario e ciclo di revisione") }) });

        Assert.Contains("corpo di validity", cut.Markup);                 // la scheda, dalla pagina
        Assert.Contains("firmatario e ciclo di revisione", cut.Markup);   // il testo, dal documento
    }

    // ---------------------------------------------------------------------------------------------------
    // 4. Le sezioni che nascono chiuse lo fanno ovunque, e lo dice il catalogo.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Profili))]
    public void Chi_nasce_chiusa_nasce_chiusa_in_tutte_le_famiglie(SectionProfile profilo)
    {
        foreach (var d in SectionCatalog.For(profilo))
        {
            var cut = Rendi(profilo, new[] { Sezione(d.Key, d.Title) });
            var details = cut.Find("details");
            var aperta = details.HasAttribute("open");
            Assert.Equal(!SectionCatalog.IsInitiallyCollapsed(d.Key), aperta);
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // 5. L'ordine è quello del DOCUMENTO, non quello del catalogo.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Profili))]
    public void Le_sezioni_escono_nell_ordine_del_documento(SectionProfile profilo)
    {
        // doc 11 §3b: si itera la lista di sezioni del documento. Se una pagina riordinasse per catalogo,
        // il riordino editoriale — frecce e trascinamento — non si vedrebbe nel pubblicato.
        var catalogo = SectionCatalog.For(profilo).ToList();
        var alRovescio = catalogo.AsEnumerable().Reverse()
            .Select(d => Sezione(d.Key, d.Title)).ToList();

        var cut = Rendi(profilo, alRovescio);

        // ⚠️ Si cerca l'ANCORA, non il titolo: «METAR & TAF» esce dal markup come «METAR &amp; TAF» e un
        // confronto sul titolo fallirebbe per l'escaping, non per l'ordine.
        var posizioni = alRovescio.Select(s => cut.Markup.IndexOf($"id=\"{s.Id}\"", StringComparison.Ordinal)).ToList();
        Assert.All(posizioni, p => Assert.True(p >= 0));
        Assert.Equal(posizioni.OrderBy(x => x), posizioni);   // già in ordine crescente = ordine del documento
    }
}
