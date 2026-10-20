using Aip;
using Aip.Aurora;
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

    private static ProcedureLeg T(string pt, string? fix = null) => new() { PathTerminator = pt, Fix = fix, Navigation = "RNAV1" };

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
            Points =
            [
                .. Punti.Select(p => new SignificantPoint { Section = "ENR 4.4", Designator = p.Nome, Position = new Position(p.Nome == "NUOVO" ? p.Lat + 1 / 60.0 : p.Lat, p.Lon) }),
                // Un punto RNAV di scalo che il sector non ha (si porta in APT.fix, nella sezione //LIRF).
                new SignificantPoint { Section = "AD 2 LIRF", Designator = "XX601", Position = new Position(41.75, 12.25) },
            ],
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

    private string Righe(string relativo) => string.Join("\n", _lab.RigheDiAdesso("SectorFiles/Include/IT/" + relativo));

    /// <summary>Le ultime <paramref name="quante"/> righe non vuote del file.</summary>
    private string[] Coda(string relativo, int quante) =>
        [.. _lab.RigheDiAdesso("SectorFiles/Include/IT/" + relativo).Where(r => r.Length > 0).TakeLast(quante)];

    /// <summary>Le prime <paramref name="quante"/> righe non vuote del file.</summary>
    private string[] Testa(string relativo, int quante) =>
        [.. _lab.RigheDiAdesso("SectorFiles/Include/IT/" + relativo).Where(r => r.Length > 0).Take(quante)];

    // Slice 7e: «Porta nel sector» una STAR che manca (col punto di scalo che manca, prima, in APT.fix) e una SID col suo
    // tracciato; il confronto rifatto le dà uguali; «Annulla» rimette tutto com'era.
    [Fact]
    public async Task PortaNelSector_UnaStarEUnaSid_CoiPuntiCheMancano()
    {
        var esiti = await ConfrontaAsync("LIRF;25;XIBR5A; ; ;0;XIBRI;1;\r\n", "LIRF;16L:16R;ELKA3A;;;;;1;\r\nELKAP;ELKAP;3A;\r\nTAQ;TAQ;\r\n",
            P(ProcedureKind.Sid, "XIBRI 5A", ["25"], T("CA"), T("TF", "ELKAP"), T("TF", "XIBRI")),
            P(ProcedureKind.Sid, "TAQUI 6B", ["25"], T("CA"), T("CF", "XX601") with { Speed = "-210" }, T("TF", "TAQ") with { Altitude = "+FL110" }),
            P(ProcedureKind.Star, "ELKAP 3A", ["16L", "16R"], T("IF", "ELKAP"), T("TF", "TAQ")),
            P(ProcedureKind.Star, "GILIO 4B", ["16L", "16R"], T("IF", "GILIO") with { Altitude = "+FL190" }, T("TF", "XX601"), T("TF", "TAQ")));
        var aptPrima = Righe("NAVAIDS/APT.fix");
        var strPrima = Righe("lirf.str");

        Assert.True(_lab.PortaLaProcedura(esiti.Single(e => e.Nome == "GILIO 4B")), _lab.Rifiuto);

        // Il punto di scalo nella sezione //LIRF, la STAR in fondo allo .str col nome a quattro lettere del file.
        var apt = _lab.RigheDiAdesso("SectorFiles/Include/IT/NAVAIDS/APT.fix").ToList();
        var lirf = apt.FindIndex(r => r.StartsWith("//LIRF", StringComparison.Ordinal));
        var xx = apt.FindIndex(r => r.StartsWith("XX601;", StringComparison.Ordinal));
        Assert.True(xx > lirf && lirf >= 0, $"XX601 alla riga {xx}, //LIRF alla {lirf}");
        Assert.Equal("XX601;N041.45.00.000;E012.15.00.000;3;", apt[xx]);
        Assert.Equal(
        [
            "//@\"GILI4B\" nav=RNAV1 note=\"AIP 2611\"", "//@START", "LIRF;16L:16R;GILI4B;;;;;1;",
            "//@@\"GILIO\" alt=+FL190", "GILIO;GILIO;4B;", "XX601;XX601;", "TAQ;TAQ;", "//@END \"GILI4B\"",
        ], Coda("lirf.str", 8));
        var gilio = _lab.UltimoConfrontoDelleProcedure!.Value.Esiti.Single(e => e.Nome == "GILIO 4B");
        Assert.True(gilio.Esito == EsitoDelConfronto.Uguale, string.Join(" | ", gilio.Dettagli));

        // La SID col tracciato; il punto c'è già: APT.fix non cambia più.
        var dopoLaStar = Righe("NAVAIDS/APT.fix");
        Assert.True(_lab.PortaLaProcedura(_lab.UltimoConfrontoDelleProcedure!.Value.Esiti.Single(e => e.Nome == "TAQUI 6B")), _lab.Rifiuto);
        Assert.Equal(dopoLaStar, Righe("NAVAIDS/APT.fix"));
        Assert.Equal(
        [
            "//@\"TAQU6B\" nav=RNAV1 note=\"AIP 2611\"", "//@START", "LIRF;25;TAQU6B; ; ;0;TAQ;1;",
            "//@@\"XX601\" spd=-210", "XX601;XX601;", "//@@\"TAQ\" alt=+FL110", "TAQ;TAQ;", "//@END \"TAQU6B\"",
        ], Coda("lirf.sid", 8));
        Assert.Equal(EsitoDelConfronto.Uguale, _lab.UltimoConfrontoDelleProcedure!.Value.Esiti.Single(e => e.Nome == "TAQUI 6B").Esito);

        // Annulla due volte: com'era.
        _lab.Annulla();
        _lab.Annulla();
        Assert.Equal(aptPrima, Righe("NAVAIDS/APT.fix"));
        Assert.Equal(strPrima, Righe("lirf.str"));
    }

    [Fact]
    public async Task Sostituisci_LaStarELeSidColLaTransizione_ELeBloccateNo()
    {
        var esiti = await ConfrontaAsync(string.Join("\r\n",
            [
                "LIRF;25;SOS5A-ESI8H; ; ;0;ESINO;1;",
                "LIRF;25;SOS5A-SOV1E; ; ;0;SOVAN;1;",
                "",
            ]), string.Join("\r\n",
            [
                "LIRF;16L:16R;ELKA3B;;;;;1;", "ELKAP;ELKAP;3B;", "NUOVO;NUOVO;", "",
                "//@\"ELKA3C\" locked=si", "LIRF;16L:16R;ELKA3C;;;;;1;", "ELKAP;ELKAP;3C;", "",
            ]),
            P(ProcedureKind.Sid, "SOSAK 5A", ["25"], T("CA"), T("TF", "ELKAP"), T("TF", "SOSAK") with { Altitude = "+3000" }),
            P(ProcedureKind.Sid, "ESINO 8H", [], T("IF", "SOSAK"), T("TF", "ESINO")),
            P(ProcedureKind.Sid, "SOVAN 1E", [], T("IF", "SOSAK"), T("TF", "SOVAN")),
            P(ProcedureKind.Star, "ELKAP 3B", ["16L", "16R"], T("IF", "ELKAP"), T("TF", "TAQ"), T("TF", "NUOVO")),
            P(ProcedureKind.Star, "ELKAP 3C", ["16L", "16R"], T("IF", "ELKAP"), T("TF", "TAQ")));

        // La STAR: il corpo riscritto, la testata com'era.
        Assert.True(_lab.SostituisciLaProcedura(esiti.Single(e => e.Nome == "ELKAP 3B")), _lab.Rifiuto);
        Assert.Equal(
        [
            "//@\"ELKA3B\" nav=RNAV1 note=\"AIP 2611\"", "//@START", "LIRF;16L:16R;ELKA3B;;;;;1;",
            "ELKAP;ELKAP;3B;", "TAQ;TAQ;", "NUOVO;NUOVO;", "//@END \"ELKA3B\"",
        ], Testa("lirf.str", 7));

        // La SID: le due voci, ognuna col tracciato della SID e della sua transizione.
        Assert.True(_lab.SostituisciLaProcedura(_lab.UltimoConfrontoDelleProcedure!.Value.Esiti.Single(e => e.Nome == "SOSAK 5A")), _lab.Rifiuto);
        var sid = Righe("lirf.sid");
        Assert.Contains(string.Join("\n",
        [
            "LIRF;25;SOS5A-ESI8H; ; ;0;ESINO;1;", "ELKAP;ELKAP;", "//@@\"SOSAK\" alt=+3000", "SOSAK;SOSAK;", "ESINO;ESINO;", "//@END \"SOS5A-ESI8H\"",
        ]), sid, StringComparison.Ordinal);
        Assert.Contains("SOSAK;SOSAK;\nSOVAN;SOVAN;\n//@END \"SOS5A-SOV1E\"", sid, StringComparison.Ordinal);

        // La STAR bloccata non si tocca.
        var riga = _lab.UltimoConfrontoDelleProcedure!.Value.Esiti.Single(e => e.Nome == "ELKAP 3C");
        Assert.Equal(EsitoDelConfronto.FormaDiversa, riga.Esito);
        Assert.False(_lab.SostituisciLaProcedura(riga));
        Assert.Contains("locked=si", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // D2: le SID uguali che nel sector non hanno tracciato lo ricevono tutte insieme; dopo restano uguali, col tracciato.
    [Fact]
    public async Task ScriviITracciati_DelleSidUguali()
    {
        var esiti = await ConfrontaAsync("LIRF;25;XIBR5A; ; ;0;XIBRI;1;\r\nLIRF;25;TAQU6B; ; ;0;TAQ;1;\r\n", "",
            P(ProcedureKind.Sid, "XIBRI 5A", ["25"], T("CA"), T("TF", "ELKAP"), T("TF", "XIBRI")),
            P(ProcedureKind.Sid, "TAQUI 6B", ["25"], T("CA"), T("TF", "TAQ")));
        Assert.All(esiti, e => Assert.Equal((EsitoDelConfronto.Uguale, 1), (e.Esito, e.VociSenzaTracciato)));

        Assert.True(_lab.ScriviITracciatiDelleSid(), _lab.Rifiuto);

        Assert.Equal(
        [
            "//@\"XIBR5A\" nav=RNAV1 note=\"AIP 2611\"", "//@START", "LIRF;25;XIBR5A; ; ;0;XIBRI;1;", "ELKAP;ELKAP;", "XIBRI;XIBRI;", "//@END \"XIBR5A\"",
            "//@\"TAQU6B\" nav=RNAV1 note=\"AIP 2611\"", "//@START", "LIRF;25;TAQU6B; ; ;0;TAQ;1;", "TAQ;TAQ;", "//@END \"TAQU6B\"",
        ], Testa("lirf.sid", 11));
        Assert.All(_lab.UltimoConfrontoDelleProcedure!.Value.Esiti, e => Assert.Equal((EsitoDelConfronto.Uguale, 0), (e.Esito, e.VociSenzaTracciato)));
        // Un gesto solo: un «Annulla» toglie tutti e due i tracciati.
        _lab.Annulla();
        Assert.Equal(["LIRF;25;XIBR5A; ; ;0;XIBRI;1;", "LIRF;25;TAQU6B; ; ;0;TAQ;1;"], Testa("lirf.sid", 2));
    }

    [Fact]
    public async Task UnaProceduraInProsa_EUnPuntoCheLAipNonHa_NonSiPortano()
    {
        var esiti = await ConfrontaAsync("", "LIRF;16L:16R;ELKA3A;;;;;1;\r\nELKAP;ELKAP;3A;\r\nTAQ;TAQ;\r\n",
            P(ProcedureKind.Star, "PROSA 1A", ["16L"]) with { Description = "Proceed via …" },
            P(ProcedureKind.Star, "STORTO 1A", ["16L"], T("IF", "ELKAP"), T("TF", "XX407")));

        Assert.False(_lab.PortaLaProcedura(esiti.Single(e => e.Nome == "PROSA 1A")));
        Assert.Contains("la prosa non si è letta", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.PortaLaProcedura(esiti.Single(e => e.Nome == "STORTO 1A")));
        Assert.Contains("XX407", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.DoesNotContain("STOR1A", Righe("lirf.str"), StringComparison.Ordinal);
    }

    // Slice 7b: una STAR dalla prosa contro una del sector che disegna la virata con righe di coordinate (il parser la legge
    // come HoldingStrRecord): i punti con nome in fila, e il punto calcolato vicino al tracciato. Uguale se ci passa vicino,
    // diversa se no; «Porta» scrive il punto calcolato come coordinate, e non scrive una voce che c'è già con le coordinate.
    [Fact]
    public async Task UnaStarDallaProsa_ControUnaDelSectorConLeCoordinate()
    {
        var spigolo = new Position(42.25, 11.40);
        ProcedureLeg[] tratti =
        [
            T("IF", "GILIO"),
            new ProcedureLeg { PathTerminator = "CF", Computed = "RDL 300/20 NM TAQ", Position = spigolo },
            T("CF", "TAQ"),
        ];
        var vicino = AuroraText.Coordinates(new Position(spigolo.Lat + 0.5 / 60, spigolo.Lon));   // mezzo miglio a nord
        var lontano = AuroraText.Coordinates(new Position(spigolo.Lat + 3 / 60.0, spigolo.Lon));  // tre miglia
        var esiti = await ConfrontaAsync("", string.Join("\r\n",
            [
                "LIRF;16L:16R;GILI4P; ; ;", "GILIO;GILIO;4P;", vicino + ";", "TAQ;TAQ;", "",
                "LIRF;16L:16R;GILI5P; ; ;", "GILIO;GILIO;5P;", lontano + ";", "TAQ;TAQ;", "",
            ]),
            P(ProcedureKind.Star, "GILIO 4P", ["16L", "16R"], tratti) with { LegsFromDescription = true },
            P(ProcedureKind.Star, "GILIO 5P", ["16L", "16R"], tratti) with { LegsFromDescription = true },
            P(ProcedureKind.Star, "GILIO 6P", ["16L", "16R"], tratti) with { LegsFromDescription = true });

        var quattro = esiti.Single(e => e.Nome == "GILIO 4P");
        Assert.True(quattro.Esito == EsitoDelConfronto.Uguale, string.Join(" | ", quattro.Dettagli));
        var cinque = esiti.Single(e => e.Nome == "GILIO 5P");
        Assert.Equal(EsitoDelConfronto.FormaDiversa, cinque.Esito);
        Assert.Equal("GILI5P: RDL 300/20 NM TAQ: il tracciato del sector passa a 3.0 NM", Assert.Single(cinque.Dettagli));

        // «Porta» la 6P: il punto calcolato come coordinate, fra i due con nome.
        Assert.True(_lab.PortaLaProcedura(esiti.Single(e => e.Nome == "GILIO 6P")), _lab.Rifiuto);
        Assert.Equal(["LIRF;16L:16R;GILI6P;;;;;1;", "GILIO;GILIO;6P;", AuroraText.Coordinates(spigolo) + ";", "TAQ;TAQ;", "//@END \"GILI6P\""], Coda("lirf.str", 5));
        Assert.Equal(EsitoDelConfronto.Uguale, _lab.UltimoConfrontoDelleProcedure!.Value.Esiti.Single(e => e.Nome == "GILIO 6P").Esito);
    }

    // Prima della 7b il nome di una STAR con le coordinate non si vedeva: «Porta» ne avrebbe scritta una seconda.
    [Fact]
    public async Task UnaVoceConLeCoordinate_ConLoStessoNome_NonSiScriveDueVolte()
    {
        var esiti = await ConfrontaAsync("", "LIRF;07;GILIO4P; ; ;\r\nGILIO;GILIO;4P;\r\nN042.15.00.000;E011.24.00.000;\r\nTAQ;TAQ;\r\n",
            P(ProcedureKind.Star, "GILIO 4P", ["16L", "16R"], T("IF", "GILIO"), T("TF", "TAQ")));

        // Le piste non hanno niente in comune: la voce del sector non è la sua, e la procedura manca.
        var riga = esiti.Single(e => e.Nome == "GILIO 4P");
        Assert.Equal(EsitoDelConfronto.MancaNelSector, riga.Esito);
        Assert.False(_lab.PortaLaProcedura(riga));
        // Il nome che «Porta» darebbe (cinque lettere: il file non ne usa altre) è quello della voce con le coordinate.
        Assert.Contains("GILIO4P: c'è già", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // Slice 7f (P10): la mappa di gruppo in testa al .sid, composta dalle SID col tracciato; il confronto non la prende per
    // una SID; «Annulla» la toglie.
    [Fact]
    public async Task LaMappaDelleSid_InTestaAlFile_CompostaDalleSidColTracciato()
    {
        await ConfrontaAsync("LIRF;25;XIBR5A; ; ;0;XIBRI;1;\r\nLIRF;25;TAQU6B; ; ;0;TAQ;1;\r\n", "",
            P(ProcedureKind.Sid, "XIBRI 5A", ["25"], T("CA"), T("TF", "ELKAP"), T("TF", "XIBRI")),
            P(ProcedureKind.Sid, "TAQUI 6B", ["25"], T("CA"), T("TF", "TAQ")));
        // Senza tracciato non c'è niente da raccogliere.
        Assert.DoesNotContain(_lab.ScaliPerLaMappaDelleSid(), s => s.Scalo == "LIRF");
        Assert.True(_lab.ScriviITracciatiDelleSid(), _lab.Rifiuto);
        var prima = Righe("lirf.sid");
        Assert.Equal(("LIRF", 2), _lab.ScaliPerLaMappaDelleSid().Select(s => (s.Scalo, s.Sid)).Single(s => s.Scalo == "LIRF"));

        Assert.True(_lab.CreaLaMappaDelleSid("LIRF"), _lab.Rifiuto);

        Assert.Equal(
        [
            "//@\"SID RNAV(ALL)\" compose=XIBR5A,TAQU6B", "LIRF;MAPS;SID RNAV(ALL);;;;;1;", "ELKAP;ELKAP;5A;", "XIBRI;XIBRI;", "TAQ;TAQ;6B;",
        ], Testa("lirf.sid", 5));
        Assert.Equal("", _lab.RigheDiAdesso("SectorFiles/Include/IT/lirf.sid")[4]);   // il tratto nuovo, dopo una riga vuota
        Assert.DoesNotContain(_lab.ScaliPerLaMappaDelleSid(), s => s.Scalo == "LIRF");
        Assert.True(await _lab.ConfrontaLeProcedureAsync(), _lab.Rifiuto);
        Assert.DoesNotContain(_lab.UltimoConfrontoDelleProcedure!.Value.Esiti, e => e.Nome.Contains("ALL", StringComparison.Ordinal));
        Assert.False(_lab.CreaLaMappaDelleSid("LIRF"));

        _lab.Annulla();
        Assert.Equal(prima, Righe("lirf.sid"));
    }

    // Prima della 7f una procedura riscritta nel testo («Sostituisci») lasciava indietro la mappa composta che la elenca:
    // ora la mappa si riallinea nello stesso gesto. Una mappa che non la elenca non si tocca.
    [Fact]
    public async Task Sostituisci_RiallineaLaMappaCompostaCheLaElenca()
    {
        var esiti = await ConfrontaAsync(string.Join("\r\n",
            [
                "//@\"SID RNAV(ALL)\" compose=XIBR5A", "LIRF;MAPS;SID RNAV(ALL);;;;;1;", "ELKAP;ELKAP;5A;", "",
                "//@\"ALTRE\" compose=TAQU6B", "LIRF;MAPS;ALTRE;;;;;1;", "SOVAN;SOVAN;6B;", "",
                "LIRF;25;XIBR5A; ; ;0;ELKAP;1;", "ELKAP;ELKAP;",
                "LIRF;25;TAQU6B; ; ;0;TAQ;1;", "TAQ;TAQ;",
                "",
            ]), "",
            P(ProcedureKind.Sid, "XIBRI 5A", ["25"], T("CA"), T("TF", "ELKAP"), T("TF", "XIBRI")),
            P(ProcedureKind.Sid, "TAQUI 6B", ["25"], T("CA"), T("TF", "TAQ")));
        var riga = esiti.Single(e => e.Nome == "XIBRI 5A");
        Assert.Equal(EsitoDelConfronto.FormaDiversa, riga.Esito);

        Assert.True(_lab.SostituisciLaProcedura(riga), _lab.Rifiuto);

        Assert.Equal(["//@\"SID RNAV(ALL)\" compose=XIBR5A", "LIRF;MAPS;SID RNAV(ALL);;;;;1;", "ELKAP;ELKAP;5A;", "XIBRI;XIBRI;"], Testa("lirf.sid", 4));
        // «ALTRE» elenca TAQU6B, che non è cambiata: resta com'era, anche se non è allineata.
        Assert.Contains("SOVAN;SOVAN;6B;", Righe("lirf.sid"), StringComparison.Ordinal);
        _lab.Annulla();
        Assert.Equal(["//@\"SID RNAV(ALL)\" compose=XIBR5A", "LIRF;MAPS;SID RNAV(ALL);;;;;1;", "ELKAP;ELKAP;5A;", "//@\"ALTRE\" compose=TAQU6B"], Testa("lirf.sid", 4));
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
