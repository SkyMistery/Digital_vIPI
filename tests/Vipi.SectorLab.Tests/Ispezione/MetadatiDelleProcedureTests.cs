using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// I metadati delle procedure (lotto «Subito», slice 9c; «file per file» P6, P7, Q2b, Q2d): le chiavi del genere di
/// voce, le scelte chiuse di navigazione e avvicinamento, minimi e pendenza nella loro forma, il fix dal nome.
/// </summary>
public sealed class MetadatiDelleProcedureTests : IDisposable
{
    private const string Sid = "SectorFiles/Include/IT/lirf.sid";
    private const string Str = "SectorFiles/Include/IT/lirf.str";

    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private static int Indice(FileAperto file, string nome)
        => ((IFileConRecord)file).RecordDelModello.Select((r, i) => (r, i))
               .First(v => v.r is SidProcedure { Name: var s } && s == nome || v.r is StrRecord { ProcedureId: var p } && p == nome).i;

    private static List<string> Chiavi(FileAperto file, string nome) => [.. MetadatiDellaScheda.Di(file, Indice(file, nome)).Select(m => m.Chiave)];

    private static (bool Ok, string? Scritto, string? Perche) Normalizza(string chiave, string? valore)
        => (ValoriDeiMetadati.Normalizza(chiave, valore, null, out string? scritto, out string? perche), scritto, perche);

    [Fact]
    public void OgniVoceHaLeChiaviDelSuoGenere()
    {
        var sessione = Apri();
        var str = sessione.File[Str];

        // Una STAR: come le SID, senza la salita iniziale; niente chiavi delle mappe.
        Assert.Equal(["locked", "gen", "note", "fix", "trans", "wtc", "cat", "nav"], Chiavi(str, "ELKA3A"));
        // Un avvicinamento: in più tipo, minimi, pendenza (Q2d).
        Assert.Equal(["locked", "gen", "note", "fix", "trans", "wtc", "cat", "nav", "type", "mins", "gp"], Chiavi(str, "RNP07(TAQ)"));
        // Una mappa del MAPS: le chiavi delle mappe (composte, forme, limiti e classe), non quelle delle procedure.
        Assert.Equal(["locked", "gen", "note", "compose", "whole", "form", "lower", "upper", "class"], Chiavi(str, "LIRF CTR"));
        // Una SID: la salita iniziale sì, i minimi no.
        Assert.Equal(["locked", "gen", "note", "fix", "trans", "initialclimb", "wtc", "cat", "nav"], Chiavi(sessione.File[Sid], "OST1E"));
    }

    [Theory]
    [InlineData("nav", "rnav1", "RNAV1")]
    [InlineData("nav", "rnp apch", "RNP APCH")]
    [InlineData("type", "ils", "ILS")]
    [InlineData("mins", "a:450, b:450,c : 500,D:500", "A:450,B:450,C:500,D:500")]
    [InlineData("mins", "D:500,A:450", "A:450,D:500")]
    [InlineData("gp", "3", "3.0")]
    [InlineData("gp", "3,5", "3.5")]
    public void NavigazioneAvvicinamentoMinimiEPendenzaSiScrivonoNellaLoroForma(string chiave, string scritto, string atteso)
        => Assert.Equal((true, atteso, null), Normalizza(chiave, scritto));

    [Theory]
    [InlineData("nav", "GPS")]
    [InlineData("type", "GLS")]
    [InlineData("mins", "F:450")]
    [InlineData("mins", "A:alto")]
    [InlineData("gp", "12")]
    [InlineData("gp", "ripida")]
    public void QuelloCheNonHaLaFormaSiRifiutaColPerche(string chiave, string scritto)
    {
        var (ok, _, perche) = Normalizza(chiave, scritto);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(perche));
    }

    [Fact]
    public void NavigazioneETipoSiScelgonoDaUnElenco()
    {
        Assert.Equal(EditorDelMetadato.Scelta, ValoriDeiMetadati.EditorDi("nav", siNo: false));
        Assert.Equal(EditorDelMetadato.Scelta, ValoriDeiMetadati.EditorDi("type", siNo: false));
        Assert.Equal(["RNAV1", "RNP1", "RNP APCH"], ValoriDeiMetadati.Scelte["nav"]);
        Assert.Equal(["ILS", "LOC", "RNP", "VOR", "NDB"], ValoriDeiMetadati.Scelte["type"]);
    }

    [Theory]
    [InlineData("EKLO8R", "EKLO")]
    [InlineData("OST1E", "OST")]
    [InlineData("SOS5A-ESI8H", "SOS")]
    [InlineData("LEUCA5C", "LEUCA")]
    [InlineData("QUIRRA DEP34", null)]
    [InlineData("ALPHA SOUTH", null)]
    public void LaRadiceDelNomeEIlPunto(string nome, string? radice)
        => Assert.Equal(radice, FixDalNome.Radice(nome));

    [Fact]
    public void IlFixSiProponeDalNomeFraIPuntiDelMasterVicinoAlloScalo()
    {
        // Due fix che cominciano con EKLO vicino a Fiumicino (da scegliere), uno lontano (fuori dal raggio).
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/APT.fix",
            File.ReadAllText(_albero.Percorso("SectorFiles/Include/IT/NAVAIDS/APT.fix"))
            + "\r\nEKLOS;N041.50.00.000;E012.00.00.000;1;\r\nEKLOR;N041.40.00.000;E012.40.00.000;1;\r\nEKLOT;N046.00.00.000;E012.00.00.000;1;\r\n");
        var catalogo = CatalogoDeiPunti.PerOgniIsc(Apri())["ITALY.isc"];

        var eklo = FixDalNome.Di("EKLO8R", "LIRF", catalogo)!;
        Assert.Equal(["EKLOS", "EKLOR"], eklo.Candidati.Select(p => p.Nome));
        Assert.False(eklo.Automatico);

        // OST1E: il nome è già il navaid, un candidato solo.
        var ost = FixDalNome.Di("OST1E", "LIRF", catalogo)!;
        Assert.True(ost.Automatico);
        Assert.Equal("OST", ost.Candidati[0].Nome);

        Assert.Null(FixDalNome.Di("QUIRRA DEP34", "LIED", catalogo));
    }
}
