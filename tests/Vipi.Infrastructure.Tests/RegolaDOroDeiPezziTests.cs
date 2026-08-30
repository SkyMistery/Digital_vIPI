using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <b>La regola d'oro</b> della carta <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c> §3d:
/// <i>un import scrive solo i pezzi della PROPRIA fonte, e non cancella mai quelli di un'altra.</i>
///
/// <para>⚠️ È la regola da cui dipende la <b>reversibilità</b> dell'aggancio: finché i pezzi di IVAO restano
/// in archivio mentre l'AIP è attivo, lo sgancio non ha niente da ri-importare. Il giorno che cadesse,
/// l'unbind si romperebbe <b>in silenzio</b> — e questi tre test sono l'unica cosa che se ne accorgerebbe.
/// Sono la fotocopia della trappola del 26 agosto 2026, quando un <c>[]</c> di sorgente azzerò 83 poligoni
/// su 83: qui la si vuole verde <b>per costruzione</b>, non per fortuna.</para>
/// </summary>
public class RegolaDOroDeiPezziTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorShapeParts _pezzi = default!;

    private const int Settore = 42;
    private const string Callsign = "LICC_APP";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _pezzi = new EfSectorShapeParts(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Il monoblocco dell'anagrafica: un anello, due quote sciolte.</summary>
    private static IReadOnlyList<ShapePart> Ivao() => new[]
    {
        new ShapePart("[[14.5,36.5],[16.0,36.5],[16.0,38.0],[14.5,38.0]]",
            0, 19_500, AirspaceDatum.Gnd, AirspaceDatum.Amsl, "GND", "19500 FT AMSL"),
    };

    /// <summary>Le due zone dell'AIP, con bande <b>diverse</b>: è il caso di Amendola, misurato.</summary>
    private static IReadOnlyList<ShapePart> Aip() => new[]
    {
        new ShapePart("[[15.0,37.0],[15.4,37.0],[15.4,37.4],[15.0,37.4]]",
            null, 10_500, AirspaceDatum.Gnd, AirspaceDatum.FlightLevel, "GND", "FL105", "CTR|Z1|GND|FL105"),
        new ShapePart("[[15.5,37.0],[15.9,37.0],[15.9,37.4],[15.5,37.4]]",
            7_000, 19_500, AirspaceDatum.Amsl, AirspaceDatum.FlightLevel, "7000 FT AMSL", "FL195", "CTR|Z2|7000 FT AMSL|FL195"),
    };

    private Task<ShapePartsWriteResult> ScriviAsync(ShapeSource fonte, IReadOnlyList<ShapePart> parts) =>
        _pezzi.ReplacePartsAsync(SourceCatalog.AirportPosition, Settore, Callsign, fonte,
            ShapePartState.InForce, parts);

    private Task<IReadOnlyList<ShapePart>> LeggiAsync(ShapeSource fonte) =>
        _pezzi.ListAsync(SourceCatalog.AirportPosition, Settore, fonte, ShapePartState.InForce);

    /// <summary>
    /// L'import di IVAO gira su un settore agganciato: i pezzi dell'AIP devono essere ancora lì, <b>identici</b>.
    /// ⚠️ L'assert è sul CONTENUTO, non sul conteggio: due pezzi riscritti male sono ancora due.
    /// </summary>
    [Fact]
    public async Task L_import_di_ivao_non_tocca_i_pezzi_dell_aip()
    {
        await ScriviAsync(ShapeSource.Aip, Aip());
        var prima = await LeggiAsync(ShapeSource.Aip);

        await ScriviAsync(ShapeSource.Source, Ivao());

        var dopo = await LeggiAsync(ShapeSource.Aip);
        Assert.Equal(prima, dopo);                       // record: uguaglianza per valore, campo per campo
        Assert.Single(await LeggiAsync(ShapeSource.Source));
    }

    /// <summary>Il verso opposto: un file nuovo dell'AIP non porta via i pezzi di IVAO, che sono il ritorno.</summary>
    [Fact]
    public async Task Un_caricamento_dell_aip_non_tocca_i_pezzi_di_ivao()
    {
        await ScriviAsync(ShapeSource.Source, Ivao());
        var prima = await LeggiAsync(ShapeSource.Source);

        await ScriviAsync(ShapeSource.Aip, Aip());

        var dopo = await LeggiAsync(ShapeSource.Source);
        Assert.Equal(prima, dopo);
        Assert.Equal(2, (await LeggiAsync(ShapeSource.Aip)).Count);
    }

    /// <summary>
    /// ⚠️ La fotocopia del 26 agosto: una sorgente <b>muta</b> (elenco vuoto) non cancella niente, in nessuno
    /// dei due versi, e lo <b>dichiara</b> nell'esito invece di far finta di aver scritto zero pezzi.
    /// </summary>
    [Fact]
    public async Task Una_sorgente_muta_non_cancella_niente()
    {
        await ScriviAsync(ShapeSource.Source, Ivao());
        await ScriviAsync(ShapeSource.Aip, Aip());

        var esitoIvao = await ScriviAsync(ShapeSource.Source, Array.Empty<ShapePart>());
        var esitoAip = await ScriviAsync(ShapeSource.Aip, Array.Empty<ShapePart>());

        Assert.True(esitoIvao.SourceSilent);
        Assert.True(esitoAip.SourceSilent);
        Assert.Single(await LeggiAsync(ShapeSource.Source));
        Assert.Equal(2, (await LeggiAsync(ShapeSource.Aip)).Count);
    }

    /// <summary>
    /// Svuotare è un gesto <b>esplicito</b> e resta dentro una fonte sola: è così che lo sgancio riporta il
    /// settore a IVAO senza ri-importare niente.
    /// </summary>
    [Fact]
    public async Task Svuotare_una_fonte_lascia_intatta_l_altra_ed_e_il_ritorno_a_ivao()
    {
        await ScriviAsync(ShapeSource.Source, Ivao());
        await ScriviAsync(ShapeSource.Aip, Aip());

        var tolti = await _pezzi.ClearPartsAsync(SourceCatalog.AirportPosition, Settore, ShapeSource.Aip);

        Assert.Equal(2, tolti);
        Assert.Empty(await LeggiAsync(ShapeSource.Aip));
        Assert.Single(await LeggiAsync(ShapeSource.Source));   // la forma di IVAO era lì tutto il tempo
    }

    /// <summary>
    /// Riscrivere la propria fonte con <b>meno</b> pezzi non lascia in giro quelli di prima, e gli ordinali
    /// ripartono da zero: senza, il secondo giro romperebbe l'indice unico.
    /// </summary>
    [Fact]
    public async Task Riscrivere_la_propria_fonte_sostituisce_l_insieme_intero()
    {
        await ScriviAsync(ShapeSource.Aip, Aip());
        await ScriviAsync(ShapeSource.Aip, new[] { Aip()[1] });

        var dopo = await LeggiAsync(ShapeSource.Aip);
        Assert.Single(dopo);
        Assert.Equal("CTR|Z2|7000 FT AMSL|FL195", dopo[0].SourceRef);
        Assert.Equal(0, await _db.SectorShapeParts.CountAsync(x => x.Source == ShapeSource.Aip && x.Ordinal > 0));
    }

    /// <summary>
    /// Il ciclo AIRAC vive solo su un insieme <b>in attesa</b>: su quello in vigore il ciclo è già arrivato, e
    /// lasciarcelo scritto lo farebbe promuovere una seconda volta.
    /// </summary>
    [Fact]
    public async Task Il_ciclo_airac_non_si_scrive_su_un_insieme_gia_in_vigore()
    {
        await _pezzi.ReplacePartsAsync(SourceCatalog.AirportPosition, Settore, Callsign, ShapeSource.Sectorfile,
            ShapePartState.InForce, Ivao(), airacCycle: "2610");
        await _pezzi.ReplacePartsAsync(SourceCatalog.AirportPosition, Settore, Callsign, ShapeSource.Sectorfile,
            ShapePartState.Pending, Aip(), airacCycle: "2610");

        var inVigore = await _db.SectorShapeParts.Where(x => x.State == ShapePartState.InForce).ToListAsync();
        var inAttesa = await _db.SectorShapeParts.Where(x => x.State == ShapePartState.Pending).ToListAsync();

        Assert.All(inVigore, x => Assert.Null(x.AiracCycle));
        Assert.All(inAttesa, x => Assert.Equal("2610", x.AiracCycle));
    }

    /// <summary>Il callsign si normalizza in maiuscolo alla scrittura: è la chiave con cui si legge.</summary>
    [Fact]
    public async Task Il_callsign_si_scrive_maiuscolo()
    {
        await _pezzi.ReplacePartsAsync(SourceCatalog.AirportPosition, Settore, "licc_app", ShapeSource.Source,
            ShapePartState.InForce, Ivao());

        Assert.Equal(Callsign, (await _db.SectorShapeParts.FirstAsync()).Callsign);
    }
}
