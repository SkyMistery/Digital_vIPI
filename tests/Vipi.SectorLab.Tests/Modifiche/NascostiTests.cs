using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Nascondi e mostra (lotto «Subito», slice 5c, «file per file» B3, R-5, K4): un record si nasconde commentando le sue
/// righe e resta nell'elenco, grigio. Tre casi: nascosto fra gli altri (per il motore sono commenti), nascosto dentro
/// (il motore lo tiene: MVA, file a una riga per record), in parte (qualche riga commentata in un record attivo).
/// </summary>
public sealed class NascostiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private FileAperto Apri(string relativo)
        => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File["SectorFiles/Include/IT/" + relativo];

    private static int Primo<T>(FileAperto file, Func<T, bool> quale)
        => ((IFileConRecord)file).RecordDelModello.Select((r, i) => (r, i)).First(v => v.r is T t && quale(t)).i;

    private IReadOnlyList<string> Righe(FileAperto file)
        => ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo));

    private static int Record(FileAperto file) => ((IFileConRecord)file).RecordDelModello.Count;

    [Theory]
    [InlineData("//L;LIRR;N041.21.09.286;E014.24.18.900;100;8; //NAPOLI CTA", true)]
    [InlineData("  //LIRR;0;0;N041.48.01.000;E012.14.20.000; Roma Area;", true)]
    [InlineData("//SARDEGNA", false)]
    [InlineData("//discontinuity (creates a break)", false)]
    [InlineData("//@\"L613\" locked=si", false)]
    [InlineData("T;L613;RIVAM;RIVAM;", false)]
    public void UnaRigaNascostaEUnCommentoCheHaDeiDati(string riga, bool nascosta)
        => Assert.Equal(nascosta, Nascosti.Commentata(riga));

    [Fact]
    public void UnaSidNascostaEsceDalMotoreMaRestaFraGliAltri()
    {
        var file = Apri("lied.sid");
        int north = Primo<SidProcedure>(file, s => s.Name == "NORTH DEP16");
        int record = Record(file);
        var prima = Righe(file);

        var fatta = _modifiche.Nascondi(file, north, "NORTH DEP16");

        Assert.Equal("NORTH DEP16: nascosto", Assert.IsType<ModificaDelTesto>(fatta).Descrizione);
        Assert.Equal(record - 1, Record(file));
        Assert.All(Righe(file).Where((r, i) => r != prima[i]), r => Assert.StartsWith("//", r, StringComparison.Ordinal));
        var blocco = Assert.Single(_modifiche.NascostiDi(file).Fra);
        Assert.Contains("NORTH DEP16", blocco.Etichetta, StringComparison.Ordinal);
        Assert.Equal(north - 1, blocco.DopoIlRecord);

        _modifiche.Mostra(file, blocco);

        Assert.Equal(prima, Righe(file));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void LeRigheDisattivateDiUnFileAUnaRigaSonoNascosteDentro()
    {
        // itap.ap: `//LIRR;0;0;…; Roma Area;` — il lettore la tiene come record disattivato. Si mostra e si rinasconde.
        var file = Apri("OTHER/itap.ap");
        var analisi = _modifiche.NascostiDi(file);
        Assert.Equal(4, analisi.Dentro.Count);
        int lirr = analisi.Dentro.Single(i => Righe(file)[((IFileConRecord)file).PostiDeiRecord([])[i].Da].Contains("Roma Area", StringComparison.Ordinal));
        var prima = Righe(file);

        Assert.IsType<ModificaDelTesto>(_modifiche.MostraNelRecord(file, lirr));
        Assert.Contains("LIRR;0;0;N041.48.01.000;E012.14.20.000; Roma Area;", Righe(file));

        Assert.IsType<ModificaDelTesto>(_modifiche.Nascondi(file, lirr));
        Assert.Equal(prima, Righe(file));
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void UnaZonaMvaConIVerticiCommentatiENascostaInParte()
    {
        // ENRMVA/lirr.mva, prima zona: la riga L attiva, le nove T commentate (l'etichetta si vede, il poligono no).
        var file = Apri("ENRMVA/lirr.mva");
        var analisi = _modifiche.NascostiDi(file);
        var sue = analisi.InParte[0];
        Assert.Equal(9, sue.Count);

        _modifiche.MostraNelRecord(file, 0);

        var diff = _modifiche.DiffDi(file);
        Assert.Equal(9, diff.Tolte);
        Assert.Equal(9, diff.Aggiunte);
        Assert.Equal(9, ((MvaSector)((IFileConRecord)file).RecordDelModello[0]).Vertices.Count);
    }

    [Fact]
    public void UnaZonaMvaNascostaTieneAttivoIlSeparatoreERestaUnRecord()
    {
        var file = Apri("ENRMVA/lirr.mva");
        int zona = Primo<MvaSector>(file, m => m.Vertices.Count > 3);
        int record = Record(file);

        _modifiche.Nascondi(file, zona);

        Assert.Equal(record, Record(file));
        Assert.Contains(zona, _modifiche.NascostiDi(file).Dentro);
        var posto = ((IFileConRecord)file).PostiDeiRecord(_modifiche.SporchiDi(file.Relativo))[zona];
        var suoi = Righe(file).Skip(posto.Da).Take(posto.Quante).ToList();
        Assert.Contains("T;DUMMY;N000.00.00.000;E000.00.00.000;", suoi);
        Assert.All(suoi.Where(r => !r.StartsWith("T;DUMMY", StringComparison.Ordinal) && r.Length > 0), r => Assert.StartsWith("//", r, StringComparison.Ordinal));
    }

    [Fact]
    public void UnRecordCoiMetadatiNonSiNasconde()
    {
        var file = Apri("AIRWAY/itawlow.lairway");
        int l613 = Primo<Airway>(file, a => a.Name == "L613" && a.FixLabels.Count > 2);
        _modifiche.CambiaIlMetadato(file, l613, "note", "prova");

        var esito = _modifiche.Nascondi(file, l613);

        Assert.Contains("metadati", Assert.IsType<ModificaRifiutata>(esito).Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void GliAtisNonSiNascondono()
    {
        var file = Apri("lica.atis");

        Assert.IsType<ModificaRifiutata>(_modifiche.Nascondi(file, 0));
        Assert.Empty(_modifiche.NascostiDi(file).Fra);
    }
}
