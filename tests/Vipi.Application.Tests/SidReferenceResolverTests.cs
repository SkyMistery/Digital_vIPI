using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Da quale tabella SID il testo prende i nomi (§A73, carta §4, deciso dal committente il 18 settembre 2026):
/// la pubblica guarda la tabella pubblica dello scalo citato — congelata se la sua release la congela, viva
/// altrimenti —, la bozza guarda la tabella viva, e lo scalo del documento quella che la pagina sta mostrando.
/// </summary>
public class SidReferenceResolverTests
{
    private static AirportSidView Tabella(params string[] nomi) =>
        new(nomi.Select(n => new AirportSidRowView("16", "—", n, "—", "—", "—", "—", "—", "—")).ToList());

    private sealed class SidVive : IAirportSidDerivationService
    {
        public Dictionary<string, AirportSidView> Tabelle { get; } = new();
        public List<string> Chieste { get; } = new();

        public Task<AirportSidView> DeriveAsync(string icao, string? atCycle = null, CancellationToken ct = default)
        {
            Chieste.Add(icao);
            return Task.FromResult(Tabelle.GetValueOrDefault(icao) ?? AirportSidView.Empty);
        }
    }

    private sealed class Congelate : IFrozenSectionReader
    {
        public Dictionary<string, AirportSidView> Tabelle { get; } = new();
        public List<(ReleaseTargetType Tipo, string Chiave)> Chieste { get; } = new();

        public Task<FrozenSections> LoadAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
        {
            Chieste.Add((type, key));
            return Task.FromResult(Tabelle.TryGetValue(key, out var t)
                ? FrozenSections.FromKeys(new Dictionary<string, string> { ["sids"] = JsonSerializer.Serialize(t) })
                : FrozenSections.Empty);
        }
    }

    private static IReadOnlyList<SectionView> Sezioni(params string[] corpi) => new[]
    {
        new SectionView
        {
            Id = "s-1", Title = "Note", Depth = 1, SectionKey = "custom",
            Blocks = corpi.Select((c, i) => new BlockView
            {
                Id = i + 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = c,
            }).ToList(),
            Children = Array.Empty<SectionView>(),
        },
    };

    [Fact]
    public async Task In_pubblica_vince_la_tabella_congelata_dello_scalo_citato()
    {
        var vive = new SidVive { Tabelle = { ["LIRF"] = Tabella("OST3E") } };
        var congelate = new Congelate { Tabelle = { ["LIRF"] = Tabella("OST2E") } };
        var nomi = await new SidReferenceResolver(vive, congelate)
            .PerVistaAsync(Sezioni("[[SID LIRF OST1E]]"), pubblica: true);

        Assert.Equal("OST2E", nomi.Nome("LIRF", "OST?E"));
        Assert.Equal((ReleaseTargetType.Airport, "LIRF"), Assert.Single(congelate.Chieste));
        Assert.Empty(vive.Chieste);
    }

    /// <summary>Sezione SID Live (niente congelato nella release): il pubblico vede la tabella di adesso, e
    /// così il testo.</summary>
    [Fact]
    public async Task In_pubblica_senza_congelato_si_deriva_adesso()
    {
        var vive = new SidVive { Tabelle = { ["LIRF"] = Tabella("OST3E") } };
        var nomi = await new SidReferenceResolver(vive, new Congelate())
            .PerVistaAsync(Sezioni("[[SID LIRF OST1E]]"), pubblica: true);

        Assert.Equal("OST3E", nomi.Nome("LIRF", "OST?E"));
    }

    [Fact]
    public async Task In_bozza_non_si_guarda_il_congelato()
    {
        var vive = new SidVive { Tabelle = { ["LIRF"] = Tabella("OST3E") } };
        var congelate = new Congelate { Tabelle = { ["LIRF"] = Tabella("OST2E") } };
        var nomi = await new SidReferenceResolver(vive, congelate)
            .PerVistaAsync(Sezioni("[[SID LIRF OST1E]]"), pubblica: false);

        Assert.Equal("OST3E", nomi.Nome("LIRF", "OST?E"));
        Assert.Empty(congelate.Chieste);
    }

    /// <summary>Lo scalo del documento usa la tabella che la pagina mostra — al ciclo dell'anteprima, o della
    /// release MILITARE — e non la rilegge. Gli altri scali citati si leggono dalla loro pubblica.</summary>
    [Fact]
    public async Task Lo_scalo_proprio_usa_la_tabella_della_pagina()
    {
        var vive = new SidVive { Tabelle = { ["LIRF"] = Tabella("OST3E"), ["LIRA"] = Tabella("TIBER7A") } };
        var congelate = new Congelate { Tabelle = { ["LIRF"] = Tabella("OST2E") } };
        var nomi = await new SidReferenceResolver(vive, congelate).PerVistaAsync(
            Sezioni("[[SID LIRF OST1E]] e [[SID LIRA TIBER6A]]"), pubblica: true,
            proprioIcao: "lirf", propriaTabella: Tabella("OST9E"));

        Assert.Equal("OST9E", nomi.Nome("LIRF", "OST?E"));
        Assert.Equal("TIBER7A", nomi.Nome("LIRA", "TIBER?A"));
        Assert.DoesNotContain(congelate.Chieste, c => c.Chiave == "LIRF");
        Assert.DoesNotContain("LIRF", vive.Chieste);
    }

    /// <summary>La via di quasi ogni pagina: nessun riferimento, nessuna query.</summary>
    [Fact]
    public async Task Senza_riferimenti_nessuna_query()
    {
        var vive = new SidVive();
        var congelate = new Congelate();
        var nomi = await new SidReferenceResolver(vive, congelate)
            .PerVistaAsync(Sezioni("OST1E scritto a mano."), pubblica: true);

        Assert.Same(NomiSid.Vuoto, nomi);
        Assert.Empty(vive.Chieste);
        Assert.Empty(congelate.Chieste);
    }

    /// <summary>Il selettore dell'editor: una voce per nome — una SID su due piste è una voce sola —, col nome
    /// completo e il riferimento da inserire.</summary>
    [Fact]
    public async Task L_elenco_per_il_selettore_ha_una_voce_per_nome()
    {
        var vive = new SidVive
        {
            Tabelle =
            {
                ["LIBV"] = new(new[]
                {
                    new AirportSidRowView("14R", "CDC", "CDC6A", "—", "—", "—", "—", "—", "—"),
                    new AirportSidRowView("14L", "CDC", "CDC6A", "—", "—", "—", "—", "—", "—"),
                    new AirportSidRowView("32L", "VIENNA", "VIE6B", "—", "—", "—", "—", "—", "—"),
                }),
            },
        };
        var elenco = await new SidReferenceResolver(vive, new Congelate()).ElencoAsync("libv");

        Assert.Equal(2, elenco.Count);
        var cdc = elenco[0];
        Assert.Equal(("CDC 6A", "14L, 14R", "[[SID LIBV CDC6A]]"), (cdc.Esteso, cdc.Piste, cdc.Riferimento));
        Assert.Equal("VIENNA 6B", elenco[1].Esteso);
    }

    [Theory]
    [InlineData("")]
    [InlineData("LIR")]
    [InlineData("LIRFX")]
    public async Task Senza_un_ICAO_valido_l_elenco_e_vuoto_e_non_si_interroga(string icao)
    {
        var vive = new SidVive();
        Assert.Empty(await new SidReferenceResolver(vive, new Congelate()).ElencoAsync(icao));
        Assert.Empty(vive.Chieste);
    }

    /// <summary>Le anteprime dell'editor: la bozza, mai il congelato.</summary>
    [Fact]
    public async Task Sui_testi_dell_editor_si_guarda_la_bozza()
    {
        var vive = new SidVive { Tabelle = { ["LIRF"] = Tabella("OST3E") } };
        var congelate = new Congelate { Tabelle = { ["LIRF"] = Tabella("OST2E") } };
        var nomi = await new SidReferenceResolver(vive, congelate)
            .PerTestiAsync(new[] { null, "Expect [[SID LIRF OST1E]]." });

        Assert.Equal("OST3E", nomi.Nome("LIRF", "OST?E"));
        Assert.Empty(congelate.Chieste);
    }

    [Fact]
    public async Task I_riferimenti_nelle_sotto_sezioni_e_nelle_tabelle_si_trovano()
    {
        var vive = new SidVive { Tabelle = { ["LIBV"] = Tabella("CDC7A") } };
        var figlia = new SectionView
        {
            Id = "s-2", Title = "Tabella", Depth = 2, SectionKey = "custom",
            Blocks = new[]
            {
                new BlockView
                {
                    Id = 9, Format = BlockFormat.Table, State = RenderState.Expanded,
                    BodyJson = """{"columns":["SID"],"rows":[{"cells":["[[SID LIBV CDC6A]]"]}]}""",
                },
            },
            Children = Array.Empty<SectionView>(),
        };
        var padre = new SectionView
        {
            Id = "s-1", Title = "Uscite", Depth = 1, SectionKey = "custom",
            Blocks = Array.Empty<BlockView>(), Children = new[] { figlia },
        };

        var nomi = await new SidReferenceResolver(vive, new Congelate()).PerVistaAsync(new[] { padre }, pubblica: false);
        Assert.Equal("CDC7A", nomi.Nome("LIBV", "CDC?A"));
    }
}
