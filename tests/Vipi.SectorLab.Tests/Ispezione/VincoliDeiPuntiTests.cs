using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// I vincoli dei punti di una procedura (lotto «Subito», slice 9d; «file per file» Q2, P11): il tag <c>//@@</c> sopra il
/// punto, con ruolo, quota e velocità nelle forme di §M.
/// </summary>
public sealed class VincoliDeiPuntiTests : IDisposable
{
    private const string Str = "SectorFiles/Include/IT/lirf.str";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private static int Indice(FileAperto file, string nome)
        => ((IFileConRecord)file).RecordDelModello.Select((r, i) => (r, i)).First(v => v.r is StrRecord { ProcedureId: var p } && p == nome).i;

    private List<string> Dopo(FileAperto file) => [.. ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo))];

    private static (bool Ok, string? Scritto) Normalizza(string chiave, string valore)
        => (ValoriDeiMetadati.Normalizza(chiave, valore, null, out string? scritto, out _), scritto);

    [Theory]
    [InlineData("alt", "+fl80", "+FL80")]
    [InlineData("alt", "-5000ft", "-5000")]
    [InlineData("alt", "= 4000", "=4000")]
    [InlineData("alt", "4000/FL80", "4000/FL80")]
    [InlineData("spd", "-210", "-210")]
    [InlineData("spd", "+180 kt", "+180")]
    [InlineData("role", "mapt", "MAPt")]
    [InlineData("role", "iaf", "IAF")]
    public void RuoloQuotaEVelocitaSiScrivonoComeInM(string chiave, string scritto, string atteso)
        => Assert.Equal((true, atteso), Normalizza(chiave, scritto));

    [Theory]
    [InlineData("alt", "5000")]
    [InlineData("alt", "+alto")]
    [InlineData("spd", "210")]
    [InlineData("spd", "-900")]
    [InlineData("role", "FAP")]
    public void UnVincoloSenzaFormaSiRifiuta(string chiave, string scritto)
        => Assert.False(Normalizza(chiave, scritto).Ok);

    [Fact]
    public void IPuntiSonoQuelliDelleRigheSenzaICommentati()
    {
        var file = Apri().File[Str];

        var punti = ((IFileConRecord)file).PuntiConTag(Indice(file, "ELKA3A"));

        Assert.Equal(["ELKAP", "BIBEK", "GOPOL", "GIXOM", "USIRU", "GIPAP", "RF424", "RF426", "SUVOK"], punti.Select(p => p.Punto));
        Assert.All(punti, p => Assert.Empty(p.Chiavi));
    }

    [Fact]
    public void IlTagVaSopraIlSuoPuntoEIPuntiDopoNonSiSpostano()
    {
        var file = Apri().File[Str];
        int elka = Indice(file, "ELKA3A");

        Assert.IsType<ModificaDelMetadato>(_modifiche.CambiaIlTagDelPunto(file, elka, 1, "role", "IAF"));
        Assert.IsType<ModificaDelMetadato>(_modifiche.CambiaIlTagDelPunto(file, elka, 1, "alt", "+FL80"));
        // Il terzo punto è ancora il terzo, anche con una riga di tag in più sopra il secondo.
        Assert.IsType<ModificaDelMetadato>(_modifiche.CambiaIlTagDelPunto(file, elka, 2, "spd", "-210"));

        var righe = Dopo(file);
        int bibek = righe.IndexOf("BIBEK;BIBEK;", righe.IndexOf("LIRF;16L:16R;ELKA3A;;;;;1;"));
        Assert.Equal("//@@\"BIBEK\" role=IAF alt=+FL80", righe[bibek - 1]);
        Assert.Equal("//@@\"GOPOL\" spd=-210", righe[bibek + 1]);
        Assert.Equal("GOPOL;GOPOL;", righe[bibek + 2]);
        Assert.Equal(["role", "alt"], ((IFileConRecord)file).PuntiConTag(elka)[1].Chiavi.Keys);

        // Tolte le chiavi, il tag sparisce e il file torna quello dell'apertura.
        _modifiche.CambiaIlTagDelPunto(file, elka, 1, "role", null);
        _modifiche.CambiaIlTagDelPunto(file, elka, 1, "alt", null);
        _modifiche.CambiaIlTagDelPunto(file, elka, 2, "spd", null);
        Assert.Equal(0, _modifiche.Quante);
        Assert.DoesNotContain(Dopo(file), r => r.StartsWith("//@@", StringComparison.Ordinal));
    }

    [Fact]
    public void IVincoliViaggianoConLaFormaDellaMappa()
    {
        var sessione = Apri();
        var file = sessione.File[Str];
        int elka = Indice(file, "ELKA3A");
        _modifiche.CambiaIlTagDelPunto(file, elka, 1, "role", "IAF");
        _modifiche.CambiaIlTagDelPunto(file, elka, 1, "alt", "+FL80");

        var forme = Vipi.SectorLab.Core.Mappa.Geometria.DelFile(file, CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"]);

        Assert.Equal("BIBEK role=IAF alt=+FL80", forme.Single(f => f.Record == elka).Vincoli);
        Assert.Null(forme.Single(f => f.Record == Indice(file, "ELKA3B")).Vincoli);
    }

    [Fact]
    public void UnaChiaveFuoriCatalogoSiRifiuta()
    {
        var file = Apri().File[Str];

        Assert.IsType<ModificaRifiutata>(_modifiche.CambiaIlTagDelPunto(file, Indice(file, "ELKA3A"), 0, "fix", "ELKAP"));
    }
}
