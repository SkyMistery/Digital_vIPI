using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F3-bis, slice 1: le copie gemelle (lo stesso scalo, pista, posizione nel file nazionale e in quelli delle FIR)
/// e la regola <see cref="Regola.CopieDiverse"/>. Le divergenze sono quelle vere dei campioni (<c>LIBA</c> a 182 ft in
/// <c>itap.ap</c> e 185 in <c>lirr.ap</c>, le prue di <c>LIRE</c>), misurate sul fork nella slice 0.
/// </summary>
public sealed class CopieGemelleTests : IDisposable
{
    private const string Ap = "SectorFiles/Include/IT/OTHER/itap.ap";
    private const string ApFir = "SectorFiles/Include/IT/OTHER/lirr.ap";

    private readonly string _radice = Path.Combine(Path.GetTempPath(), "gemelli-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static AirportInfo Scalo(string icao, int quota = 13, bool commentato = false)
        => new() { IcaoCode = icao, ElevationFt = quota, Name = "SCALO", IsDisabled = commentato };

    [Fact]
    public void LaChiaveEIcaoPisteOCodiceDellaPosizione()
    {
        Assert.Equal("LIRF", CopieGemelle.Chiave(Scalo("LIRF")));
        Assert.Equal("LIRF 16L/34R", CopieGemelle.Chiave(new Runway { IcaoCode = "LIRF", Designator1 = "16L", Designator2 = "34R" }));
        Assert.Equal("LIRR_NE_CTR", CopieGemelle.Chiave(new AtcPosition { Code = "LIRR_NE_CTR" }));
        Assert.Null(CopieGemelle.Chiave(new Fix()));
        // //LIBB;… commentato in itap.ap e libb.ap: Aurora non lo legge, e sul fork dava una «copia diversa» per uno spazio.
        Assert.Null(CopieGemelle.Chiave(Scalo("LIBB", commentato: true)));
    }

    [Theory]
    [InlineData("SectorFiles/Include/IT/OTHER/lirr.ap", "sectorfiles/include/it/other/*.ap")]
    [InlineData(@"SectorFiles\Include\IT\OTHER\LIRR.RW", "sectorfiles/include/it/other/*.rw")]
    [InlineData("itfreq.frq", "*.frq")]
    [InlineData("SectorFiles/Include/IT/lirf.str", null)]
    [InlineData("SectorFiles/Include/IT.ap/leggimi", null)]
    public void LaFamigliaECartellaPiuEstensione(string percorso, string? famiglia)
        => Assert.Equal(famiglia, CopieGemelle.Famiglia(percorso));

    [Fact]
    public void SonoGemelleSoloLeChiaviInPiuFileDellaStessaFamiglia()
    {
        var gruppi = CopieGemelle.Trova([
            (Ap, [Scalo("LIRF"), Scalo("LIML")]),
            (ApFir, [Scalo("LIRF")]),
            ("SectorFiles/Include/IT/ALTRO/lirr.ap", [Scalo("LIML")]),   // altra cartella, altra famiglia
        ]);

        var gruppo = Assert.Single(gruppi);
        Assert.Equal(("LIRF", true), (gruppo.Chiave, gruppo.PerOrdine));
        Assert.Equal([(Ap, 0), (ApFir, 0)], gruppo.Copie.Select(c => (c.File, c.Indice)));
    }

    // D10: la chiave che si ripete va la prima con la prima, ma solo se ogni file ne ha lo stesso numero.
    [Fact]
    public void UnaChiaveRipetutaSiAbbinaPerOrdineSoloAParitaDiNumero()
    {
        var pari = CopieGemelle.Trova([
            (Ap, [Scalo("LIMF", 1), Scalo("LIMF", 2)]),
            (ApFir, [Scalo("LIMF", 1), Scalo("LIMF", 2)]),
        ]);
        Assert.Equal(2, pari.Count);
        Assert.All(pari, g => Assert.True(g.PerOrdine));
        Assert.Equal([1, 1], pari[0].Copie.Select(c => ((AirportInfo)c.Record).ElevationFt));
        Assert.Equal([2, 2], pari[1].Copie.Select(c => ((AirportInfo)c.Record).ElevationFt));

        var dispari = Assert.Single(CopieGemelle.Trova([
            (Ap, [Scalo("LIPY")]),
            (ApFir, [Scalo("LIPY"), Scalo("LIPY")]),
        ]));
        Assert.False(dispari.PerOrdine);
        Assert.Equal(3, dispari.Copie.Count);
    }

    [Fact]
    public void FuoriPostoEChiNonECommeLaMaggioranzaESenzaMaggioranzaTutti()
    {
        var tre = Assert.Single(CopieGemelle.Trova([
            (Ap, [Scalo("LIBA", 182)]),
            (ApFir, [Scalo("LIBA", 185)]),
            ("SectorFiles/Include/IT/OTHER/libb.ap", [Scalo("LIBA", 185)]),
        ]));
        var (copia, campi) = Assert.Single(CopieGemelle.Divergenti(tre));
        Assert.Equal(Ap, copia.File);
        Assert.Equal(["ElevationFt"], campi);

        var due = Assert.Single(CopieGemelle.Trova([(Ap, [Scalo("LICK", 22)]), (ApFir, [Scalo("LICK", 23)])]));
        Assert.Equal(2, CopieGemelle.Divergenti(due).Count);

        var uguali = Assert.Single(CopieGemelle.Trova([(Ap, [Scalo("LIRF")]), (ApFir, [Scalo("LIRF")])]));
        Assert.Empty(CopieGemelle.Divergenti(uguali));
    }

    // Sui campioni veri: LIBA (quota), LICK (nome), LIRE 31/13 e 31R/13L (prue). Due copie per chiave, nessuna
    // maggioranza: tutte e due fuori posto, 8 avvisi per 4 chiavi.
    [Fact]
    public void IlValidatoreDiceLeCopieDiverseDeiCampioni()
    {
        foreach (string nome in new[] { "itap.ap", "lirr.ap", "itrw.rw", "lirr.rw", "itfreq.frq", "lirr.frq" })
        {
            string destinazione = Path.Combine(_radice, "Include", "IT", "OTHER", nome);
            Directory.CreateDirectory(Path.GetDirectoryName(destinazione)!);
            File.Copy(RealSectorFiles.Path("OTHER/" + nome)!, destinazione);
        }

        var copie = Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == Regola.CopieDiverse).ToList();

        Assert.Equal(["LIBA", "LICK", "LIRE 31/13", "LIRE 31R/13L"],
            copie.Select(p => p.Dettaglio[1..p.Dettaglio.IndexOf('»')]).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(8, copie.Count);
        Assert.All(copie, p => Assert.Equal(Gravita.Avviso, p.Gravita));

        var liba = copie.Single(p => p.Dettaglio.StartsWith("«LIBA»", StringComparison.Ordinal) && p.File.EndsWith("itap.ap", StringComparison.Ordinal));
        Assert.Equal(8, liba.Riga);
        Assert.StartsWith("LIBA;182;", liba.Testo, StringComparison.Ordinal);
        Assert.Equal("«LIBA»: qui ElevationFt 182; in lirr.ap:8 ElevationFt 185", liba.Dettaglio);
    }
}
