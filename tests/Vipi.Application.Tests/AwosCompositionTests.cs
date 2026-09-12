using Vipi.Application.Awos;
using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il cuore deterministico del quadro vAWOS: strisce di pista, TL, QFE, pista in uso.
/// Nessun IO: è il pezzo che si prova senza banco (carta 2026-09-12, fetta 2).
/// </summary>
public class AwosCompositionTests
{
    private static RunwayRow Pista(string ident, int? bearing = null, int? elev = null) =>
        new(0, ident, null, bearing, null, null, null, null, null, null, null, elev);

    // ─── Strisce ───────────────────────────────────────────────────────────────────

    [Fact] // Fiumicino: tre piste fisiche, sei testate, ZERO codice per aeroporto
    public void Tre_Piste_Diventano_Tre_Strisce()
    {
        var strisce = AwosComposition.Strisce(new[]
        {
            Pista("07", 70), Pista("25", 250),
            Pista("16L", 159), Pista("34R", 339),
            Pista("16R", 159), Pista("34L", 339),
        });

        Assert.Equal(3, strisce.Count);
        Assert.All(strisce, s => Assert.NotNull(s.Right));

        // a sinistra l'ident numericamente minore, come sul quadro vero
        Assert.Equal("07", strisce[0].Left.Ident);
        Assert.Equal("25", strisce[0].Right!.Ident);
    }

    [Fact] // si accoppia per ROTTA, non per ident: 16L sta con 34R, non con 16R
    public void Le_Parallele_Non_Si_Accoppiano_Fra_Loro()
    {
        var strisce = AwosComposition.Strisce(new[]
        {
            Pista("16L", 159), Pista("16R", 159), Pista("34L", 339), Pista("34R", 339),
        });

        Assert.Equal(2, strisce.Count);
        foreach (var s in strisce)
        {
            Assert.NotNull(s.Right);
            Assert.NotEqual(s.Left.Ident[..2], s.Right!.Ident[..2]);   // mai 16 con 16
        }
    }

    [Fact] // una testata senza opposta resta SOLA: non le si inventa una gemella a +180
    public void Testata_Spaiata_Resta_Sola()
    {
        var strisce = AwosComposition.Strisce(new[] { Pista("03L", 30), Pista("21R", 210), Pista("17", 170) });

        Assert.Equal(2, strisce.Count);
        var spaiata = Assert.Single(strisce, s => s.Right is null);
        Assert.Equal("17", spaiata.Left.Ident);
    }

    [Fact] // la rotta è quella dell'ANAGRAFICA, non quella dedotta dal nome
    public void La_Rotta_Vera_Vince_Sul_Nome()
    {
        var strisce = AwosComposition.Strisce(new[] { Pista("16L", 159), Pista("34R", 339) });
        Assert.Equal(159, strisce[0].Left.HeadingDeg);      // NON 160
        Assert.Equal(339, strisce[0].Right!.HeadingDeg);
    }

    [Fact] // senza rotta in archivio si deduce dall'ident: ripiego dichiarato, non un valore
    public void Senza_Rotta_Si_Deduce_Dall_Ident()
    {
        Assert.Equal(170, AwosComposition.Rotta("17", null));
        Assert.Equal(360, AwosComposition.Rotta("36", null));
        Assert.Equal(159, AwosComposition.Rotta("16L", 159));
    }

    [Fact] // nessuna pista in archivio: nessuna striscia, e non è un errore
    public void Nessuna_Pista_Nessuna_Striscia() =>
        Assert.Empty(AwosComposition.Strisce(Array.Empty<RunwayRow>()));

    // ─── Transition level ──────────────────────────────────────────────────────────

    [Fact] // il TL viene dalla TABELLA dello scalo, per fascia di QNH
    public void Il_Tl_Viene_Dalla_Tabella()
    {
        var righe = new[]
        {
            new TlRow(1, 1013, null, "FL70"),
            new TlRow(2, 995, 1012, "FL75"),
            new TlRow(3, null, 994, "FL80"),
        };

        Assert.Equal("FL70", AwosComposition.TransitionLevel(righe, 1020));
        Assert.Equal("FL75", AwosComposition.TransitionLevel(righe, 1000));
        Assert.Equal("FL80", AwosComposition.TransitionLevel(righe, 980));
    }

    [Fact] // senza QNH o senza tabella si dice «non lo so», non «FL70»
    public void Senza_Qnh_O_Tabella_Il_Tl_E_Null()
    {
        var righe = new[] { new TlRow(1, 1013, null, "FL70") };
        Assert.Null(AwosComposition.TransitionLevel(righe, null));
        Assert.Null(AwosComposition.TransitionLevel(Array.Empty<TlRow>(), 1013));
        Assert.Null(AwosComposition.TransitionLevel(righe, 990));      // nessuna fascia copre
    }

    // ─── QFE ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Qfe_Dalla_Elevazione_Della_Soglia()
    {
        Assert.Equal(1010, AwosComposition.Qfe(1013, 81));    // 81/27 = 3 hPa
        Assert.Null(AwosComposition.Qfe(1013, null));         // soglia senza elevazione: non si stima
        Assert.Null(AwosComposition.Qfe(null, 81));
    }

    // ─── Pista in uso ──────────────────────────────────────────────────────────────

    private static RunwayRuleRow Regola(string dep, string arr, string nome, int coda = 5) =>
        new(0, dep, arr, nome, coda, null, RunwaySurface.Any, null);

    [Fact] // l'ATIS batte tutto: dice che cosa STA succedendo
    public void L_Atis_Vince_Sulle_Regole()
    {
        var metar = MetarParser.ParseMetar("LIRF 121250Z 34015KT 9999 NSC 12/08 Q1013");
        var attiva = AwosComposition.PistaAttiva(
            new[] { Regola("16R", "16L", "Config 16") }, new[] { "16L", "16R", "34L", "34R" }, metar,
            atisDep: new[] { "34L" }, atisArr: new[] { "34R" }, daChi: "LIRF_TWR");

        Assert.Equal(AwosRunwaySource.Atis, attiva.Sorgente);
        Assert.Equal("34L", attiva.Dep);
        Assert.Equal("34R", attiva.Arr);
        Assert.Equal("LIRF_TWR", attiva.Dettaglio);
    }

    [Fact] // senza ATIS decide la regola, e il quadro NOMINA la regola che vince
    public void Senza_Atis_Decide_La_Regola_E_La_Nomina()
    {
        var metar = MetarParser.ParseMetar("LIRF 121250Z 16008KT 9999 NSC 12/08 Q1013");
        var attiva = AwosComposition.PistaAttiva(
            new[] { Regola("16R", "16L", "Config 16") }, new[] { "16L", "16R", "34L", "34R" }, metar);

        Assert.Equal(AwosRunwaySource.Regola, attiva.Sorgente);
        Assert.Equal("16R", attiva.Dep);
        Assert.Equal("16L", attiva.Arr);
        Assert.Equal("Config 16", attiva.Dettaglio);
    }

    [Fact] // nessuna regola applicabile (troppa coda): si ricade sul vento, e si DICE
    public void Regola_Che_Non_Si_Applica_Lascia_Decidere_Al_Vento()
    {
        var metar = MetarParser.ParseMetar("LIRF 121250Z 34020KT 9999 NSC 12/08 Q1013");
        var attiva = AwosComposition.PistaAttiva(
            new[] { Regola("16R", "16L", "Config 16", coda: 5) }, new[] { "16L", "16R", "34L", "34R" }, metar);

        Assert.Equal(AwosRunwaySource.Vento, attiva.Sorgente);
        Assert.StartsWith("34", attiva.Dep);
    }

    [Fact] // vento calmo e nessuna regola: NESSUNA pista, non una a caso
    public void Vento_Calmo_Nessuna_Pista()
    {
        var metar = MetarParser.ParseMetar("LIRF 121250Z 00000KT 9999 NSC 12/08 Q1013");
        var attiva = AwosComposition.PistaAttiva(Array.Empty<RunwayRuleRow>(), new[] { "16L", "34R" }, metar);

        Assert.Equal(AwosRunwaySource.Nessuna, attiva.Sorgente);
        Assert.Null(attiva.Dep);
    }

    [Fact] // senza METAR il quadro non inventa una configurazione
    public void Senza_Metar_Nessuna_Pista()
    {
        var attiva = AwosComposition.PistaAttiva(Array.Empty<RunwayRuleRow>(), new[] { "16L", "34R" }, null);
        Assert.Equal(AwosRunwaySource.Nessuna, attiva.Sorgente);
    }
}
