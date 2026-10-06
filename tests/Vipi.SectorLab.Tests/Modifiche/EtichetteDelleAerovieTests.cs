using Vipi.SectorLab.Core.Modifiche;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Le etichette calcolate, aggiungere e togliere un'aerovia (lotto «Subito» slice 14d-14f; B4, B14, B15), sulle righe:
/// punti inventati a distanze note, lungo il parallelo 45° (un primo di longitudine = 0,707 NM).
/// </summary>
public sealed class EtichetteDelleAerovieTests
{
    // AAAAA —21 NM— BBBBB —21 NM— CCCCC —7 NM— DDDDD —21 NM— EEEEE, e NNNNN 30 NM a nord di AAAAA.
    private static readonly Dictionary<string, Coordinate> Punti = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AAAAA"] = new(45, 10.0),
        ["BBBBB"] = new(45, 10.5),
        ["CCCCC"] = new(45, 11.0),
        ["DDDDD"] = new(45, 11.0 + (10.0 / 60)),
        ["EEEEE"] = new(45, 11.0 + (40.0 / 60)),
        ["NNNNN"] = new(45.5, 10.0),
    };

    private static Coordinate? Punto(string nome) => Punti.TryGetValue(nome, out var dove) ? dove : null;

    private static readonly string[] Tracciati =
    [
        "//Airway tracks",
        "T;L1;AAAAA;AAAAA;",
        "T;L1;BBBBB;BBBBB;",
        "T;L1;CCCCC;CCCCC;",
        "T;L1;DDDDD;DDDDD;",
        "T;L1;EEEEE;EEEEE;",
        "T;M2;BBBBB;BBBBB;",
        "T;M2;CCCCC;CCCCC;",
        "T;N3;AAAAA;AAAAA;",
        "T;N3;NNNNN;NNNNN;",
        "",
        "//Airway labels",
    ];

    private static List<string> Applica(IReadOnlyList<string> righe, IReadOnlyDictionary<int, IReadOnlyList<string>> sostituzioni)
    {
        var nuove = new List<string>();
        for (int i = 0; i < righe.Count; i++)
            nuove.AddRange(sostituzioni.TryGetValue(i + 1, out var alPosto) ? alPosto : [righe[i]]);
        return nuove;
    }

    private static PianoDelleEtichette Piano(IReadOnlyList<string> righe, double soglia = 10, string? solo = null)
        => EtichetteDelleAerovie.Piano(righe, Punto, soglia, solo);

    [Fact]
    public void UnTrattoLungoSenzaEtichettaNeRiceveUnaAMeta_UnoCortoNo()
    {
        string[] righe = [.. Tracciati, "L;L1;N045.00.00.000;E010.15.00.000;"];

        var piano = Piano(righe);

        // A-B ha già la sua. Mancano: B-C (condiviso da L1 e M2), D-E, e A-N della N3. C-D è lungo 7 NM: sotto la soglia.
        Assert.Equal(["L1", "L1-M2", "N3"], piano.DaAggiungere.Select(a => a.Nome));
        Assert.Equal("L;L1-M2;N045.00.00.000;E010.45.00.000;", piano.DaAggiungere[1].Riga);
        Assert.Equal("L;N3;N045.15.00.000;E010.00.00.000;", piano.DaAggiungere[2].Riga);
        Assert.Empty(piano.DaRinominare);
        Assert.Empty(piano.DaTogliere);

        // Con la soglia a zero anche il tratto corto ha la sua.
        Assert.Equal(4, Piano(righe, soglia: 0).DaAggiungere.Count);
    }

    [Fact]
    public void LeNuoveVannoFraLeEtichetteInOrdineDiNomeEIlRestoNonSiTocca()
    {
        string[] righe = [.. Tracciati, "L;L1;N045.00.00.000;E010.15.00.000;", "L;N3;N045.15.00.000;E010.00.00.000;"];

        var piano = Piano(righe);
        var dopo = Applica(righe, piano.Sostituzioni(righe));

        Assert.Equal(Tracciati, dopo.Take(Tracciati.Length));
        Assert.Equal(
        [
            "L;L1;N045.00.00.000;E010.15.00.000;",
            // Le due nuove della L1 dopo l'ultima etichetta della L1, prima della N3.
            "L;L1;N045.00.00.000;E011.25.00.000;",
            "L;L1-M2;N045.00.00.000;E010.45.00.000;",
            "L;N3;N045.15.00.000;E010.00.00.000;",
        ], dopo.Skip(Tracciati.Length));
        // Rifatto sul file sistemato, il piano non trova più niente.
        Assert.True(Piano(dopo).Vuoto);
    }

    [Fact]
    public void UnEtichettaLontanaSiToglieEIlSuoTrattoNeRiceveUnaAMeta()
    {
        string[] righe =
        [
            .. Tracciati,
            // Sul primo tratto ma non a metà: resta dov'è.
            "L;L1;N045.00.00.000;E010.05.00.000;",
            // Tre miglia a nord dell'ultimo tratto: si rifà a metà.
            "L;L1;N045.03.00.000;E011.25.00.000;",
            "L;L1-M2;N045.00.00.000;E010.45.00.000;",
            "L;N3;N045.15.00.000;E010.00.00.000;",
        ];

        var piano = Piano(righe);

        var tolta = Assert.Single(piano.DaTogliere);
        Assert.Equal(Tracciati.Length + 2, tolta.Riga);
        Assert.Equal(3, tolta.LontanaNm, 1);
        Assert.Equal("L;L1;N045.00.00.000;E011.25.00.000;", Assert.Single(piano.DaAggiungere).Riga);
        var dopo = Applica(righe, piano.Sostituzioni(righe));
        Assert.Contains("L;L1;N045.00.00.000;E010.05.00.000;", dopo);
        Assert.DoesNotContain("L;L1;N045.03.00.000;E011.25.00.000;", dopo);
        Assert.True(Piano(dopo).Vuoto);
    }

    [Fact]
    public void UnEtichettaColNomeSbagliatoPrendeQuelloGiustoERestaDove()
    {
        string[] righe =
        [
            .. Tracciati,
            // Un nome che non c'è più accanto a quello vero; un tratto condiviso col nome di una sola.
            "L;UL1-L1;N045.00.00.000;E010.15.00.000;",
            "L;M2;N045.00.00.000;E010.40.00.000;",
            "L;L1;N045.00.00.000;E011.25.00.000;",
            "L;N3;N045.15.00.000;E010.00.00.000;",
        ];

        var piano = Piano(righe);

        Assert.Equal(
            [("L;UL1-L1;N045.00.00.000;E010.15.00.000;", "L;L1;N045.00.00.000;E010.15.00.000;"),
             ("L;M2;N045.00.00.000;E010.40.00.000;", "L;L1-M2;N045.00.00.000;E010.40.00.000;")],
            piano.DaRinominare.Select(r => (r.Prima, r.Dopo)));
        Assert.Empty(piano.DaAggiungere);
        Assert.Empty(piano.DaTogliere);
    }

    [Fact]
    public void UnAeroviaNuovaHaIlSuoBloccoInOrdineDiNomeEIlPianoSoloSuo()
    {
        string[] righe = [.. Tracciati, "L;L1;N045.00.00.000;E010.15.00.000;"];

        var gesto = AerovieAMano.Nuova(righe, "M1", AerovieAMano.Punti("ccccc, ddddd eeeee"), Punto);

        Assert.Null(gesto.Perche);
        var dopo = Applica(righe, gesto.Sostituzioni!);
        // Dopo la L1 (il nome più grande che non supera M1), prima della M2.
        Assert.Equal(
        [
            "T;L1;EEEEE;EEEEE;",
            "//@\"M1\" locked=si",
            "//@START",
            "//@@\"CCCCC\" dir=both",
            "T;M1;CCCCC;CCCCC;",
            "//@@\"DDDDD\" dir=both",
            "T;M1;DDDDD;DDDDD;",
            "T;M1;EEEEE;EEEEE;",
            "//@END \"M1\"",
            "T;M2;BBBBB;BBBBB;",
        ], dopo.Skip(5).Take(10));

        // Le sue etichette: il tratto D-E, che ora condivide con la L1 (C-D è sotto la soglia). Le altre aerovie non si toccano.
        var piano = Piano(dopo, solo: "M1");
        Assert.Equal(["L1-M1"], piano.DaAggiungere.Select(a => a.Nome));
        Assert.Empty(piano.DaRinominare);
    }

    [Theory]
    [InlineData("L1", "AAAAA BBBBB", "c'è già")]
    [InlineData("", "AAAAA BBBBB", "nome")]
    [InlineData("L 9", "AAAAA BBBBB", "spazi")]
    [InlineData("L9-M9", "AAAAA BBBBB", "trattino")]
    [InlineData("BREAK", "AAAAA BBBBB", "BREAK")]
    [InlineData("Z9", "AAAAA", "almeno due")]
    [InlineData("Z9", "AAAAA ZZZZZ", "ZZZZZ")]
    [InlineData("Z9", "AAAAA AAAAA BBBBB", "due volte")]
    public void UnAeroviaNuovaCheNonVaSiRifiutaColPerche(string nome, string punti, string perche)
    {
        var gesto = AerovieAMano.Nuova(Tracciati, nome, AerovieAMano.Punti(punti), Punto);

        Assert.Null(gesto.Sostituzioni);
        Assert.Contains(perche, gesto.Perche, StringComparison.Ordinal);
    }

    [Fact]
    public void ToltaUnAeroviaSparisconoTracciatoTagEIlSuoNomeDalleEtichette()
    {
        string[] righe =
        [
            "//Airway tracks",
            "//@\"L1\" locked=si",
            "//@START",
            "//@@\"AAAAA\" dir=both lower=FL95",
            "T;L1;AAAAA;AAAAA;",
            "T;L1;BBBBB;BBBBB;",
            "T;BREAK;BBBBB;BBBBB;",
            "T;L1;CCCCC;CCCCC;",
            "T;L1;DDDDD;DDDDD;",
            "//@END \"L1\"",
            "T;M2;BBBBB;BBBBB;",
            "T;M2;CCCCC;CCCCC;",
            "",
            "//Airway labels",
            "L;L1;N045.00.00.000;E010.15.00.000;",
            "L;L1-M2;N045.00.00.000;E010.45.00.000;",
        ];

        var gesto = AerovieAMano.Togli(righe, "L1", Punto);

        Assert.Equal(
        [
            "//Airway tracks",
            "T;M2;BBBBB;BBBBB;",
            "T;M2;CCCCC;CCCCC;",
            "",
            "//Airway labels",
            "L;M2;N045.00.00.000;E010.45.00.000;",
        ], Applica(righe, gesto.Sostituzioni!));
        Assert.Contains("non c'è", AerovieAMano.Togli(righe, "Q9", Punto).Perche, StringComparison.Ordinal);
    }
}
