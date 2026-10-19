using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F7, slice 7d: le procedure dell'AIP espanso contro <c>lirf.sid</c> e <c>lirf.str</c>, in sola lettura, coi casi
/// della misura del 19-20 ottobre: i tre modi di abbreviare i nomi, la SID con la transizione, SOS5A che va bene per SOSAK e
/// SOSIV, le transizioni che LIRF stampa fra le SID, le revisioni (GILI3T contro GILIO 4T). Dati inventati.
/// </summary>
public sealed class ConfrontoDelleProcedureTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;
    private readonly string _espanso;

    public ConfrontoDelleProcedureTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _espanso = Path.Combine(_albero.Radice, "espanso");
    }

    public void Dispose() => _albero.Dispose();

    // I punti del sector e dell'AIP (gli stessi, tranne NUOVO, che l'AIP mette un miglio più a nord).
    private static readonly (string Nome, double Lat, double Lon)[] Punti =
    [
        ("ELKAP", 42.0, 12.0), ("XIBRI", 41.5, 11.5), ("SOSAK", 41.6, 12.1), ("SOSIV", 41.7, 12.0), ("ESINO", 42.5, 13.0),
        ("SOVAN", 42.0, 11.0), ("GILIO", 42.3, 11.0), ("TAQ", 42.2, 11.8), ("NUOVO", 42.0, 12.5), ("XX401", 41.9, 12.3),
    ];

    private static string Dms(double gradi, bool lat)
    {
        var g = (int)gradi;
        var p = (int)Math.Round((gradi - g) * 60);
        return $"{(lat ? 'N' : 'E')}{g:000}.{p:00}.00.000";
    }

    private static ProcedureLeg T(string pt, string? fix = null) => new() { PathTerminator = pt, Fix = fix };

    private static Procedure P(ProcedureKind genere, string nome, string[] piste, params ProcedureLeg[] tratti) => new()
    {
        Aerodrome = "LIRF", Kind = genere, Designator = nome, Runways = piste, Legs = tratti,
    };

    private async Task<IReadOnlyList<DifferenzaDiProcedura>> ConfrontaAsync(string sid, string str, params Procedure[] procedure)
    {
        var apt = _albero.Percorso("SectorFiles/Include/IT/NAVAIDS/APT.fix");
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/APT.fix", File.ReadAllText(apt).TrimEnd() + "\r\n"
            + string.Concat(Punti.Where(p => p.Nome != "XX401").Select(p => $"{p.Nome};{Dms(p.Lat, true)};{Dms(p.Lon, false)};3;\r\n")));
        _albero.Scrivi("SectorFiles/Include/IT/lirf.sid", sid);
        _albero.Scrivi("SectorFiles/Include/IT/lirf.str", str);
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLAipEspanso(_espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(_espanso, "2611"));
        var aip = new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [], Procedures = procedure,
            Points = [.. Punti.Select(p => new SignificantPoint { Section = "ENR 4.4", Designator = p.Nome, Position = new Position(p.Nome == "NUOVO" ? p.Lat + 1 / 60.0 : p.Lat, p.Lon) })],
        };
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"), AipJson.Serialize(aip));
        Assert.True(await _lab.ConfrontaLeProcedureAsync(), _lab.Rifiuto);
        return _lab.UltimoConfrontoDelleProcedure!.Value.Esiti;
    }

    [Fact]
    public async Task LeSid_ILoroNomi_ELeTransizioni()
    {
        var esiti = await ConfrontaAsync(string.Join("\r\n",
            [
                "LIRF;25;XIBR5A; ; ;0;XIBRI;1;",
                "LIRF;25;SOS5A-ESI8H; ; ;0;ESINO;1;",
                "LIRF;25;SIV5A-SOV1E; ; ;0;SOVAN;1;",
                "LIRF;25;SOSA7Y; ; ;0;TAQ;1;",
                "LIRF;07;OST1D; ; ;0;OST;1;",
                "",
            ]), "",
            P(ProcedureKind.Sid, "XIBRI 5A", ["25"], T("CA"), T("TF", "ELKAP"), T("TF", "XIBRI")),
            P(ProcedureKind.Sid, "SOSAK 5A", ["25"], T("CA"), T("TF", "SOSAK")),
            P(ProcedureKind.Sid, "SOSIV 5A", ["25"], T("CA"), T("TF", "SOSIV")),
            // LIRF stampa le transizioni fra le SID, senza pista.
            P(ProcedureKind.Sid, "ESINO 8H", [], T("IF", "SOSAK"), T("TF", "ESINO")),
            P(ProcedureKind.Sid, "SOVAN 1E", [], T("IF", "SOSIV"), T("TF", "SOVAN")),
            P(ProcedureKind.Sid, "SOSAK 7Y", ["25"], T("CA"), T("TF", "SOSAK")),
            P(ProcedureKind.Sid, "VELAD 2A", [], T("IF", "SOSAK"), T("TF", "NUOVO")));

        DifferenzaDiProcedura Riga(string nome) => esiti.Single(e => e.Nome == nome);
        Assert.Equal((EsitoDelConfronto.Uguale, "XIBR5A"), (Riga("XIBRI 5A").Esito, Assert.Single(Riga("XIBRI 5A").Nomi)));
        // SOS5A va bene per SOSAK e per SOSIV: SIV5A è già SOSIV, e la transizione ESINO 8H parte da SOSAK.
        Assert.Equal(["SOS5A-ESI8H"], Riga("SOSAK 5A").Nomi);
        Assert.Equal(["SIV5A-SOV1E"], Riga("SOSIV 5A").Nomi);
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("ESINO 8H").Esito);
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("SOVAN 1E").Esito);
        Assert.DoesNotContain(esiti, e => e.Dettagli.Any(d => d.Contains("più procedure", StringComparison.Ordinal)));
        // Il campo 7 dice TAQ, l'AIP finisce a SOSAK.
        Assert.Contains("SOSA7Y: punto TAQ nel sector, SOSAK alla fine dell'AIP", Riga("SOSAK 7Y").Dettagli);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, Riga("VELAD 2A").Esito);
        Assert.Equal(EsitoDelConfronto.SoloNelSector, Riga("OST1D").Esito);
    }

    [Fact]
    public async Task LeStar_IPuntiInFila_LePosizioni_ELeRevisioni()
    {
        var esiti = await ConfrontaAsync("", string.Join("\r\n",
            [
                "LIRF;MAPS;LIRF CTR; ; ;1;",
                "N042.00.00.000;E012.00.00.000;",
                "N042.10.00.000;E012.10.00.000;",
                "",
                "LIRF;16L:16R;ELKA3A;;;;;1;",
                "ELKAP;ELKAP;",
                "ELKAP;ELKAP;3A;",
                "TAQ;TAQ;",
                "",
                "LIRF;16L:16R;ELKA3B;;;;;1;",
                "ELKAP;ELKAP;3B;",
                "NUOVO;NUOVO;",
                "",
                "LIRF;07:25;GILI3T;;;;;1;",
                "GILIO;GILIO;3T;",
                "TAQ;TAQ;",
                "",
            ]),
            P(ProcedureKind.Star, "ELKAP 3A", ["16L", "16R"], T("IF", "ELKAP"), T("TF", "TAQ")),
            P(ProcedureKind.Star, "ELKAP 3B", ["16L", "16R", "34L"], T("IF", "ELKAP"), T("TF", "XX401"), T("TF", "NUOVO")),
            P(ProcedureKind.Star, "GILIO 4T", ["07", "25"], T("IF", "GILIO"), T("TF", "TAQ")));

        DifferenzaDiProcedura Riga(string nome) => esiti.Single(e => e.Nome == nome);
        // ELKAP ripetuto di seguito nel sector è lo stesso punto; il MAPS non è una STAR.
        Assert.Equal(EsitoDelConfronto.Uguale, Riga("ELKAP 3A").Esito);
        Assert.DoesNotContain(esiti, e => e.Nome == "LIRF CTR");
        Assert.Equal(
        [
            "pista 34L: nel sector non c'è",
            "ELKA3B: punti nel sector ELKAP NUOVO, nell'AIP ELKAP XX401 NUOVO",
            "XX401: il sector non ha il punto",
            "NUOVO: nel sector a 1.00 NM dall'AIP",
        ], Riga("ELKAP 3B").Dettagli);
        Assert.Equal(EsitoDelConfronto.MancaNelSector, Riga("GILIO 4T").Esito);
        Assert.Contains("nel sector c'è GILI3T: stesso nome, altro numero (un'altra revisione?)", Riga("GILIO 4T").Dettagli);
        Assert.Contains("nell'AIP c'è GILIO 4T: stesso nome, altro numero (un'altra revisione?)", Riga("GILI3T").Dettagli);
    }

    [Theory]
    [InlineData("XIBR5A", "XIBRI 5A", 1)]
    [InlineData("BAGIX3R", "BAGIX 3R", 0)]
    [InlineData("IRK7A", "IRKED 7A", 2)]
    [InlineData("NOK1K", "NOBKE 1K", 12)]
    [InlineData("SIV5A", "SOSIV 5A", 12)]
    [InlineData("XIBR5B", "XIBRI 5A", null)]
    [InlineData("SO5A", "SOSAK 5A", null)]
    [InlineData("KIV5A", "SOSIV 5A", null)]
    public void LaSomiglianzaDeiNomi(string delSector, string dellAip, int? attesa) =>
        Assert.Equal(attesa, ConfrontoDelleProcedure.Somiglia(delSector, dellAip));
}
