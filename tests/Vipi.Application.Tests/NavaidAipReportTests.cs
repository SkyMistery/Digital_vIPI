using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Tests;

/// <summary>
/// Il confronto fra le radioassistenze dell'AIP e la nostra anagrafica. ⚠️ <b>Segnala e basta</b>: le
/// correzioni si fanno nel sectorfile, e da lì si reimportano (decisione 9 del committente).
/// </summary>
public class NavaidAipReportTests
{
    // Un ritaglio del file vero: il VOR di Alghero come AirspaceConverter lo scrive.
    private const string Kml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <kml xmlns="http://www.opengis.net/kml/2.2"><Document>
          <Placemark><name>ALGHERO</name><ExtendedData><SchemaData>
            <SimpleData name="Name">ALGHERO</SimpleData>
            <SimpleData name="Type">VOR</SimpleData>
            <SimpleData name="Code">AHO</SimpleData>
            <SimpleData name="VOR">109.300</SimpleData>
            <SimpleData name="Desc">TACAN, Frequency: 109.30 MHz, Channel: 30X, Range: 40 NM, Declination: 3.03 deg magnetic</SimpleData>
          </SchemaData></ExtendedData>
          <Point><coordinates>8.28944,40.6361,37</coordinates></Point></Placemark>
          <Placemark><name>ALBENGA</name><ExtendedData><SchemaData>
            <SimpleData name="Name">ALBENGA</SimpleData>
            <SimpleData name="Type">NDB</SimpleData>
            <SimpleData name="Code">ABN</SimpleData>
            <SimpleData name="NDB">420.0</SimpleData>
            <SimpleData name="Desc">NDB, Frequency: 420.0 kHz, Range: 25 NM, Declination: 2.90 deg magnetic</SimpleData>
          </SchemaData></ExtendedData>
          <Point><coordinates>8.22111,44.0561,2</coordinates></Point></Placemark>
          <Placemark><ExtendedData><SchemaData>
            <SimpleData name="Name">UNA CTR</SimpleData>
            <SimpleData name="Category">Control Traffic Region</SimpleData>
            <SimpleData name="Base">GND</SimpleData><SimpleData name="Top">FL100</SimpleData>
          </SchemaData></ExtendedData>
          <Polygon><outerBoundaryIs><LinearRing><coordinates>
            9,45,0 9.1,45,0 9.05,45.1,0 9,45,0
          </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
        </Document></kml>
        """;

    private static NavaidRow Nostra(string code, string kind, string? type = null, string? freq = null,
        string? chan = null, double? lat = null, double? lon = null) =>
        new(1, code, kind, type, freq, chan, lat, lon,
            NavaidFieldOrigin.Source, NavaidFieldOrigin.Source, NavaidFieldOrigin.Source, null, null);

    [Fact]
    public void Legge_Le_Radioassistenze_E_Lascia_Fuori_Gli_Spazi_Aerei()
    {
        var righe = AirspaceNavaidReader.LeggiKml(Kml);

        Assert.Equal(2, righe.Count);   // il CTR non è una radioassistenza
        var aho = Assert.Single(righe, r => r.Code == "AHO");
        Assert.Equal("VHF", aho.Kind);
        Assert.Equal("TACAN", aho.Type);        // il tipo lo dice la descrizione
        Assert.Equal("109.300", aho.Frequency);
        Assert.Equal("30X", aho.Channel);       // e il canale pure
        Assert.Equal(40.6361, aho.Latitude!.Value, 4);
        Assert.Equal(8.28944, aho.Longitude!.Value, 5);
    }

    [Fact]
    public void Un_Ndb_Si_Riconosce_Dalla_Famiglia_E_Non_Ha_Canale()
    {
        var abn = Assert.Single(AirspaceNavaidReader.LeggiKml(Kml), r => r.Code == "ABN");

        Assert.Equal("NDB", abn.Kind);
        Assert.Equal("NDB", abn.Type);
        Assert.Equal("420.0", abn.Frequency);
        Assert.Null(abn.Channel);
    }

    [Fact]
    public void Due_Archivi_Che_Dicono_La_Stessa_Cosa_Non_Fanno_Differenze()
    {
        var aip = AirspaceNavaidReader.LeggiKml(Kml);
        var nostre = new[]
        {
            Nostra("AHO", "VHF", "TACAN", "109.3", "30X", 40.6361, 8.28944),
            Nostra("ABN", "NDB", "NDB", "420", null, 44.0561, 8.22111),
        };

        Assert.Empty(NavaidAipReport.Confronta(aip, nostre));
    }

    [Fact]
    public void La_Frequenza_Si_Confronta_Sul_VALORE_Non_Sulla_Scrittura()
    {
        // ⚠️ `109.300` e `109.3` sono lo stesso numero: un confronto sulle stringhe darebbe 78 differenze
        // finte al primo giro, e nessuno guarderebbe più il rapporto.
        var aip = AirspaceNavaidReader.LeggiKml(Kml);

        var esito = NavaidAipReport.Confronta(aip, [Nostra("AHO", "VHF", "TACAN", "109.3", "30X", 40.6361, 8.28944)]);

        Assert.DoesNotContain(esito, d => d.Kind == NavaidDiffKind.FrequenzaDiversa);
    }

    [Fact]
    public void Una_Frequenza_Davvero_Diversa_Si_Dice()
    {
        var aip = AirspaceNavaidReader.LeggiKml(Kml);

        var esito = NavaidAipReport.Confronta(aip, [Nostra("AHO", "VHF", "TACAN", "115.25", "30X", 40.6361, 8.28944)]);

        var d = Assert.Single(esito, x => x.Kind == NavaidDiffKind.FrequenzaDiversa);
        Assert.Equal("109.3", d.Aip);
        Assert.Equal("115.25", d.Nostro);
    }

    [Fact]
    public void Il_Tipo_Che_Manca_Da_Noi_Si_Segnala_Ma_Non_Si_Scrive()
    {
        // Il tipo dell'anagrafica è editoriale e nasce vuoto: l'AIP una risposta ce l'ha, e la si mostra.
        var aip = AirspaceNavaidReader.LeggiKml(Kml);

        var esito = NavaidAipReport.Confronta(aip, [Nostra("AHO", "VHF", null, "109.3", "30X", 40.6361, 8.28944)]);

        var d = Assert.Single(esito, x => x.Kind == NavaidDiffKind.TipoMancante);
        Assert.Equal("TACAN", d.Aip);
        Assert.Null(d.Nostro);
    }

    [Fact]
    public void Un_Canale_Che_Noi_Non_Abbiamo_E_Una_LACUNA_Non_Una_Discordanza()
    {
        // ⚠️ Misurato dal vivo il 29 agosto 2026: su 54 righe di canale, 49 erano «l'AIP ce l'ha e noi no» e
        // solo 5 erano canali davvero diversi. Tenendole insieme, i cinque che contano sparivano.
        var aip = AirspaceNavaidReader.LeggiKml(Kml);

        var esito = NavaidAipReport.Confronta(aip, [Nostra("AHO", "VHF", "TACAN", "109.3", null, 40.6361, 8.28944)]);

        var d = Assert.Single(esito, x => x.Kind == NavaidDiffKind.CanaleMancante);
        Assert.Equal("30X", d.Aip);
        Assert.Null(d.Nostro);
        Assert.DoesNotContain(esito, x => x.Kind == NavaidDiffKind.CanaleDiverso);
    }

    [Fact]
    public void Due_Canali_Diversi_Sono_Una_DISCORDANZA_E_Vengono_Prima()
    {
        var aip = AirspaceNavaidReader.LeggiKml(Kml);

        var esito = NavaidAipReport.Confronta(aip, [
            Nostra("AHO", "VHF", "TACAN", "109.3", "31X", 40.6361, 8.28944),
            Nostra("ZZZ", "NDB", null, "400"),
        ]);

        var d = Assert.Single(esito, x => x.Kind == NavaidDiffKind.CanaleDiverso);
        Assert.Equal("30X", d.Aip);
        Assert.Equal("31X", d.Nostro);
        // ⚠️ L'ordine è la gravità: una discordanza sta prima di un'assenza.
        var iDiscordanza = esito.ToList().IndexOf(d);
        var iAssenza = esito.ToList().FindIndex(x => x.Kind == NavaidDiffKind.SoloInAnagrafica);
        Assert.True(iDiscordanza < iAssenza, $"discordanza in {iDiscordanza}, assenza in {iAssenza}");
    }

    [Fact]
    public void Una_Posizione_Lontana_Si_Dice_Con_Quanto()
    {
        var aip = AirspaceNavaidReader.LeggiKml(Kml);

        var esito = NavaidAipReport.Confronta(aip, [Nostra("AHO", "VHF", "TACAN", "109.3", "30X", 40.70, 8.28944)]);

        var d = Assert.Single(esito, x => x.Kind == NavaidDiffKind.PosizioneDiversa);
        Assert.Contains("NM", d.Nota);
    }

    [Fact]
    public void Uno_Scarto_Di_Arrotondamento_Non_E_Una_Differenza()
    {
        var aip = AirspaceNavaidReader.LeggiKml(Kml);

        var esito = NavaidAipReport.Confronta(aip, [Nostra("AHO", "VHF", "TACAN", "109.3", "30X", 40.6362, 8.28945)]);

        Assert.DoesNotContain(esito, d => d.Kind == NavaidDiffKind.PosizioneDiversa);
    }

    [Fact]
    public void Chi_Ce_Da_Una_Parte_Sola_Si_Dice_Da_Che_Parte()
    {
        var aip = AirspaceNavaidReader.LeggiKml(Kml);

        var esito = NavaidAipReport.Confronta(aip, [Nostra("XXX", "VHF", "VOR", "112.0")]);

        Assert.Contains(esito, d => d.Kind == NavaidDiffKind.SoloNellAip && d.Code == "AHO");
        Assert.Contains(esito, d => d.Kind == NavaidDiffKind.SoloInAnagrafica && d.Code == "XXX");
    }

    [Fact]
    public void Lo_Stesso_Codice_Due_Volte_Si_Guarda_A_Mano()
    {
        // ⚠️ Il caso vero: `GRO` è due volte in VHF — Grosseto è un VOR e un TACAN — sia nel file sia da noi.
        // Accoppiarli a indovinare produrrebbe due differenze inventate.
        var aip = AirspaceNavaidReader.LeggiKml(Kml);
        var nostre = new[]
        {
            Nostra("AHO", "VHF", "TACAN", "109.3", "30X", 40.6361, 8.28944),
            Nostra("AHO", "VHF", "VOR", "112.0", null, 40.6361, 8.28944),
        };

        var d = Assert.Single(NavaidAipReport.Confronta(aip, nostre), x => x.Kind == NavaidDiffKind.DaGuardareAMano);
        Assert.Equal("AHO", d.Code);
        Assert.Equal("2", d.Nostro);
    }

    [Fact]
    public void Due_Archivi_Vuoti_Non_Fanno_Rumore()
    {
        Assert.Empty(NavaidAipReport.Confronta([], []));
        Assert.Empty(AirspaceNavaidReader.LeggiKml(null));
        Assert.Empty(AirspaceNavaidReader.LeggiKml("non xml"));
    }
}
