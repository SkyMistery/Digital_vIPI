using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.PdfAip;
using V = Vipi.SectorLab.Core.PdfAip.CurveDelleProcedure.Vertice;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Le virate delle procedure lette dalla prosa disegnate come curve (carta «Le curve delle procedure», R1, R2; D1, D2).
/// Geometria inventata attorno a 44° N 10° E.
/// </summary>
public sealed class CurveDelleProcedureTests
{
    private static readonly Position O = new(44.0, 10.0);

    private static Position Da(Position p, double rotta, double nm) => Sphere.Destination(p, rotta, nm);

    private static double Distanza(Position a, Position b) => Sphere.DistanceNm(a, b);

    [Fact]
    public void IlRaggio_A210NodiE25Gradi_EUnMiglioEMezzoScarso()
    {
        Assert.Equal(1.38, CurveDelleProcedure.Raggio(210), 2);
        Assert.Equal(0.61, CurveDelleProcedure.Raggio(140), 2);
    }

    [Fact]
    public void LaVelocita_DallaColonna_DallaNota_OQuellaDellaProcedura()
    {
        var conColonna = new ProcedureLeg { PathTerminator = "CF", Fix = "PRT", Speed = "-185" };
        var conNota = new ProcedureLeg { PathTerminator = "CF", Fix = "PRT", Notes = "IAS MAX 200 KT during turn" };
        var senza = new ProcedureLeg { PathTerminator = "CF", Fix = "PRT", Notes = "(4)" };
        Assert.Equal(185, CurveDelleProcedure.Velocita(ProcedureKind.Sid, conColonna, conNota));
        Assert.Equal(200, CurveDelleProcedure.Velocita(ProcedureKind.Sid, senza, conNota));
        Assert.Equal(210, CurveDelleProcedure.Velocita(ProcedureKind.Sid, senza, null));
        Assert.Equal(220, CurveDelleProcedure.Velocita(ProcedureKind.Star, senza, null));
    }

    [Fact]
    public void UnPuntoCalcolato_AdAngoloRetto_SiArrotondaConLArcoTangente()
    {
        // Verso nord fino al punto, poi verso est: una virata a destra di 90°.
        var b = Da(O, 0, 10);
        var c = Da(b, 90, 10);
        var tappe = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, false, false, null, 1.4), new V(c, true, false, null, 0)]);

        // Il punto calcolato non c'è più: al suo posto l'arco, dall'inizio (1,4 NM prima) alla fine (1,4 NM dopo).
        Assert.DoesNotContain(tappe, t => t.Vertice == 1);
        var arco = tappe.Where(t => t.Vertice is null).Select(t => t.Punto).ToList();
        Assert.Equal(19, arco.Count);   // 90° a 5°: 18 passi, 19 punti
        Assert.Equal(1.4, Distanza(arco[0], b), 1);
        Assert.Equal(1.4, Distanza(arco[^1], b), 1);
        Assert.True(Distanza(arco[0], Da(O, 0, 8.6)) < 0.02);
        Assert.True(Distanza(arco[^1], Da(b, 90, 1.4)) < 0.02);
        // Il punto di mezzo dell'arco sta dentro la virata, a R·(√2 − 1) dallo spigolo.
        Assert.Equal(1.4 * (Math.Sqrt(2) - 1), Distanza(arco[arco.Count / 2], b), 1);
        Assert.Equal([0, null, 2], new[] { tappe[0].Vertice, tappe[1].Vertice, tappe[^1].Vertice });
    }

    [Fact]
    public void UnPuntoColNome_SiSorvola_EPoiSiViraVersoIlPuntoDopo()
    {
        var b = Da(O, 0, 10);
        var c = Da(b, 90, 10);
        var tappe = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, true, false, null, 1.4), new V(c, true, false, null, 0)]);

        // Il punto resta (la riga col nome nel sector), e dopo la curva a destra.
        Assert.Equal([0, 1], tappe.Take(2).Select(t => t.Vertice));
        var curva = tappe.Skip(2).TakeWhile(t => t.Vertice is null).Select(t => t.Punto).ToList();
        Assert.True(curva.Count > 10);
        // A est del punto (a destra), più a nord del punto (lo si è sorvolato e si continua un po').
        Assert.All(curva, p => Assert.True(p.Lon >= b.Lon - 1e-9));
        Assert.True(curva.Max(p => p.Lat) > b.Lat);
        // Dalla fine della curva la rotta guarda C.
        var fine = curva[^1];
        var ultimoTratto = Sphere.BearingDeg(curva[^2], fine);
        Assert.True(Math.Abs(Sphere.Normalize(Sphere.BearingDeg(fine, c) - ultimoTratto + 180) - 180) < 6);
    }

    [Fact]
    public void OltreI120Gradi_UnPuntoCalcolatoSiSorvola()
    {
        // Verso nord, poi indietro verso sud-sud-est: 150°.
        var b = Da(O, 0, 10);
        var c = Da(b, 150, 10);
        var tappe = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, false, false, null, 1.4), new V(c, true, false, null, 0)]);

        Assert.Contains(tappe, t => t.Vertice == 1);
        Assert.True(tappe.SkipWhile(t => t.Vertice != 1).Skip(1).TakeWhile(t => t.Vertice is null).Count() > 20);
    }

    [Fact]
    public void IlVersoDettoDallaProsa_ComandaAncheSeEIlGiroLungo()
    {
        // Verso nord, poi verso ovest: la più corta è a sinistra (90°), la prosa dice «turn right» (270°): si sorvola.
        var b = Da(O, 0, 10);
        var c = Da(b, 270, 10);
        var tappe = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, false, false, "R", 1.4), new V(c, true, false, null, 0)]);

        Assert.Contains(tappe, t => t.Vertice == 1);
        var curva = tappe.SkipWhile(t => t.Vertice != 1).Skip(1).TakeWhile(t => t.Vertice is null).Select(t => t.Punto).ToList();
        Assert.Contains(curva, p => p.Lon > b.Lon + 0.01);   // la curva va prima a est, cioè a destra
    }

    [Fact]
    public void SottoI5Gradi_FissoEPrimoUltimo_NonSiTocca()
    {
        var b = Da(O, 0, 10);
        var c = Da(b, 3, 10);
        var d = Da(c, 90, 10);
        var e = Da(d, 0, 10);
        var tappe = CurveDelleProcedure.Disegna(
            [new V(O, false, false, null, 1.4), new V(b, false, false, null, 1.4), new V(c, false, true, null, 1.4), new V(d, false, false, null, 0), new V(e, false, false, null, 1.4)]);

        Assert.Equal([0, 1, 2, 3, 4], tappe.Select(t => t.Vertice));
    }

    [Fact]
    public void UnTrattoCorto_StringeIlRaggio()
    {
        // Il tratto dopo è lungo 1 NM: l'arco non può cominciare a 1,4 NM; si prende metà del tratto più corto.
        var b = Da(O, 0, 10);
        var c = Da(b, 90, 1);
        var tappe = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, false, false, null, 1.4), new V(c, true, false, null, 0)]);

        var arco = tappe.Where(t => t.Vertice is null).Select(t => t.Punto).ToList();
        Assert.Equal(0.5, Distanza(arco[0], b), 2);
        Assert.Equal(0.5, Distanza(arco[^1], b), 2);
    }

    [Fact]
    public void UnaRadialeDaIntercettare_SiViraSiPrendeA45GradiESiSegue()
    {
        // Verso nord fino a B; poi «turn right to join» la linea per C con rotta 090°, 3 NM a nord di B.
        var b = Da(O, 0, 10);
        var c = Da(Da(b, 0, 3), 90, 15);
        var tappe = CurveDelleProcedure.Disegna(
            [new V(O, true, false, null, 0), new V(b, true, false, "R", 1.0, RottaDopo: 90), new V(c, true, false, null, 0)]);

        var curva = tappe.SkipWhile(t => t.Vertice != 1).Skip(1).TakeWhile(t => t.Vertice is null).Select(t => t.Punto).ToList();
        Assert.True(curva.Count > 5);
        // Prima la virata a destra fino alla prua 045° (da 000°), poi il raccordo di 45° che finisce sulla linea.
        var fine = curva[^1];
        Assert.Equal(c.Lat, fine.Lat, 3);
        Assert.True(fine.Lon < c.Lon);
        Assert.InRange(Sphere.BearingDeg(curva[^2], fine), 87, 90);   // l'ultima corda di 5°: 87,5°
        // Il tratto diritto fra le due curve è a 45°.
        var prue = curva.Zip(curva.Skip(1), Sphere.BearingDeg).ToList();
        Assert.Contains(prue, p => Math.Abs(p - 45) < 0.5);
    }

    [Fact]
    public void DopoIlVertice_LaCurvaNonRipeteIlVertice()
    {
        // Banco, 1 novembre: «Porta» di LICA SUGEP 3G scriveva «LMT;LMT;» e subito la coordinata di LMT, e il punto dove si
        // prende la radiale due volte di fila. La curva comincia dopo il vertice, non sopra.
        var b = Da(O, 0, 10);
        var sorvolo = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, true, false, null, 1.4), new V(Da(b, 90, 10), true, false, null, 0)]);
        var presa = CurveDelleProcedure.Disegna(
            [new V(O, true, false, null, 0), new V(b, true, false, "R", 1.0, RottaDopo: 90), new V(Da(Da(b, 0, 3), 90, 15), true, false, null, 0)]);

        foreach (var tappe in new[] { sorvolo, presa })
        {
            var dopo = tappe.SkipWhile(t => t.Vertice != 1).Skip(1).First();
            Assert.Null(dopo.Vertice);
            Assert.True(Distanza(dopo.Punto, b) > 0.01);
        }
    }

    [Fact]
    public void UnaRottaCheVaDirittaAlPunto_NonSiIntercetta()
    {
        var b = Da(O, 0, 10);
        var c = Da(b, 90, 10);
        var con = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, false, false, null, 1.4, RottaDopo: 90), new V(c, true, false, null, 0)]);
        var senza = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, false, false, null, 1.4), new V(c, true, false, null, 0)]);

        Assert.Equal(senza, con);
    }

    [Fact]
    public void IlPuntoDopoDentroIlCerchio_NonSiCurvaDopoIlSorvolo()
    {
        var b = Da(O, 0, 10);
        var c = Da(b, 90, 0.5);   // a 0,5 NM, dentro il cerchio di 1,4 NM che parte da B
        var tappe = CurveDelleProcedure.Disegna([new V(O, true, false, null, 0), new V(b, true, false, null, 1.4), new V(c, true, false, null, 0)]);

        Assert.Equal([0, 1, 2], tappe.Select(t => t.Vertice));
    }
}
