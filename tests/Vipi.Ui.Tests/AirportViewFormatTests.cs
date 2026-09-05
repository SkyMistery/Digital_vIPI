using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Tests;

/// <summary>
/// Logica di presentazione condivisa dalle viste aeroporto (documento completo + pannello rapido), prima
/// duplicata in entrambi i componenti e quindi non coperta da test. Sono funzioni pure: qui si fissa il
/// comportamento atteso, incluso quello che le due copie rendevano in modo diverso.
/// </summary>
public class AirportViewFormatTests
{
    // ---- InitialClimb ----

    [Theory]
    [InlineData("5000", 6000, "5000 ft")]      // sotto la TA
    [InlineData("6000", 6000, "6000 ft")]      // esattamente alla TA: resta in piedi
    [InlineData("9000", 6000, "FL90")]         // sopra la TA
    [InlineData("11000", 6000, "FL110")]
    [InlineData("5,000", 6000, "5000 ft")]     // separatore delle migliaia
    [InlineData("9,500", 6000, "FL95")]
    public void InitialClimb_Sceglie_Piedi_O_Livello_In_Base_Alla_Ta(string raw, int ta, string expected) =>
        Assert.Equal(expected, AirportViewFormat.InitialClimb(raw, ta));

    [Fact]
    public void InitialClimb_Senza_Ta_Resta_In_Piedi()
    {
        // TA sconosciuta: non si può decidere il livello, quindi non si inventa.
        Assert.Equal("9000 ft", AirportViewFormat.InitialClimb("9000", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("—")]
    public void InitialClimb_Vuoto_Rende_Il_Trattino(string? raw) =>
        Assert.Equal("—", AirportViewFormat.InitialClimb(raw, 6000));

    [Fact]
    public void InitialClimb_Preserva_La_Nota_Dopo_La_Quota()
    {
        Assert.Equal("FL90 (to coord with APP)",
            AirportViewFormat.InitialClimb("9000 (to coord with APP)", 6000));
        Assert.Equal("5000 ft or as assigned",
            AirportViewFormat.InitialClimb("5000 or as assigned", 6000));
    }

    [Fact]
    public void InitialClimb_Senza_Quota_Numerica_Torna_Invariato()
    {
        // Valore puramente testuale: si mostra così com'è, non si forza un formato.
        Assert.Equal("to coord with APP",
            AirportViewFormat.InitialClimb("to coord with APP", 6000));
    }

    [Fact]
    public void InitialClimb_Arrotonda_Il_Livello_Al_Centinaio_Piu_Vicino()
    {
        Assert.Equal("FL75", AirportViewFormat.InitialClimb("7460", 6000));
        Assert.Equal("FL75", AirportViewFormat.InitialClimb("7540", 6000));
    }

    // ---- QnhRowMatches ----

    [Theory]
    [InlineData("1014 – 1030", 1014, true)]     // estremi inclusi
    [InlineData("1014 – 1030", 1030, true)]
    [InlineData("1014 – 1030", 1022, true)]
    [InlineData("1014 – 1030", 1013, false)]
    [InlineData("1014 – 1030", 1031, false)]
    public void QnhRowMatches_Intervallo(string range, int qnh, bool expected) =>
        Assert.Equal(expected, AirportViewFormat.QnhRowMatches(range, qnh));

    [Theory]
    [InlineData("≥ 1031", 1031, true)]
    [InlineData("≥ 1031", 1030, false)]
    [InlineData(">= 1031", 1032, true)]
    [InlineData("≤ 984", 984, true)]
    [InlineData("≤ 984", 985, false)]
    [InlineData("<= 984", 983, true)]
    [InlineData("> 1031", 1031, false)]
    [InlineData("> 1031", 1032, true)]
    [InlineData("< 984", 984, false)]
    [InlineData("< 984", 983, true)]
    public void QnhRowMatches_Disuguaglianze(string range, int qnh, bool expected) =>
        Assert.Equal(expected, AirportViewFormat.QnhRowMatches(range, qnh));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("n/d")]
    public void QnhRowMatches_Riga_Senza_Numeri_Non_Corrisponde(string? range) =>
        Assert.False(AirportViewFormat.QnhRowMatches(range, 1013));

    [Fact]
    public void QnhRowMatches_Intervallo_Invertito_Funziona_Comunque()
    {
        // Robustezza sul dato editoriale: l'ordine degli estremi non deve cambiare l'esito.
        Assert.True(AirportViewFormat.QnhRowMatches("1030 – 1014", 1022));
    }

    // ---- ParseTransitionLevels ----

    [Fact]
    public void ParseTransitionLevels_Legge_Colonne_E_Righe()
    {
        const string json = """
        {"columns":["QNH (hPa)","Transition Level"],
         "rows":[{"cells":["≥ 1031","FL65"]},{"cells":["1014 – 1030","FL70"]}]}
        """;

        var t = AirportViewFormat.ParseTransitionLevels(json);

        Assert.Equal(new[] { "QNH (hPa)", "Transition Level" }, t.Columns);
        Assert.Equal(2, t.Rows.Count);
        Assert.Equal(("≥ 1031", "FL65"), t.Rows[0]);
        Assert.Equal(("1014 – 1030", "FL70"), t.Rows[1]);
    }

    [Fact]
    public void ParseTransitionLevels_Senza_Colonne_Rende_Solo_Le_Righe()
    {
        // È il caso che il pannello rapido usava: prima aveva un parser proprio che ignorava "columns".
        var t = AirportViewFormat.ParseTransitionLevels("""{"rows":[{"cells":["≥ 1031","FL65"]}]}""");

        Assert.Empty(t.Columns);
        Assert.Single(t.Rows);
    }

    [Fact]
    public void ParseTransitionLevels_Scarta_Le_Righe_Con_Meno_Di_Due_Celle()
    {
        var t = AirportViewFormat.ParseTransitionLevels(
            """{"rows":[{"cells":["solo-una"]},{"cells":["≥ 1031","FL65"]},{"cells":[]}]}""");

        Assert.Single(t.Rows);
        Assert.Equal(("≥ 1031", "FL65"), t.Rows[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ non è json")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"rows":"non-un-array"}""")]
    [InlineData("""{"columns":42,"rows":[]}""")]
    public void ParseTransitionLevels_Input_Non_Valido_Rende_Tabella_Vuota(string? json)
    {
        // La sezione TL è informativa: un blocco malformato non deve far cadere il render della pagina.
        var t = AirportViewFormat.ParseTransitionLevels(json);

        Assert.Empty(t.Columns);
        Assert.Empty(t.Rows);
    }

    // ---- MapRule ----

    [Fact]
    public void MapRule_Riporta_Tutti_I_Campi_Della_Regola()
    {
        var row = new RunwayRuleRow(
            Id: 7, DepRunways: "16R", ArrRunways: "16L", Name: "Config Nord", Note: "preferenziale",
            MaxTailwindKt: 5, MaxCrosswindKt: 15, Surface: RunwaySurface.Wet,
            TimeFromLocalMin: 360, TimeToLocalMin: 1320, DaysOfWeekMask: 62,
            DateParity: DateParity.Odd,
            DateFromMonthDay: 401, DateToMonthDay: 1031);

        var eval = AirportViewFormat.MapRule(row);

        Assert.Equal("16R", eval.DepRunways);
        Assert.Equal("16L", eval.ArrRunways);
        Assert.Equal("Config Nord", eval.Name);
        Assert.Equal("preferenziale", eval.Note);
        Assert.Equal(5, eval.MaxTailwindKt);
        Assert.Equal(15, eval.MaxCrosswindKt);
        Assert.Equal(RunwaySurface.Wet, eval.Surface);
        Assert.Equal(360, eval.TimeFromLocalMin);
        Assert.Equal(1320, eval.TimeToLocalMin);
        Assert.Equal(62, eval.DaysOfWeekMask);
        Assert.Equal(DateParity.Odd, eval.DateParity);
        Assert.Equal(401, eval.DateFromMonthDay);
        Assert.Equal(1031, eval.DateToMonthDay);
    }
}
