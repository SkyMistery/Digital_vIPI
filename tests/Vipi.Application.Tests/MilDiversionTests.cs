using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La tabella «Aeroporti alternati» del vSOP militare (carta <c>2026-08-27-vsop-militari.md</c> §12f).
///
/// <para>⚠️ Rispetto alle Radioassistenze c'è una differenza che decide tutto il resto: <b>rilevamento e
/// distanza sono del documento</b>, non dell'aeroporto — il rilevamento di Grottaglie <i>da Amendola</i> non
/// è un fatto di Grottaglie. Nomi e radioassistenze invece vengono dai cataloghi.</para>
/// </summary>
public class MilDiversionTests
{
    // ---- La resa, come l'ha chiesta il committente -----------------------------------------------------

    /// <summary>Chi compila scrive <b>solo il numero</b>: l'unità la mette il documento, così non ci
    /// finiscono dentro tre modi diversi di dire gradi.</summary>
    [Theory]
    [InlineData(126, "126°")]
    [InlineData(7, "007°")]
    [InlineData(360, "360°")]
    [InlineData(null, "")]
    public void Il_rilevamento_si_scrive_col_grado(int? gradi, string atteso) =>
        Assert.Equal(atteso, MilDiversionText.Rilevamento(gradi));

    [Theory]
    [InlineData(40, "40 NM")]
    [InlineData(null, "")]
    public void La_distanza_si_scrive_con_NM(int? nm, string atteso) =>
        Assert.Equal(atteso, MilDiversionText.Distanza(nm));

    // ---- Il payload ------------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ Un rilevamento di 400° o una distanza negativa non sono «quasi giusti»: sono un refuso, e
    /// stamparli darebbe a una tabella di documento l'aria di dire una cosa precisa e falsa. Si scartano
    /// alla scrittura, dove ancora si può.
    /// </summary>
    [Theory]
    [InlineData(400, 40, null, 40)]
    [InlineData(-1, 40, null, 40)]
    [InlineData(126, -5, 126, null)]
    [InlineData(126, 40, 126, 40)]
    public void I_numeri_fuori_scala_non_si_salvano(int b, int d, int? attesoB, int? attesoD)
    {
        var json = MilDiversionPayload.Scrivi(new[]
        {
            new MilDiversionPayload.Riga { Icao = "LIBG", Bearing = b, Distance = d },
        });

        var r = Assert.Single(MilDiversionPayload.Leggi(json));
        Assert.Equal(attesoB, r.Bearing);
        Assert.Equal(attesoD, r.Distance);
    }

    [Fact]
    public void Il_payload_conserva_l_ordine_e_normalizza_i_codici()
    {
        var json = MilDiversionPayload.Scrivi(new[]
        {
            new MilDiversionPayload.Riga { Icao = "libg", Name = " Grottaglie " },
            new MilDiversionPayload.Riga { Icao = "LGKR" },
        });

        var righe = MilDiversionPayload.Leggi(json);
        Assert.Equal(new[] { "LIBG", "LGKR" }, righe.Select(r => r.Icao));
        Assert.Equal("Grottaglie", righe[0].Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non json")]
    [InlineData("""{"variant":"mildiversion","rows":[{"icao":""}]}""")]
    public void Un_payload_illeggibile_da_zero_righe(string? json) =>
        Assert.Empty(MilDiversionPayload.Leggi(json));

    /// <summary>Le identità da risolvere si raccolgono UNA volta per tutte le righe: dieci alternati con tre
    /// navaid ciascuno sono una pagina pubblica, non una schermata d'amministrazione.</summary>
    [Fact]
    public void Le_chiavi_delle_radioassistenze_si_raccolgono_senza_ripetizioni()
    {
        var righe = new[]
        {
            Riga("LIBG", ("MNL", "VOR"), ("AEA", "VOR")),
            Riga("LGKR", ("MNL", "VOR")),
        };

        Assert.Equal(2, MilDiversionPayload.ChiaviNavaid(righe).Count);
    }

    // ---- La risoluzione --------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ Il nome dell'ARCHIVIO vince su quello salvato nel documento: quello è il dato vero, e il nome nel
    /// payload è il ripiego per gli scali esteri che non abbiamo. Al contrario si congelerebbe nel documento
    /// un nome che poi cambia.
    /// </summary>
    [Fact]
    public async Task Il_nome_dell_archivio_vince_su_quello_salvato()
    {
        var righe = new[]
        {
            new MilDiversionPayload.Riga { Icao = "LIBG", Name = "un nome vecchio" },
            new MilDiversionPayload.Riga { Icao = "LGKR", Name = "Kerkyra" },
        };

        var viste = await MilDiversionResolver.ResolveAsync(righe, new AnagraficaFinta(), new AeroportiFinti());

        Assert.Equal("Grottaglie", viste[0].Name);   // in archivio
        Assert.Equal("Kerkyra", viste[1].Name);      // estero: vale il nome salvato
    }

    /// <summary>Uno scalo che non conosce nessuno resta con la sua sigla e la cella vuota: una riga senza
    /// nome è meglio di una riga che sparisce.</summary>
    [Fact]
    public async Task Uno_scalo_ignoto_resta_in_tabella()
    {
        var viste = await MilDiversionResolver.ResolveAsync(
            new[] { new MilDiversionPayload.Riga { Icao = "ZZZZ" } }, new AnagraficaFinta(), new AeroportiFinti());

        Assert.Equal("ZZZZ", Assert.Single(viste).Icao);
        Assert.Equal("", viste[0].Name);
    }

    /// <summary>Le radioassistenze si risolvono sull'anagrafica e si rendono con la forma degli alternati —
    /// col tipo in mezzo e il canale <b>senza</b> «CH».</summary>
    [Fact]
    public async Task Le_radioassistenze_si_risolvono_e_si_rendono_col_tipo()
    {
        var viste = await MilDiversionResolver.ResolveAsync(
            new[] { Riga("LIBG", ("MNL", "VOR")) }, new AnagraficaFinta(), new AeroportiFinti());

        var n = Assert.Single(viste[0].Navaids);
        Assert.Equal("MNL VORTACAN - 99Y (115.25)", NavaidText.ConTipo(n.Code, n.Type, n.Channel, n.Frequency));
    }

    /// <summary>Una radioassistenza citata ma non più in anagrafica non si stampa: un documento che cita un
    /// codice inesistente manderebbe qualcuno a cercarlo.</summary>
    [Fact]
    public async Task Una_radioassistenza_sconosciuta_non_si_stampa()
    {
        var viste = await MilDiversionResolver.ResolveAsync(
            new[] { Riga("LIBG", ("XXX", "VOR")) }, new AnagraficaFinta(), new AeroportiFinti());

        Assert.Empty(viste[0].Navaids);
    }

    /// <summary>Senza archivio degli aeroporti — è opzionale — resta il nome salvato: la tabella si legge lo
    /// stesso.</summary>
    [Fact]
    public async Task Senza_archivio_resta_il_nome_salvato()
    {
        var viste = await MilDiversionResolver.ResolveAsync(
            new[] { new MilDiversionPayload.Riga { Icao = "LIBG", Name = "Grottaglie" } },
            new AnagraficaFinta(), aeroporti: null);

        Assert.Equal("Grottaglie", Assert.Single(viste).Name);
    }

    // ---- Il catalogo -----------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ <b>Derivata</b> come le Radioassistenze, e per la stessa ragione: solo le derivate le cattura
    /// <c>FrozenSectionScan</c>, quindi solo così la release <b>fotografa</b> la tabella. Qui vale doppio —
    /// il nome di uno scalo e la frequenza di una radioassistenza stanno in <i>due</i> cataloghi diversi, e
    /// nessuno dei due appartiene al documento.
    /// </summary>
    [Fact]
    public void La_sezione_e_derivata_tiene_la_prosa_ed_e_annidata()
    {
        Assert.Equal(SectionKind.Derived, SectionCatalog.KindOf("diversion"));
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, "diversion"));
        Assert.True(SectionCatalog.KeepsOwnBlocks(SectionProfile.AirportMil, "diversion"));
        Assert.DoesNotContain(SectionCatalog.For(SectionProfile.AirportMil), d => d.Key == "diversion");
        Assert.NotNull(SectionCatalog.Find(SectionProfile.AirportMil, "diversion"));
    }

    // ---- Aiutanti --------------------------------------------------------------------------------------

    private static MilDiversionPayload.Riga Riga(string icao, params (string Code, string Kind)[] nav) => new()
    {
        Icao = icao,
        Navaids = nav.Select(n => new MilDiversionPayload.Nav { Code = n.Code, Kind = n.Kind }).ToList(),
    };

    private sealed class AnagraficaFinta : INavaidCatalog
    {
        private static readonly NavaidRow Mnl = new(1, "MNL", "VOR", "VORTACAN", "115.25", "99Y", 41.5, 15.7,
            NavaidFieldOrigin.Source, NavaidFieldOrigin.Source, NavaidFieldOrigin.Source, null, null);

        public Task<IReadOnlyList<NavaidRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NavaidRow>>(new[] { Mnl });

        public Task<IReadOnlyList<NavaidRow>> GetManyAsync(IReadOnlyList<NavaidKey> keys, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NavaidRow>>(
                keys.Where(k => k.Code == "MNL").Select(_ => Mnl).ToList());

        public Task<NavaidRow> CreateAsync(string code, string kind, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidWrite> SetDisplayTypeAsync(int id, string? tipo, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidWrite> SetFrequencyAsync(int id, string? f, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidWrite> SetChannelAsync(int id, string? c, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidWrite> SetCoordinatesAsync(int id, string? s, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<NavaidImportOutcome> ImportFromSourceAsync(IReadOnlyList<SourceNavaid> n, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class AeroportiFinti : IAirportNameLookup
    {
        public Task<IReadOnlyDictionary<string, string>> NamesAsync(IReadOnlyList<string> icaos, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                icaos.Where(i => i == "LIBG").ToDictionary(i => i, _ => "Grottaglie"));

        public Task<AirportName?> FindAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<AirportName?>(icao == "LIBG" ? new AirportName("LIBG", "Grottaglie", true) : null);

        public Task<IReadOnlyList<AirportName>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirportName>>(new[] { new AirportName("LIBG", "Grottaglie", true) });
    }
}
