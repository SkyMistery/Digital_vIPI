using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// Segnalazioni 2611, passo 5 della revisione col committente: le pendenze minime di salita come dato della procedura. I testi
/// sono quelli dell'AIP del 2611.
/// </summary>
public sealed class ClimbGradientsTests
{
    private static string Riga(ItalianClimbGradients.Letta l) =>
        $"{l.Pendenza.FeetPerNm} {l.Pendenza.Percent} {l.Pendenza.Until} {l.Pendenza.Reason} [{string.Join(",", l.Sid)}]";

    [Fact]
    public void UnaPendenzaNelTesto()
    {
        var lette = ItalianClimbGradients.Read("Minimum climb gradient 8.3% (500 ft/NM) until leaving 4500FT. After take-off proceed on RWY heading.");

        Assert.Equal(["500 8.3 4500FT Unspecified []"], lette.Select(Riga));
    }

    // LIPO 6: il blocco in testa alla pagina, per SID, con la pendenza dell'ATC e, fra parentesi, quella degli ostacoli.
    [Fact]
    public void IlBloccoPerLeSidSeguenti_AtcEOstacoli()
    {
        var lette = ItalianClimbGradients.Read("Minimum climb gradient:\nFor the following SIDs: OSKOR 7W and UVNIB 1W\n"
            + "- 365 ft/NM - 6.0% until passing FL90 for ATC reasons (a gradient of 334 ft/NM - 5.5% until passing 5000ft provides separation from\nobstacles).\n"
            + "For the following SID: CIBFU 6A\n- 450 ft/NM - 7.4% until passing FL110 for ATC reasons (a gradient of 334 ft/NM - 5.5% until passing 5000ft provides separation from obstacles).");

        Assert.Equal(
        [
            "365 6 FL90 Atc [OSKOR 7W,UVNIB 1W]", "334 5.5 5000ft Obstacles [OSKOR 7W,UVNIB 1W]",
            "450 7.4 FL110 Atc [CIBFU 6A]", "334 5.5 5000ft Obstacles [CIBFU 6A]",
        ], lette.Select(Riga));
        Assert.Equal(334, ItalianClimbGradients.PerDisegnare([.. ItalianClimbGradients.Per(lette, "OSKOR 7W", ancheSenzaNomi: true)])!.FeetPerNm);
    }

    // LIEE: il perché in testa («(ATC)»), la SID in coda; LIRZ: le SID davanti a «Minimum climb gradient»; LIPX: le SID
    // davanti al valore, dopo «then: a) if proceeding via SID …:».
    [Theory]
    [InlineData("Minimum climb gradient (ATC): - 300 ft/NM (4.9%) up to FL 150 if cleared via SID KOVAS 6E; e - 320 ft/NM (5.3%) if cleared via SID CAR 6E.",
        "300 4.9 FL 150 Atc [KOVAS 6E]|320 5.3  Atc [CAR 6E]")]
    [InlineData("REMARKS 1. For LAKOX 7W, GITOD 8W, KATAR 9W, Minimum climb gradient: 5.8% (355 ft/NM) until passing 5000 ft; 2. For KATAR 9Z, Minimum climb gradient: 5.8% (355 ft/NM) until passing TRANSITION LEVEL;",
        "355 5.8 5000 ft Unspecified [LAKOX 7W,GITOD 8W,KATAR 9W]|355 5.8 TRANSITION LEVEL Unspecified [KATAR 9Z]")]
    [InlineData("Minimum climb gradient 600 ft/NM (9.8%) till passing 1000 FT then: a) if proceeding via SID ECVAZ 8A and UPCAL 7A: 395 ft/NM (6,5%); b) if proceeding via SID TUVDE 7A 515 ft/NM (8.5%), until passing FL 85 due to obstacles and ATC reasons.",
        "600 9.8 1000 FT Unspecified []|395 6.5  Unspecified [ECVAZ 8A,UPCAL 7A]|515 8.5 FL 85 Obstacles [TUVDE 7A]")]
    [InlineData("REMARK Minimum climb gradient: - 7.5% (456 ft/NM) until passing 6000FT for FARAK 7Q and IRKED 8E - 7.5% (456 ft/NM) until passing FL95 for IBCUC 7F",
        "456 7.5 6000FT Unspecified [FARAK 7Q,IRKED 8E]|456 7.5 FL95 Unspecified [IBCUC 7F]")]   // LIMC: le SID in coda, senza punto e virgola
    public void LeSidDelleVoci(string testo, string attese) =>
        Assert.Equal(attese.Split('|'), ItalianClimbGradients.Read(testo).Select(Riga));

    // Le avvertenze dopo la pendenza non sono pendenze («Close-in obstacles penetrating OIS 2.5% exist», LIEA).
    [Fact]
    public void LAvvertenzaDopo_NonEUnaPendenza()
    {
        var lette = ItalianClimbGradients.Read("Minimum climb gradient: 318 ft/NM until passing 2500 ft Close-in obstacles penetrating OIS 2.5% exist but were not considered.");

        Assert.Equal(["318  2500 ft Unspecified []"], lette.Select(Riga));
    }

    // Per disegnare (committente): gli ostacoli quando ci sono; una sola senza motivo; se no niente.
    [Fact]
    public void LaPendenzaPerDisegnare()
    {
        ClimbGradient P(double ft, ClimbGradientReason r) => new(ft, null, null, r, $"{ft}");

        Assert.Equal(300, ItalianClimbGradients.PerDisegnare([P(365, ClimbGradientReason.Atc), P(300, ClimbGradientReason.Obstacles)])!.FeetPerNm);
        Assert.Equal(400, ItalianClimbGradients.PerDisegnare([P(400, ClimbGradientReason.Unspecified)])!.FeetPerNm);
        Assert.Null(ItalianClimbGradients.PerDisegnare([P(400, ClimbGradientReason.Unspecified), P(350, ClimbGradientReason.Unspecified)]));
        Assert.Null(ItalianClimbGradients.PerDisegnare([]));
        Assert.Equal(500, ItalianClimbGradients.PiediPerMiglio(new ClimbGradient(null, 500 / 6076.12 * 100, null, ClimbGradientReason.Unspecified, ""))!.Value, 3);
    }
}
