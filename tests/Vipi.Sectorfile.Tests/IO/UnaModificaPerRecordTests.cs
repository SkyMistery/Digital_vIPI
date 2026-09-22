using System.Collections;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.IO.Tests;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Tests.IO;

/// <summary>
/// La misura dura di «riga come campi» (carta F2 §9.5), sui campioni veri: si sposta di un millesimo di secondo
/// il primo punto di OGNI record e si riscrive. Deve cambiare una riga per record, e in quella riga un campo.
/// Sull'albero intero la stessa misura la fa <c>tools/Vipi.SectorfileProva</c> (99 707 record, 99 707 righe).
/// </summary>
public sealed class UnaModificaPerRecordTests
{
    private readonly CollectingWarnings _warnings = new();

    [Fact] public void Geo() => Misura(new GeoParser(_warnings), new GeoSaver(), "RW_MARKINGS/ba_mark.geo");
    [Fact] public void Pol() => Misura(new PolParser(_warnings), new PolSaver(), "GND_LAYOUT/rf_ad_gnd.pol");
    // Non ci sono .sid né .frq: i campioni (lirf.sid, solo etichette; itfreq.frq) non hanno punti da spostare.
    [Fact] public void Str() => Misura(new StrParser(_warnings), new StrSaver(), "lirf.str");
    [Fact] public void Vor() => Misura(new VorParser(_warnings), new VorSaver(), "NAVAIDS/itvor.vor");
    [Fact] public void Rw() => Misura(new RwParser(_warnings), new RwSaver(), "OTHER/itrw.rw");
    [Fact] public void Ap() => Misura(new ApParser(_warnings), new ApSaver(), "OTHER/itap.ap");
    [Fact] public void Hartcc() => Misura(new HartccParser(_warnings), new HartccSaver(), "HI_AIRSPACE/lirr.hartcc");
    [Fact] public void Artcc() => Misura(new ArtccParser(_warnings), new ArtccSaver(), "ACC/FRA.artcc");
    [Fact] public void Tfl() => Misura(new TflParser(_warnings), new TflSaver(), "DYNAMIC_SEC/twrs.tfl");
    [Fact] public void Fic() => Misura(new FicParser(_warnings), new FicSaver(), "DYNAMIC_SEC/limmfic.tfl");
    [Fact] public void MvaDiScalo() => Misura(new MvaAirportParser(_warnings), new MvaSaver(enroute: false), "liba.mva");
    [Fact] public void MvaDiRotta() => Misura(new MvaEnrouteParser(_warnings), new MvaSaver(enroute: true), "ENRMVA/lirr.mva");
    [Fact] public void Gts() => Misura(new GtsParser(_warnings), new GtsSaver(), "lirf.gts");
    [Fact] public void Lairway() => Misura(new AirwayParser(_warnings), new AirwaySaver(), "AIRWAY/itawlow.lairway");

    private static void Misura<T>(IFileParser<T> lettore, IFileSaver<T> scrittore, string campione)
    {
        string percorso = RealSectorFiles.Path(campione)!;
        string temporaneo = Path.Combine(Path.GetTempPath(), "una-modifica-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var letto = lettore.Parse(percorso, new ColorPalette()).FissaLeBasi(scrittore);
            var spostati = new HashSet<T>(letto.Records.Where(r => r is not null && Sposta(r)));
            Assert.NotEmpty(spostati);

            new FileSaverOrchestrator().Save(letto, spostati, scrittore, temporaneo);

            string[] prima = File.ReadAllLines(percorso);
            string[] dopo = File.ReadAllLines(temporaneo);
            Assert.Equal(prima.Length, dopo.Length);
            var cambiate = prima.Zip(dopo).Where(c => c.First != c.Second).ToList();
            Assert.Equal(spostati.Count, cambiate.Count);
            Assert.All(cambiate, c => Assert.Equal(1, c.First.Split(';').Zip(c.Second.Split(';')).Count(f => f.First != f.Second)));
        }
        finally
        {
            File.Delete(temporaneo);
        }
    }

    // Lo stesso spostamento dello strumento: il primo punto del record, dovunque stia.
    private static bool Sposta(object record)
    {
        const double UnMillesimo = 1 / 3_600_000.0;
        static Coordinate Spostata(Coordinate c) => new(c.LatitudeDeg + UnMillesimo, c.LongitudeDeg);

        foreach (var p in record.GetType().GetProperties().Where(p => p.GetIndexParameters().Length == 0))
        {
            if (p.PropertyType == typeof(Coordinate) && p.CanWrite)
            {
                p.SetValue(record, Spostata((Coordinate)p.GetValue(record)!));
                return true;
            }

            if (p.PropertyType == typeof(Coordinate?) && p.CanWrite && p.GetValue(record) is Coordinate c)
            {
                p.SetValue(record, Spostata(c));
                return true;
            }

            if (p.GetValue(record) is IList<Coordinate> { Count: > 0 } punti)
            {
                punti[0] = Spostata(punti[0]);
                return true;
            }

            if (p.GetValue(record) is IList { Count: > 0 } elenco && elenco[0] is { } primo
                && primo.GetType().IsClass && primo is not string && Sposta(primo))
            {
                return true;
            }
        }

        return false;
    }
}
