using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le correzioni a mano dei volumi dell'AIP su database (carta docs/feature/2026-09-30-correzioni-spazi-aerei.md):
/// si vedono ovunque si legga il volume, non rompono gli agganci, sopravvivono a un caricamento nuovo, e quel che il
/// file nuovo cambia sotto di loro si segnala e si conferma.
/// </summary>
public class CorrezioniSpaziAereiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirspaceCatalog _catalogo = default!;
    private EfSectorAirspaceBindings _agganci = default!;
    private readonly ShapeChangeStamp _gettone = new();

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _catalogo = new EfAirspaceCatalog(_db, LivelloFisso.Editor, _gettone);
        _agganci = new EfSectorAirspaceBindings(_db, LivelloFisso.Editor);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static string Kml(params (string Nome, string Categoria, string Base, string Top)[] volumi)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?><kml xmlns="http://www.opengis.net/kml/2.2"><Document>""");
        var lon = 9.0;
        foreach (var v in volumi)
        {
            sb.Append($"""
                <Placemark><ExtendedData><SchemaData>
                  <SimpleData name="Name">{v.Nome}</SimpleData>
                  <SimpleData name="Category">{v.Categoria}</SimpleData>
                  <SimpleData name="Base">{v.Base}</SimpleData><SimpleData name="Top">{v.Top}</SimpleData>
                </SchemaData></ExtendedData>
                <Polygon><outerBoundaryIs><LinearRing><coordinates>
                  {lon},45.0,0 {lon + 0.5},45.0,0 {lon + 0.25},45.5,0 {lon},45.0,0
                </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
                """);
            lon += 1;
        }
        sb.Append("</Document></kml>");
        return sb.ToString();
    }

    private const string Ctr = "Control Traffic Region";
    private const string Tma = "Terminal Manoeuvring Area";

    private async Task CaricaAsync(string kml) =>
        await _catalogo.SaveAsync(
            new NewAirspaceImport("it.kmz", System.Text.Encoding.UTF8.GetBytes(kml), "2610", 7, "Chi carica"),
            AirspaceKmlReader.LeggiKml(kml), DateTime.UtcNow);

    private async Task<AirspaceVolumeRow> VolumeAsync(string nome) =>
        (await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery())).Single(v => v.Name == nome);

    private static AirspaceVolumeKey Chiave(AirspaceVolumeRow v) => new(v.NaturalKey, v.Ordinal);

    /// <summary>«La classe del file»: per le prove in cui la classe non è la cosa che si corregge.</summary>
    private const string DalFile = "=";

    private Task CorreggiAsync(AirspaceVolumeRow v, AirspaceFamily famiglia, string? classe, string @base, string tetto) =>
        _catalogo.CorrectAsync(Chiave(v),
            new AirspaceCorrectionInput(famiglia, classe == DalFile ? v.AirspaceClass : classe, @base, tetto), 1,
            "Correttore", DateTime.UtcNow);

    [Fact]
    public async Task La_correzione_si_vede_nell_elenco_e_la_chiave_non_cambia()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        var prima = await VolumeAsync("PROVA CTR");
        var gettone = _gettone.LastChangeUtc;

        await CorreggiAsync(prima, AirspaceFamily.Ctr, "C", "GND", "3500 FT AMSL");

        var dopo = await VolumeAsync("PROVA CTR");
        Assert.True(dopo.IsCorrected);
        Assert.Equal("3500 FT AMSL", dopo.TopRaw);
        Assert.Equal(3500, dopo.TopFeet);
        Assert.Equal("C", dopo.AirspaceClass);
        Assert.Equal(prima.NaturalKey, dopo.NaturalKey);
        Assert.True(_gettone.LastChangeUtc > gettone);   // mappa, 3D e stampa ridisegnano adesso

        var c = Assert.Single(await _catalogo.ListCorrectionsAsync());
        Assert.Equal("2500 FT AMSL", c.FileTopRaw);
        Assert.Equal("Correttore", c.UpdatedByName);
        Assert.Empty(await _catalogo.ReviewCorrectionsAsync());
    }

    [Fact]
    public async Task Un_settore_agganciato_disegna_il_volume_corretto()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        var v = await VolumeAsync("PROVA CTR");
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LIXX_APP", [Chiave(v)], 7, "Chi sceglie");

        await CorreggiAsync(v, AirspaceFamily.Ctr, DalFile, "1000 FT AMSL", "FL95");

        var riga = (await _agganci.ResolveAsync(["LIXX_APP"]))["LIXX_APP"];
        Assert.Empty(riga.Missing);   // l'aggancio non si è rotto
        var volume = Assert.Single(riga.Volumes);
        Assert.Equal((1000, 9500), (volume.BaseFeet, volume.TopFeet));
        Assert.True(volume.IsCorrected);
    }

    [Fact]
    public async Task Il_tipo_corretto_decide_filtro_e_conteggio()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL"), ("ALTRA TMA", Tma, "1500 FT AMSL", "FL195")));
        await CorreggiAsync(await VolumeAsync("PROVA CTR"), AirspaceFamily.Tma, DalFile, "GND", "2500 FT AMSL");

        var tma = await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery(Families: [AirspaceFamily.Tma]));
        Assert.Equal(["ALTRA TMA", "PROVA CTR"], tma.Select(v => v.Name).ToArray());
        Assert.Empty(await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery(Families: [AirspaceFamily.Ctr])));

        var conti = await _catalogo.CountByFamilyAsync();
        Assert.Equal(2, conti[AirspaceFamily.Tma]);
        Assert.False(conti.ContainsKey(AirspaceFamily.Ctr));
    }

    [Fact]
    public async Task Correggere_riportando_i_valori_del_file_toglie_la_correzione()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        var v = await VolumeAsync("PROVA CTR");
        await CorreggiAsync(v, AirspaceFamily.Ctr, DalFile, "GND", "3500 FT AMSL");

        await CorreggiAsync(v, AirspaceFamily.Ctr, v.AirspaceClass, "GND", "2500 FT AMSL");

        Assert.Empty(await _catalogo.ListCorrectionsAsync());
        Assert.False((await VolumeAsync("PROVA CTR")).IsCorrected);
    }

    [Fact]
    public async Task Una_quota_che_non_si_legge_non_si_salva()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        var v = await VolumeAsync("PROVA CTR");

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => CorreggiAsync(v, AirspaceFamily.Ctr, "D", "boh", "2500 FT AMSL"));
        Assert.Empty(await _catalogo.ListCorrectionsAsync());
    }

    [Fact]
    public async Task Correggere_chiede_l_Editor()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        var v = await VolumeAsync("PROVA CTR");
        var anonimo = new EfAirspaceCatalog(_db, LivelloFisso.Anonimo);

        await Assert.ThrowsAnyAsync<Exception>(() => anonimo.CorrectAsync(Chiave(v),
            new AirspaceCorrectionInput(AirspaceFamily.Ctr, DalFile, "GND", "3500 FT AMSL"), null, null, DateTime.UtcNow));
        Assert.Empty(await _catalogo.ListCorrectionsAsync());
    }

    [Fact]
    public async Task Lo_stesso_file_ricaricato_tiene_la_correzione_senza_segnalare_niente()
    {
        var kml = Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL"));
        await CaricaAsync(kml);
        await CorreggiAsync(await VolumeAsync("PROVA CTR"), AirspaceFamily.Ctr, DalFile, "GND", "3500 FT AMSL");

        await CaricaAsync(kml);

        Assert.Equal("3500 FT AMSL", (await VolumeAsync("PROVA CTR")).TopRaw);
        Assert.Empty(await _catalogo.ReviewCorrectionsAsync());
    }

    [Fact]
    public async Task File_cambiato_si_segnala_e_va_bene_lo_fa_sparire_tenendo_la_correzione()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        var v = await VolumeAsync("PROVA CTR");
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LIXX_APP", [Chiave(v)], 7, "Chi sceglie");
        await CorreggiAsync(v, AirspaceFamily.Ctr, DalFile, "GND", "3500 FT AMSL");

        // Il file nuovo cambia il tetto (quindi la chiave), ma non nel senso della correzione.
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "3000 FT AMSL")));

        var f = Assert.Single(await _catalogo.ReviewCorrectionsAsync());
        Assert.Equal(AirspaceCorrectionFindingKind.FileChanged, f.Kind);
        Assert.True(f.KeyChanged);
        var d = Assert.Single(f.Diffs);
        Assert.Equal(("2500 FT AMSL", "3000 FT AMSL", "3500 FT AMSL"), (d.FileBefore, d.FileNow, d.Corrected));
        // Finché non si conferma, l'aggancio cita la chiave vecchia: scoperto, e la pagina lo dice.
        Assert.Single((await _agganci.ResolveAsync(["LIXX_APP"]))["LIXX_APP"].Missing);

        await _catalogo.AcknowledgeCorrectionAsync(f.Correction.Id, 1, "Correttore", DateTime.UtcNow);

        Assert.Empty(await _catalogo.ReviewCorrectionsAsync());
        var nuovo = await VolumeAsync("PROVA CTR");
        Assert.True(nuovo.IsCorrected);
        Assert.Equal("3500 FT AMSL", nuovo.TopRaw);
        Assert.Equal("3000 FT AMSL", Assert.Single(await _catalogo.ListCorrectionsAsync()).FileTopRaw);
        var riga = (await _agganci.ResolveAsync(["LIXX_APP"]))["LIXX_APP"];
        Assert.Empty(riga.Missing);   // l'aggancio ha seguito il volume
        Assert.Equal("3500 FT AMSL", Assert.Single(riga.Volumes).TopRaw);
    }

    [Fact]
    public async Task Prendi_il_file_toglie_la_correzione_e_gli_agganci_seguono_il_volume()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        var v = await VolumeAsync("PROVA CTR");
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LIXX_APP", [Chiave(v)], 7, "Chi sceglie");
        await CorreggiAsync(v, AirspaceFamily.Ctr, DalFile, "GND", "3500 FT AMSL");
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "3000 FT AMSL")));
        var f = Assert.Single(await _catalogo.ReviewCorrectionsAsync());

        await _catalogo.RemoveCorrectionAsync(f.Correction.Id);

        Assert.Empty(await _catalogo.ListCorrectionsAsync());
        var riga = (await _agganci.ResolveAsync(["LIXX_APP"]))["LIXX_APP"];
        Assert.Empty(riga.Missing);
        Assert.Equal("3000 FT AMSL", Assert.Single(riga.Volumes).TopRaw);
    }

    [Fact]
    public async Task Il_file_ora_dice_gia_cosi_va_bene_toglie_la_correzione()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        await CorreggiAsync(await VolumeAsync("PROVA CTR"), AirspaceFamily.Ctr, DalFile, "GND", "3500 FT AMSL");
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "3500 FT AMSL")));

        var f = Assert.Single(await _catalogo.ReviewCorrectionsAsync());
        Assert.Equal(AirspaceCorrectionFindingKind.FileAgrees, f.Kind);

        await _catalogo.AcknowledgeCorrectionAsync(f.Correction.Id, 1, "Correttore", DateTime.UtcNow);

        Assert.Empty(await _catalogo.ListCorrectionsAsync());
        Assert.Empty(await _catalogo.ReviewCorrectionsAsync());
    }

    [Fact]
    public async Task Volume_sparito_si_segnala_e_va_bene_toglie_la_correzione()
    {
        await CaricaAsync(Kml(("PROVA CTR", Ctr, "GND", "2500 FT AMSL")));
        await CorreggiAsync(await VolumeAsync("PROVA CTR"), AirspaceFamily.Ctr, DalFile, "GND", "3500 FT AMSL");
        await CaricaAsync(Kml(("ALTRO CTR", Ctr, "GND", "2500 FT AMSL")));

        var f = Assert.Single(await _catalogo.ReviewCorrectionsAsync());
        Assert.Equal(AirspaceCorrectionFindingKind.VolumeMissing, f.Kind);

        await _catalogo.AcknowledgeCorrectionAsync(f.Correction.Id, 1, "Correttore", DateTime.UtcNow);

        Assert.Empty(await _catalogo.ListCorrectionsAsync());
    }
}
