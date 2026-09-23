using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Da quale tabella SID il testo prende i nomi (§A73, carta §4, deciso dal committente il 18 settembre 2026):
/// la pubblica guarda la tabella pubblica dello scalo citato — congelata se la sua release la congela, viva
/// altrimenti —, la bozza guarda la tabella viva, e lo scalo del documento quella che la pagina sta mostrando.
/// </summary>
public class ProcedureReferenceResolverTests
{
    private static AirportSidView Tabella(params string[] nomi) =>
        new(nomi.Select(n => new AirportSidRowView("16", "—", n, "—", "—", "—", "—", "—", "—")).ToList());

    private sealed class SidVive : IAirportSidDerivationService
    {
        // La chiave porta il VERSO: il fake deve poter dire cose diverse per le partenze e per gli arrivi
        // dello stesso scalo, che è esattamente la prova che conta.
        public Dictionary<(ProcedureKind Kind, string Icao), AirportSidView> Tabelle { get; } = new();
        public List<(ProcedureKind Kind, string Icao)> Chieste { get; } = new();
        /// <summary>Il ciclo a cui è stata chiesta ogni tabella, nello stesso ordine di <see cref="Chieste"/>.</summary>
        public List<string?> Cicli { get; } = new();

        public Task<AirportSidView> DeriveAsync(string icao, ProcedureKind kind = ProcedureKind.Sid,
            string? atCycle = null, CancellationToken ct = default)
        {
            Chieste.Add((kind, icao));
            Cicli.Add(atCycle);
            return Task.FromResult(Tabelle.GetValueOrDefault((kind, icao)) ?? AirportSidView.Empty);
        }
    }

    private sealed class Congelate : IFrozenSectionReader
    {
        public Dictionary<string, AirportSidView> Tabelle { get; } = new();
        /// <summary>Le sezioni STAR congelate, per scalo: la release ne porta due, una per verso.</summary>
        public Dictionary<string, AirportSidView> Arrivi { get; } = new();
        public List<(ReleaseTargetType Tipo, string Chiave)> Chieste { get; } = new();

        public Task<FrozenSections> LoadAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
        {
            Chieste.Add((type, key));
            var sezioni = new Dictionary<string, string>();
            if (Tabelle.TryGetValue(key, out var t)) sezioni["sids"] = JsonSerializer.Serialize(t);
            if (Arrivi.TryGetValue(key, out var a)) sezioni["stars"] = JsonSerializer.Serialize(a);
            return Task.FromResult(sezioni.Count > 0 ? FrozenSections.FromKeys(sezioni) : FrozenSections.Empty);
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

    /// <summary>
    /// 🔴 In pubblica ogni verso legge la SUA sezione congelata: gli arrivi da <c>stars</c>, le partenze da
    /// <c>sids</c>. Leggerli dalla stessa sezione darebbe al riferimento d'arrivo il nome di una partenza.
    /// </summary>
    [Fact]
    public async Task In_pubblica_gli_arrivi_vengono_dalla_sezione_stars()
    {
        var vive = new SidVive
        {
            Tabelle =
            {
                [(ProcedureKind.Sid, "LIRF")] = Tabella("OST9E"),
                [(ProcedureKind.Star, "LIRF")] = Tabella("OST8E"),
            },
        };
        var congelate = new Congelate
        {
            Tabelle = { ["LIRF"] = Tabella("OST2E") },
            Arrivi = { ["LIRF"] = Tabella("OST7E") },
        };

        var nomi = await new ProcedureReferenceResolver(vive, congelate)
            .PerVistaAsync(Sezioni("[[SID LIRF OST1E]] e [[STAR LIRF OST1E]]"), pubblica: true);

        Assert.Equal("OST2E", nomi.Nome(ProcedureKind.Sid, "LIRF", "OST?E"));
        Assert.Equal("OST7E", nomi.Nome(ProcedureKind.Star, "LIRF", "OST?E"));
        Assert.Empty(vive.Chieste);   // niente derivazione: la release ha già tutt'e due
    }

    /// <summary>Lo scalo del documento: le due tabelle che la pagina mostra, una per verso.</summary>
    [Fact]
    public async Task Le_due_tabelle_proprie_valgono_per_i_due_versi()
    {
        var vive = new SidVive();
        var nomi = await new ProcedureReferenceResolver(vive, new Congelate()).PerVistaAsync(
            Sezioni("[[SID LIRF OST1E]] e [[STAR LIRF OST1E]]"), pubblica: false,
            proprioIcao: "LIRF", propriaTabella: Tabella("OST3E"), propriaTabellaStar: Tabella("OST5E"));

        Assert.Equal("OST3E", nomi.Nome(ProcedureKind.Sid, "LIRF", "OST?E"));
        Assert.Equal("OST5E", nomi.Nome(ProcedureKind.Star, "LIRF", "OST?E"));
        Assert.Empty(vive.Chieste);
    }

    [Fact]
    public async Task In_pubblica_vince_la_tabella_congelata_dello_scalo_citato()
    {
        var vive = new SidVive { Tabelle = { [(ProcedureKind.Sid, "LIRF")] = Tabella("OST3E") } };
        var congelate = new Congelate { Tabelle = { ["LIRF"] = Tabella("OST2E") } };
        var nomi = await new ProcedureReferenceResolver(vive, congelate)
            .PerVistaAsync(Sezioni("[[SID LIRF OST1E]]"), pubblica: true);

        Assert.Equal("OST2E", nomi.Nome(ProcedureKind.Sid, "LIRF", "OST?E"));
        Assert.Equal((ReleaseTargetType.Airport, "LIRF"), Assert.Single(congelate.Chieste));
        Assert.Empty(vive.Chieste);
    }

    /// <summary>Sezione SID Live (niente congelato nella release): il pubblico vede la tabella di adesso, e
    /// così il testo.</summary>
    [Fact]
    public async Task In_pubblica_senza_congelato_si_deriva_adesso()
    {
        var vive = new SidVive { Tabelle = { [(ProcedureKind.Sid, "LIRF")] = Tabella("OST3E") } };
        var nomi = await new ProcedureReferenceResolver(vive, new Congelate())
            .PerVistaAsync(Sezioni("[[SID LIRF OST1E]]"), pubblica: true);

        Assert.Equal("OST3E", nomi.Nome(ProcedureKind.Sid, "LIRF", "OST?E"));
    }

    [Fact]
    public async Task In_bozza_non_si_guarda_il_congelato()
    {
        var vive = new SidVive { Tabelle = { [(ProcedureKind.Sid, "LIRF")] = Tabella("OST3E") } };
        var congelate = new Congelate { Tabelle = { ["LIRF"] = Tabella("OST2E") } };
        var nomi = await new ProcedureReferenceResolver(vive, congelate)
            .PerVistaAsync(Sezioni("[[SID LIRF OST1E]]"), pubblica: false);

        Assert.Equal("OST3E", nomi.Nome(ProcedureKind.Sid, "LIRF", "OST?E"));
        Assert.Empty(congelate.Chieste);
    }

    /// <summary>Lo scalo del documento usa la tabella che la pagina mostra — al ciclo dell'anteprima, o della
    /// release MILITARE — e non la rilegge. Gli altri scali citati si leggono dalla loro pubblica.</summary>
    [Fact]
    public async Task Lo_scalo_proprio_usa_la_tabella_della_pagina()
    {
        var vive = new SidVive { Tabelle = { [(ProcedureKind.Sid, "LIRF")] = Tabella("OST3E"), [(ProcedureKind.Sid, "LIRA")] = Tabella("TIBER7A") } };
        var congelate = new Congelate { Tabelle = { ["LIRF"] = Tabella("OST2E") } };
        var nomi = await new ProcedureReferenceResolver(vive, congelate).PerVistaAsync(
            Sezioni("[[SID LIRF OST1E]] e [[SID LIRA TIBER6A]]"), pubblica: true,
            proprioIcao: "lirf", propriaTabella: Tabella("OST9E"));

        Assert.Equal("OST9E", nomi.Nome(ProcedureKind.Sid, "LIRF", "OST?E"));
        Assert.Equal("TIBER7A", nomi.Nome(ProcedureKind.Sid, "LIRA", "TIBER?A"));
        Assert.DoesNotContain(congelate.Chieste, c => c.Chiave == "LIRF");
        Assert.DoesNotContain(vive.Chieste, c => c.Icao == "LIRF");
    }

    /// <summary>La via di quasi ogni pagina: nessun riferimento, nessuna query.</summary>
    [Fact]
    public async Task Senza_riferimenti_nessuna_query()
    {
        var vive = new SidVive();
        var congelate = new Congelate();
        var nomi = await new ProcedureReferenceResolver(vive, congelate)
            .PerVistaAsync(Sezioni("OST1E scritto a mano."), pubblica: true);

        Assert.Same(NomiProcedura.Vuoto, nomi);
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
                [(ProcedureKind.Sid, "LIBV")] = new(new[]
                {
                    new AirportSidRowView("14R", "CDC", "CDC6A", "—", "—", "—", "—", "—", "—"),
                    new AirportSidRowView("14L", "CDC", "CDC6A", "—", "—", "—", "—", "—", "—"),
                    new AirportSidRowView("32L", "VIENNA", "VIE6B", "—", "—", "—", "—", "—", "—"),
                }),
            },
        };
        var elenco = await new ProcedureReferenceResolver(vive, new Congelate()).ElencoAsync("libv");

        Assert.Equal(2, elenco.Count);
        var cdc = elenco[0];
        Assert.Equal(("CDC 6A", "14L, 14R", "[[SID LIBV CDC6A]]"), (cdc.Esteso, cdc.Piste, cdc.Riferimento));
        Assert.Equal("VIENNA 6B", elenco[1].Esteso);
    }

    /// <summary>Il selettore offre solo nomi che il riferimento sa portare: uno che la regola non riconosce uscirebbe
    /// grezzo in pagina (revisione del 18 settembre 2026).</summary>
    [Fact]
    public async Task Il_selettore_non_offre_nomi_che_il_riferimento_non_sa_portare()
    {
        var vive = new SidVive { Tabelle = { [(ProcedureKind.Sid, "LICZ")] = Tabella("NELD6V(NSY)", "VFR NORD/SUD", "ALFA.1") } };
        var elenco = await new ProcedureReferenceResolver(vive, new Congelate()).ElencoAsync("LICZ");

        Assert.Equal("NELD6V(NSY)", Assert.Single(elenco).Codice);
    }

    /// <summary>
    /// 🔴 §S3 del filone sito (23 settembre 2026): fra i punti di un trasferimento a LIRN non si trovava ERIKA 1A.
    /// Le STAR, al primo import, prendono il ciclo che il sectorfile dichiara (2610) e la tabella di OGGI (2609) le
    /// teneva tutte fuori. Chi cita una procedura scrive per i giorni che vengono: l'elenco guarda al ciclo
    /// ENTRANTE, che contiene anche tutto quel che vale oggi.
    /// </summary>
    [Fact]
    public async Task L_elenco_guarda_al_ciclo_ENTRANTE()
    {
        var airac = new AiracService();
        var vive = new SidVive { Tabelle = { [(ProcedureKind.Star, "LIRN")] = Tabella("ERIK1A") } };

        var elenco = await new ProcedureReferenceResolver(vive, new Congelate(), airac)
            .ElencoAsync("LIRN", ProcedureKind.Star);

        Assert.Equal("ERIK1A", Assert.Single(elenco).Codice);
        var chiesto = Assert.Single(vive.Cicli);
        Assert.Equal(airac.NextCycles(DateTime.UtcNow, 2)[1].Cycle, chiesto);
        Assert.NotEqual(airac.GetCycle(DateTime.UtcNow), chiesto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("LIR")]
    [InlineData("LIRFX")]
    public async Task Senza_un_ICAO_valido_l_elenco_e_vuoto_e_non_si_interroga(string icao)
    {
        var vive = new SidVive();
        Assert.Empty(await new ProcedureReferenceResolver(vive, new Congelate()).ElencoAsync(icao));
        Assert.Empty(vive.Chieste);
    }

    /// <summary>Le anteprime dell'editor: la bozza, mai il congelato.</summary>
    [Fact]
    public async Task Sui_testi_dell_editor_si_guarda_la_bozza()
    {
        var vive = new SidVive { Tabelle = { [(ProcedureKind.Sid, "LIRF")] = Tabella("OST3E") } };
        var congelate = new Congelate { Tabelle = { ["LIRF"] = Tabella("OST2E") } };
        var nomi = await new ProcedureReferenceResolver(vive, congelate)
            .PerTestiAsync(new[] { null, "Expect [[SID LIRF OST1E]]." });

        Assert.Equal("OST3E", nomi.Nome(ProcedureKind.Sid, "LIRF", "OST?E"));
        Assert.Empty(congelate.Chieste);
    }

    [Fact]
    public async Task I_riferimenti_nelle_sotto_sezioni_e_nelle_tabelle_si_trovano()
    {
        var vive = new SidVive { Tabelle = { [(ProcedureKind.Sid, "LIBV")] = Tabella("CDC7A") } };
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

        var nomi = await new ProcedureReferenceResolver(vive, new Congelate()).PerVistaAsync(new[] { padre }, pubblica: false);
        Assert.Equal("CDC7A", nomi.Nome(ProcedureKind.Sid, "LIBV", "CDC?A"));
    }
}
