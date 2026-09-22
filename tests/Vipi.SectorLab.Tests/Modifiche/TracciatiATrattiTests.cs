using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// I tracciati a più tratti (carta F3, slice 7-bis): il <c>Track</c> delle <c>.sid</c>, che tiene i punti dentro
/// un involucro (etichetta e «nuovo tratto»), e le zone dei <c>.str</c>, che li tengono dentro i loro segmenti.
/// </summary>
public sealed class TracciatiATrattiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private (FileAperto File, int Indice) Sid()
    {
        var sessione = Apri();
        var file = sessione.File["SectorFiles/Include/IT/lied.sid"];
        int indice = ((IFileConRecord)file).RecordDelModello
            .Select((r, i) => (r, i)).First(v => ElenchiDiVertici.Di(file, v.i).Any(e => e.Quanti > 2)).i;
        return (file, indice);
    }

    private (FileAperto File, int Indice) ZonaStr()
    {
        var sessione = Apri();
        var file = sessione.File["SectorFiles/Include/IT/lirf.str"];
        int indice = ((IFileConRecord)file).RecordDelModello
            .Select((r, i) => (r, i)).First(v => ElenchiDiVertici.Di(file, v.i).Count(e => e.Quanti > 1) > 1).i;
        return (file, indice);
    }

    [Fact]
    public void UnaSidHaIlSuoTracciato()
    {
        var (file, indice) = Sid();

        var elenchi = ElenchiDiVertici.Di(file, indice);

        var tracciato = Assert.Single(elenchi, e => e.Chiave == "Track");
        Assert.Equal("Tracciato", tracciato.Nome);
        Assert.True(tracciato.Quanti > 2);
        // Le SID ammettono i nomi: il tracciato è fatto di fix.
        Assert.True(tracciato.AmmetteNomi);
    }

    [Fact]
    public void SpostareUnPuntoDiUnaSidCambiaUnaRigaSola()
    {
        var (file, indice) = Sid();

        var fatta = _modifiche.CambiaVertice(file, indice, "Track", 1, "N041.00.00.000 E012.00.00.000");

        Assert.IsType<ModificaDeiVertici>(fatta);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
    }

    [Fact]
    public void SpostandoUnPuntoDiUnaSidLEtichettaEIlTrattoRESTANO()
    {
        // Un punto che porta qualcosa in più (etichetta o «nuovo tratto»): se l'involucro si perdesse, il diff
        // toccherebbe altre righe. Si cerca in tutto il file, non solo nel primo record.
        var sessione = Apri();
        var file = sessione.File["SectorFiles/Include/IT/lied.sid"];
        var (indice, quale) = ((IFileConRecord)file).RecordDelModello
            .SelectMany((_, r) => Enumerable.Range(0, ElenchiDiVertici.Uno(file, r, "Track")?.Quanti ?? 0).Select(p => (Record: r, Punto: p)))
            .First(v => ElenchiDiVertici.Uno(file, v.Record, "Track")!.Elenco[v.Punto]
                is PuntoDelTracciato { Etichetta.Length: > 0 } or PuntoDelTracciato { NuovoTratto: true });
        var tracciato = ElenchiDiVertici.Uno(file, indice, "Track")!;
        var prima = (PuntoDelTracciato)tracciato.Elenco[quale]!;
        string? etichetta = prima.Etichetta;
        bool nuovoTratto = prima.NuovoTratto;

        _modifiche.CambiaVertice(file, indice, "Track", quale, "N041.00.00.000 E012.00.00.000");

        var dopo = (PuntoDelTracciato)ElenchiDiVertici.Uno(file, indice, "Track")!.Elenco[quale]!;
        Assert.Equal(etichetta, dopo.Etichetta);
        Assert.Equal(nuovoTratto, dopo.NuovoTratto);
        Assert.Equal(1, _modifiche.DiffDi(file).Tolte);
    }

    [Fact]
    public void UnaZonaStrHaUnElencoPerTRATTO()
    {
        var (file, indice) = ZonaStr();

        var elenchi = ElenchiDiVertici.Di(file, indice);

        Assert.True(elenchi.Count > 1, $"tratti trovati: {elenchi.Count}");
        Assert.All(elenchi, e => Assert.StartsWith("Segments[", e.Chiave, StringComparison.Ordinal));
        Assert.Equal("Tratto 1", elenchi[0].Nome);
        // I punti di una zona .str sono coordinate e basta: i nomi lì non si scrivono.
        Assert.All(elenchi, e => Assert.False(e.AmmetteNomi));
    }

    [Fact]
    public void SiModificaUnTrattoSoloELAltroNonSiMuove()
    {
        var (file, indice) = ZonaStr();
        var elenchi = ElenchiDiVertici.Di(file, indice);
        var secondo = elenchi[1];
        var comEraIlSecondo = secondo.Elenco.Cast<object>().ToList();

        var fatta = _modifiche.CambiaVertice(file, indice, elenchi[0].Chiave, 0, "N041.00.00.000 E012.00.00.000");

        Assert.IsType<ModificaDeiVertici>(fatta);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(1, diff.Tolte);
        Assert.Equal(1, diff.Aggiunte);
        Assert.Equal(comEraIlSecondo, ElenchiDiVertici.Di(file, indice)[1].Elenco.Cast<object>());
    }

    [Fact]
    public void DueTrattiToccatiSonoDUEModificheNelPannello()
    {
        var (file, indice) = ZonaStr();
        var elenchi = ElenchiDiVertici.Di(file, indice);

        _modifiche.CambiaVertice(file, indice, elenchi[0].Chiave, 0, "N041.00.00.000 E012.00.00.000");
        _modifiche.CambiaVertice(file, indice, elenchi[1].Chiave, 0, "N042.00.00.000 E013.00.00.000");

        Assert.Equal(2, _modifiche.Quante);
        var diff = _modifiche.DiffDi(file);
        Assert.Equal(2, diff.Tolte);
        Assert.Equal(2, diff.Aggiunte);
    }

    [Fact]
    public void AggiungereETogliereInUnTrattoTornaComEra()
    {
        var (file, indice) = ZonaStr();
        string tratto = ElenchiDiVertici.Di(file, indice)[0].Chiave;
        var righeComErano = ((IFileConRecord)file).RigheDelFile([]).ToList();

        _modifiche.AggiungiVertice(file, indice, tratto, 1, "N041.00.00.000 E012.00.00.000");
        _modifiche.TogliVertice(file, indice, tratto, 1);

        Assert.False(_modifiche.CEQualcosa);
        Assert.Equal(righeComErano, ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo)));
    }

    [Fact]
    public void UnTrattoCheNonCESiRifiuta()
    {
        var (file, indice) = ZonaStr();

        var esito = _modifiche.CambiaVertice(file, indice, "Segments[99].Points", 0, "N041.00.00.000 E012.00.00.000");

        Assert.Contains("non è un elenco di vertici", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IncollareUnTestoAipInUnTrattoLoRifaSenzaToccareGliAltri()
    {
        var (file, indice) = ZonaStr();
        var elenchi = ElenchiDiVertici.Di(file, indice);
        var comEraIlSecondo = elenchi[1].Elenco.Cast<object>().ToList();

        var fatta = _modifiche.IncollaVertici(file, indice, elenchi[0].Chiave,
            "44°51'24\" N 008°14'57\" E\n" +
            "then arc of circle in clockwise direction radius 17 NM centred on\n" +
            "44°55'29\" N 007°51'43\" E\n" +
            "till point\n" +
            "44°41'08\" N 008°04'34\" E",
            puntiPerGrado: 2.0);

        var modifica = Assert.IsType<ModificaDeiVertici>(fatta);
        Assert.True(modifica.Dopo > 4);
        Assert.Equal(comEraIlSecondo, ElenchiDiVertici.Di(file, indice)[1].Elenco.Cast<object>());
    }
}
