using System.Globalization;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// Gli avvisi degli <c>.artcc</c> (lotto «Subito» slice 14a, «file per file» A4): i cerchi dei gate, le AOCC, le
/// etichette dei fix di confine.
/// </summary>
/// <remarks>
/// <para>Misure sul fork del 6 ottobre 2026. <c>FRA-gates.artcc</c>: 85 cerchi di mezzo miglio (37-38 punti, uno ogni
/// 10°); 84 chiudono a meno di 20 metri, e a <c>//X07-X08</c> manca il punto che lo chiude (0,085 NM, un passo); tutti
/// hanno il centro a meno di 0,3 NM dal confine. <c>FRA.artcc</c>: cinque AOCC, ognuna un gambo che parte dal confine e
/// una stanghetta di traverso in fondo, toccata a metà — le stanghette misurano da 14,93 a 15,08 NM; 104 etichette
/// <c>L;</c>, tutte sul fix che ha il loro nome.</para>
/// <para>🔴 Non si controlla la distanza fra due gate (di solito 10 NM, non sempre) né l'etichetta di un gate fuori dal
/// centro: quelle eccezioni vengono dai documenti (committente, 24 settembre).</para>
/// <para>Si legge dalle righe, non dal modello: un gruppo è un record solo coi suoi tratti, e l'avviso va sulla riga
/// del tratto, col nome che gli dà il commento sopra (<c>//X07-X08</c>).</para>
/// </remarks>
public static class ControlloDegliAcc
{
    // Un cerchio di gate: almeno 12 punti a distanza quasi uguale dal loro centro, entro 2 NM.
    private const int PuntiMinimiDiUnCerchio = 12;
    private const double RaggioMassimoNm = 2;

    // Il centro di un gate sta sul confine: sul fork lo scarto massimo è 0,26 NM.
    private const double DalConfineNm = 0.3;

    // La stanghetta di un'AOCC è di 15 NM (committente); le cinque del fork scartano al più 0,08.
    private const double StanghettaNm = 15;
    private const double TolleranzaDellaStanghettaNm = 0.25;

    // Sotto un decimo di miglio l'etichetta è sul suo fix (come per NomeRipetuto).
    private const double DalFixNm = 0.1;

    private const double MetriPerNm = 1852;

    private sealed record Tratto(string Relativo, int Riga, string Gruppo, string Commento, List<Coordinate> Punti);

    /// <summary>
    /// I problemi degli <c>.artcc</c>. <paramref name="file"/>: il percorso da mostrare e le righe di ognuno;
    /// <paramref name="punto"/>: la posizione di un fix, VOR o NDB per nome, o null.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> file,
                                                    Func<string, Coordinate?> punto)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(punto);

        var tratti = new List<Tratto>();
        var problemi = new List<ProblemaDelSector>();
        foreach (var (relativo, righe) in file)
        {
            Tratto? aperto = null;
            string commento = string.Empty;
            for (int i = 0; i < righe.Count; i++)
            {
                string riga = righe[i].Trim();
                if (riga.Length == 0)
                {
                    aperto = null;
                    continue;
                }

                if (riga.StartsWith("//", StringComparison.Ordinal))
                {
                    commento = riga.TrimStart('/', ' ').Trim();
                    aperto = null;
                    continue;
                }

                string[] campi = riga.Split(';');
                string tipo = campi[0].Trim().ToUpperInvariant();
                if (campi.Length < 4 || tipo is not ("T" or "L"))
                {
                    aperto = null;
                    continue;
                }

                string nome = campi[1].Trim();
                var dove = Leggi(campi[2], campi[3], punto);
                if (tipo == "L")
                {
                    aperto = null;
                    // Solo le etichette per coordinate: una scritta per nome (A8) segue il fix da sé.
                    if (dove is { } qui && Coordinata(campi[2]) && punto(nome) is { } delFix
                        && Validatore.Metri(qui, delFix) / MetriPerNm is var lontana && lontana > DalFixNm)
                    {
                        campi[2] = CoordinateConverter.LatitudeToDottedDms(delFix.LatitudeDeg);
                        campi[3] = CoordinateConverter.LongitudeToDottedDms(delFix.LongitudeDeg);
                        problemi.Add(new(Regola.EtichettaLontanaDalFix, relativo, i + 1, righe[i],
                            $"l'etichetta «{nome}» è a {Nm(lontana)} NM dal punto che ha il suo nome: il punto è stato spostato, o l'etichetta",
                            string.Join(';', campi)));
                    }

                    continue;
                }

                if (string.Equals(nome, "DUMMY", StringComparison.OrdinalIgnoreCase))
                {
                    aperto = null;
                    continue;
                }

                if (aperto is null || !string.Equals(aperto.Gruppo, nome, StringComparison.Ordinal))
                {
                    aperto = new Tratto(relativo, i + 1, nome, commento, []);
                    tratti.Add(aperto);
                }

                if (dove is { } p)
                    aperto.Punti.Add(p);
            }
        }

        var cerchi = tratti.Select(t => (Tratto: t, Cerchio: Cerchio(t.Punti))).Where(c => c.Cerchio is not null).ToList();
        var delCerchio = cerchi.Select(c => c.Tratto).ToHashSet();
        // Il confine: ogni tratto che non è un cerchio. Senza nomi scritti nel codice (FRA BDRY, LIMITROFI…).
        var confine = tratti.Where(t => !delCerchio.Contains(t) && t.Punti.Count >= 2).ToList();

        foreach (var (tratto, cerchio) in cerchi)
        {
            var (centro, passo) = cerchio!.Value;
            double apertura = Validatore.Metri(tratto.Punti[0], tratto.Punti[^1]) / MetriPerNm;
            if (apertura > passo / 2)
            {
                problemi.Add(new(Regola.CerchioNonChiuso, tratto.Relativo, tratto.Riga, string.Empty,
                    $"il cerchio{Detto(tratto)} non è chiuso: fra l'ultimo punto e il primo mancano {Nm(apertura)} NM (un passo del cerchio è {Nm(passo)})"));
            }

            if (confine.Count > 0 && confine.Min(t => DalTratto(centro, t.Punti)) is var fuori && fuori > DalConfineNm)
            {
                problemi.Add(new(Regola.CentroFuoriDalConfine, tratto.Relativo, tratto.Riga, string.Empty,
                    $"il cerchio{Detto(tratto)} ha il centro a {Nm(fuori)} NM dal confine più vicino: un gate sta sul confine"));
            }
        }

        // Le AOCC: nei gruppi che si chiamano AOCC…, la stanghetta è il tratto che un altro tocca con un suo estremo.
        foreach (var gruppo in confine.Where(t => t.Gruppo.StartsWith("AOCC", StringComparison.OrdinalIgnoreCase)).GroupBy(t => (t.Relativo, t.Gruppo)))
        {
            var suoi = gruppo.ToList();
            foreach (var stanghetta in suoi)
            {
                bool toccata = suoi.Any(g => !ReferenceEquals(g, stanghetta)
                    && Math.Min(DalTratto(g.Punti[0], stanghetta.Punti), DalTratto(g.Punti[^1], stanghetta.Punti)) < DalConfineNm
                    // Il gambo tocca la stanghetta lontano dai suoi estremi: due tratti che si toccano in punta non sono una T.
                    && Math.Min(Validatore.Metri(Vicino(g, stanghetta), stanghetta.Punti[0]), Validatore.Metri(Vicino(g, stanghetta), stanghetta.Punti[^1])) / MetriPerNm > 1);
                double lunga = Lunghezza(stanghetta.Punti);
                if (toccata && Math.Abs(lunga - StanghettaNm) > TolleranzaDellaStanghettaNm)
                {
                    problemi.Add(new(Regola.StanghettaDellAocc, stanghetta.Relativo, stanghetta.Riga, string.Empty,
                        $"la stanghetta di {stanghetta.Gruppo}{Detto(stanghetta)} è lunga {Nm(lunga)} NM: deve essere di {Nm(StanghettaNm)}"));
                }
            }
        }

        return problemi.Select(p => p.Testo.Length > 0 ? p : p with { Testo = TestoDi(file, p.File, p.Riga) });
    }

    private static string TestoDi(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> file, string relativo, int riga)
        => file.First(f => f.Relativo == relativo).Righe[riga - 1];

    // L'estremo del gambo che sta sulla stanghetta.
    private static Coordinate Vicino(Tratto gambo, Tratto stanghetta)
        => DalTratto(gambo.Punti[0], stanghetta.Punti) < DalTratto(gambo.Punti[^1], stanghetta.Punti) ? gambo.Punti[0] : gambo.Punti[^1];

    private static string Detto(Tratto tratto) => tratto.Commento.Length > 0 ? $" «{tratto.Commento}»" : string.Empty;

    private static string Nm(double nm) => nm.ToString(nm < 1 ? "0.###" : "0.##", CultureInfo.InvariantCulture);

    private static bool Coordinata(string campo)
        => campo.Trim() is { Length: > 1 } t && "NSEWnsew".Contains(t[0], StringComparison.Ordinal) && char.IsAsciiDigit(t[1]);

    private static Coordinate? Leggi(string lat, string lon, Func<string, Coordinate?> punto)
    {
        if (!Coordinata(lat))
            return punto(lat.Trim());
        try
        {
            return CoordinateConverter.ParsePair(lat.Trim(), lon.Trim());
        }
        catch (CoordinateParseException)
        {
            return null;
        }
    }

    /// <summary>
    /// Il centro e il passo (la distanza media fra due punti di seguito, in NM) se il tratto è un cerchio: tanti punti
    /// alla stessa distanza dal loro centro. Il punto che chiude, se c'è, non pesa sul centro.
    /// </summary>
    private static (Coordinate Centro, double Passo)? Cerchio(List<Coordinate> punti)
    {
        if (punti.Count < PuntiMinimiDiUnCerchio)
            return null;
        var giro = Validatore.Metri(punti[0], punti[^1]) < 1 ? punti.GetRange(0, punti.Count - 1) : punti;
        var centro = new Coordinate(giro.Average(p => p.LatitudeDeg), giro.Average(p => p.LongitudeDeg));
        var raggi = giro.Select(p => Validatore.Metri(centro, p) / MetriPerNm).ToList();
        double medio = raggi.Average();
        if (medio > RaggioMassimoNm || raggi.Max() - raggi.Min() > medio * 0.2)
            return null;
        double passo = Enumerable.Range(0, giro.Count - 1).Average(i => Validatore.Metri(giro[i], giro[i + 1])) / MetriPerNm;
        return (centro, passo);
    }

    private static double Lunghezza(List<Coordinate> punti)
        => Enumerable.Range(0, punti.Count - 1).Sum(i => Validatore.Metri(punti[i], punti[i + 1])) / MetriPerNm;

    private static double DalTratto(Coordinate p, List<Coordinate> punti)
        => Enumerable.Range(0, punti.Count - 1).Min(i => ControlloDellaTerra.MetriDalSegmento(p, punti[i], punti[i + 1])) / MetriPerNm;
}
