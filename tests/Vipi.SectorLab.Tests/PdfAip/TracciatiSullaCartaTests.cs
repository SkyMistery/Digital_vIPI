using Aip;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta «Gli aeroporti nel Lab», S4: ridisegnare sulla carta posata — una parte nuova come aggiunta, dei tratti in coda a una
/// parte come correzione, con la carta per fonte; la parte sta sotto la sua carta e si disegna. Dati inventati.
/// </summary>
public sealed class TracciatiSullaCartaTests
{
    private static readonly Position Uno = new(40.88, 14.29);

    private static AipDocument Modello() => new()
    {
        State = "LI",
        Airac = "2611",
        Airspaces = [],
        Points = [new SignificantPoint { Section = "AD 2 LIXX", Designator = "UNO", Position = Uno }, new SignificantPoint { Section = "AD 2 LIXX", Designator = "DUE", Position = new(40.95, 14.40) }],
        Aerodromes =
        [
            new Aerodrome
            {
                Icao = "LIXX", Name = "PROVA", Arp = Uno,
                Runways = [new Runway { Designator = "24" }, new Runway { Designator = "06" }],
                Charts =
                [
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS Z RWY 24", "AD 2 LIXX 5-3", new DateOnly(2025, 12, 25)),
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART VOR RWY 06", "AD 2 LIXX 5-5", new DateOnly(2026, 3, 19)),
                ],
            },
        ],
        Procedures =
        [
            new Procedure
            {
                Aerodrome = "LIXX", Kind = ProcedureKind.Approach, Designator = "ILS Z RWY24", Runways = ["24"], Page = new AipPage("AD 2 LIXX 5-4"),
                Segment = "RNAV to ILS TRANSITION - APPROACH FROM BENTO",
                Legs = [new ProcedureLeg { PathTerminator = "IF", Fix = "DUE" }, new ProcedureLeg { PathTerminator = "TF", Fix = "UNO" }],
            },
        ],
    };

    private static PaginaDelloScalo Pagina(AipDocument modello) => PagineDegliScali.Di(AipSullaMappa.Di(modello, "2611", _ => null), modello, "LIXX")!;

    private static CartaDiAvvicinamento Carta(AipDocument modello, string titolo) => Pagina(modello).Avvicinamenti.Single(c => c.Titolo == titolo);

    private static readonly DateOnly Oggi = new(2026, 10, 30);

    [Fact]
    public void UnaParteNuova_StaSottoLaSuaCarta_ESiDisegnaCoiPuntiPresiSullaCarta()
    {
        var modello = Modello();
        var vor = Carta(modello, "VOR RWY 06");
        Assert.Empty(vor.Parti);

        var (parte, perche) = TracciatiSullaCarta.NuovaParte("LIXX", vor, " MISSED APPROACH ",
            [new PuntoDelTracciato(40.8, 14.2, null, "IF", Quota: "+2000"), new PuntoDelTracciato(Uno.Lat, Uno.Lon, "uno")]);
        Assert.Null(perche);
        Assert.Equal("VOR RWY 06", parte!.Designator);
        Assert.Equal(["06"], parte.Runways);
        Assert.Equal("MISSED APPROACH", parte.Segment);
        Assert.Equal(new AipPage("AD 2 LIXX 5-5", new DateOnly(2026, 3, 19)), parte.Page);
        // Il punto preso sulla carta ha le sue coordinate e «dalla carta»; il fix il suo nome, maiuscolo.
        Assert.Equal(TracciatiSullaCarta.DallaCarta, parte.Legs[0].Computed);
        Assert.Equal(new Position(40.8, 14.2), parte.Legs[0].Position);
        Assert.Equal("+2000", parte.Legs[0].Altitude);
        Assert.Equal("UNO", parte.Legs[1].Fix);
        Assert.Null(parte.Legs[1].Position);

        var fonte = TracciatiSullaCarta.Fonte(vor.Carta);
        Assert.Equal("carta AD 2 LIXX 5-5 (19/03/2026)", fonte);
        var (fatta, no) = CorrezioniDellAip.Aggiungi(modello, new AipCorrectionFile { State = "LI" }, CorrectionKind.Procedure, parte,
            TracciatiSullaCarta.PercheProposto, fonte, "2611", Oggi);
        Assert.Null(no);
        var correzione = Assert.Single(fatta!.Correzioni.Corrections);
        Assert.Equal((CorrectionKind.Procedure, CorrectionOp.Add, parte.Key), (correzione.Kind, correzione.Op, correzione.Key));

        // Nella pagina dello scalo la carta non è più solo immagine: ha la parte, con la sua chiave.
        var dopo = Carta(fatta.Modello, "VOR RWY 06");
        var ridisegnata = Assert.Single(dopo.Parti);
        Assert.Equal("MISSED APPROACH", ridisegnata.Nome);
        Assert.Equal(parte.Key, ridisegnata.Procedura.Chiave);
        Assert.Equal(2, ridisegnata.Procedura.Tratti);
        Assert.Equal(StatoDellaCarta.Letta, dopo.Stato);
        // E sulla mappa è una linea dal punto della carta al fix.
        var strato = AipSullaMappa.Di(fatta.Modello, "2611", _ => null);
        var linea = strato.Strato.Forme[ridisegnata.Procedura.Indice];
        Assert.Equal(2, Assert.Single(linea.Tratti).Count);
        Assert.Empty(linea.NomiNonRisolti);
    }

    [Fact]
    public void ITrattiInCoda_SiAggiungonoAllaParte_ERitirandoTornaComEraNelPdf()
    {
        var modello = Modello();
        var prima = modello.Procedures![0];
        var (lunga, perche) = TracciatiSullaCarta.InCoda(prima,
            [new PuntoDelTracciato(40.7, 14.1, null, "RF", Centro: "due", Verso: "R"), new PuntoDelTracciato(40.6, 14.0, null, "DF", Velocita: "-210")]);
        Assert.Null(perche);
        Assert.Equal(4, lunga!.Legs.Count);
        Assert.Equal(prima.Legs, lunga.Legs.Take(2));
        Assert.Equal(("RF", "DUE", "R"), (lunga.Legs[2].PathTerminator, lunga.Legs[2].ArcCenter, lunga.Legs[2].Turn));
        Assert.Equal("-210", lunga.Legs[3].Speed);

        var (fatta, no) = CorrezioniDellAip.Correggi(modello, new AipCorrectionFile { State = "LI" }, CorrectionKind.Procedure, prima.Key, lunga,
            TracciatiSullaCarta.PercheProposto, "carta AD 2 LIXX 5-3 (25/12/2025)", "2611", Oggi);
        Assert.Null(no);
        var correzione = Assert.Single(fatta!.Correzioni.Corrections);
        Assert.Equal(CorrectionOp.Change, correzione.Op);
        Assert.Equal(prima, CorrezioniDellAip.Leggi(correzione.Before, CorrectionKind.Procedure));
        Assert.Equal(4, fatta.Modello.Procedures![0].Legs.Count);

        var (ritirata, noRitira) = CorrezioniDellAip.Ritira(fatta.Modello, fatta.Correzioni, correzione.Id);
        Assert.Null(noRitira);
        Assert.Equal(prima, Assert.Single(ritirata!.Modello.Procedures!));
    }

    [Fact]
    public void UnTrattoIncompletoSiDiceCosaManca()
    {
        var vor = Carta(Modello(), "VOR RWY 06");
        PuntoDelTracciato[] due = [new(40.8, 14.2, null, "IF"), new(40.9, 14.3, null)];

        Assert.Contains("nome della parte", TracciatiSullaCarta.NuovaParte("LIXX", vor, "  ", due).Perche, StringComparison.Ordinal);
        Assert.Contains("almeno due punti", TracciatiSullaCarta.NuovaParte("LIXX", vor, "MISSED APPROACH", [due[0]]).Perche, StringComparison.Ordinal);
        Assert.Contains("centro", TracciatiSullaCarta.NuovaParte("LIXX", vor, "MISSED APPROACH", [due[0], due[1] with { Tipo = "RF", Centro = "UNO" }]).Perche,
            StringComparison.Ordinal);
        Assert.Contains("«HM»", TracciatiSullaCarta.NuovaParte("LIXX", vor, "MISSED APPROACH", [due[0], due[1] with { Tipo = "HM" }]).Perche, StringComparison.Ordinal);
        Assert.Contains("Nessun punto", TracciatiSullaCarta.InCoda(Modello().Procedures![0], []).Perche, StringComparison.Ordinal);
    }
}
