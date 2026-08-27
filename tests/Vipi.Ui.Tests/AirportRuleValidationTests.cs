using Vipi.Application.Weather;
using Vipi.Domain;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le regole di scelta pista: la validazione, e la conversione verso il dominio.
///
/// <para>
/// ⚠️ Perché la conversione ha le sue prove. Stava scritta <b>due volte</b> nella pagina, campo per campo —
/// una per il banco di prova (<c>RunwayRuleEval</c>) e una per il salvataggio (<c>RunwayRuleRow</c>). Due
/// copie che possono divergere: il banco direbbe che con vento da 200° vince la regola #2, e il documento
/// pubblicato ne applicherebbe un'altra. Su una regola che dice quale pista è in uso, è il difetto peggiore
/// possibile — e non l'avrebbe trovato nessuno leggendo il codice, perché le due copie erano lontane
/// quattrocento righe.
/// </para>
/// </summary>
public class AirportRuleValidationTests
{
    private static RuleEdit Regola(string[]? dep = null, string[]? arr = null,
        TimeOnly? da = null, TimeOnly? a = null)
    {
        var r = new RuleEdit();
        foreach (var d in dep ?? Array.Empty<string>()) r.Dep.Add(d);
        foreach (var x in arr ?? Array.Empty<string>()) r.Arr.Add(x);
        r.TimeFrom = da;
        r.TimeTo = a;
        return r;
    }

    private static readonly string[] PisteDiRoma = { "16L", "16R", "34L", "34R" };

    // ---------------------------------------------------------------------------------------------------
    // Validazione
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Una_regola_sana_non_ha_problemi()
    {
        var esito = AirportRuleValidation.Issues(new[] { Regola(dep: new[] { "16L" }, arr: new[] { "16R" }) }, PisteDiRoma);

        Assert.Empty(esito.Errors);
        Assert.Empty(esito.Warnings);
    }

    [Fact]
    public void Una_regola_senza_piste_e_un_ERRORE_e_blocca_il_salvataggio()
    {
        // Non sceglie niente: salvarla metterebbe in archivio una riga che non può mai vincere.
        var esito = AirportRuleValidation.Issues(new[] { Regola() }, PisteDiRoma);

        var errore = Assert.Single(esito.Errors);
        Assert.Equal("Ape_IssueRuleNoRw", errore.Key);
        Assert.Equal(new object[] { 1 }, errore.Args);
    }

    [Fact]
    public void Basta_una_pista_in_DEP_oppure_in_ARR()
    {
        // Una regola che governa solo le partenze è legittima: gli arrivi li decide quella dopo.
        Assert.Empty(AirportRuleValidation.Issues(new[] { Regola(dep: new[] { "16L" }) }, PisteDiRoma).Errors);
        Assert.Empty(AirportRuleValidation.Issues(new[] { Regola(arr: new[] { "34R" }) }, PisteDiRoma).Errors);
    }

    [Fact]
    public void Una_pista_che_lo_scalo_non_ha_e_un_AVVISO_non_un_errore()
    {
        // ⚠️ È un refuso, o una pista tolta dopo che la regola era scritta. Resta salvabile: chi la corregge
        // è chi sa quale pista intendeva, e bloccarlo gli toglierebbe anche il resto del lavoro.
        var esito = AirportRuleValidation.Issues(new[] { Regola(dep: new[] { "07" }) }, PisteDiRoma);

        Assert.Empty(esito.Errors);
        var avviso = Assert.Single(esito.Warnings);
        Assert.Equal("Ape_IssueRuleUnknownRw", avviso.Key);
        Assert.Equal(new object[] { 1, "07" }, avviso.Args);
    }

    [Fact]
    public void Mezza_finestra_oraria_e_un_avviso()
    {
        // «Dalle 06:00» senza un «fino a» non si sa dove finisce.
        var soloInizio = AirportRuleValidation.Issues(
            new[] { Regola(dep: new[] { "16L" }, da: new TimeOnly(6, 0)) }, PisteDiRoma);
        var soloFine = AirportRuleValidation.Issues(
            new[] { Regola(dep: new[] { "16L" }, a: new TimeOnly(22, 0)) }, PisteDiRoma);

        Assert.Equal("Ape_IssueRuleTimeWin", Assert.Single(soloInizio.Warnings).Key);
        Assert.Equal("Ape_IssueRuleTimeWin", Assert.Single(soloFine.Warnings).Key);

        // Con tutt'e due gli estremi, nessun avviso.
        Assert.Empty(AirportRuleValidation.Issues(
            new[] { Regola(dep: new[] { "16L" }, da: new TimeOnly(6, 0), a: new TimeOnly(22, 0)) }, PisteDiRoma).Warnings);
    }

    [Fact]
    public void Le_regole_si_nominano_per_POSIZIONE_perche_la_posizione_e_la_regola()
    {
        // L'ordine non è presentazione: si applica la PRIMA che vince. «#3» dice quale riga guardare.
        var esito = AirportRuleValidation.Issues(
            new[] { Regola(dep: new[] { "16L" }), Regola(dep: new[] { "34R" }), Regola() }, PisteDiRoma);

        Assert.Equal(new object[] { 3 }, Assert.Single(esito.Errors).Args);
    }

    // ---------------------------------------------------------------------------------------------------
    // Conversione — la parte che stava scritta due volte
    // ---------------------------------------------------------------------------------------------------

    private static RuleEdit RegolaCompleta()
    {
        var r = new RuleEdit { Name = "Preferenziale notte", Note = "vento permettendo", MaxTail = 7, MaxCross = 25, Surface = "wet", Parity = "even", DaysMask = 0b0000101, TimeFrom = new TimeOnly(22, 30), TimeTo = new TimeOnly(6, 15), DateFromDay = 1, DateFromMonth = 11, DateToDay = 31, DateToMonth = 3 };
        r.Dep.Add("16L"); r.Dep.Add("16R");
        r.Arr.Add("34L");
        return r;
    }

    [Fact]
    public void La_conversione_per_la_PROVA_e_quella_per_il_SALVATAGGIO_dicono_la_stessa_cosa()
    {
        // ⚠️ È l'invariante che le due copie separate non potevano garantire.
        var r = RegolaCompleta();
        var prova = AirportRuleMapping.ToEval(r);
        var salvata = AirportRuleMapping.ToRow(r);

        Assert.Equal(prova.DepRunways, salvata.DepRunways);
        Assert.Equal(prova.ArrRunways, salvata.ArrRunways);
        Assert.Equal(prova.Name, salvata.Name);
        Assert.Equal(prova.Note, salvata.Note);
        Assert.Equal(prova.MaxTailwindKt, salvata.MaxTailwindKt);
        Assert.Equal(prova.MaxCrosswindKt, salvata.MaxCrosswindKt);
        Assert.Equal(prova.Surface, salvata.Surface);
        Assert.Equal(prova.TimeFromLocalMin, salvata.TimeFromLocalMin);
        Assert.Equal(prova.TimeToLocalMin, salvata.TimeToLocalMin);
        Assert.Equal(prova.DaysOfWeekMask, salvata.DaysOfWeekMask);
        Assert.Equal(prova.DateParity, salvata.DateParity);
        Assert.Equal(prova.DateFromMonthDay, salvata.DateFromMonthDay);
        Assert.Equal(prova.DateToMonthDay, salvata.DateToMonthDay);
    }

    [Fact]
    public void L_ora_diventa_minuti_dalla_mezzanotte()
    {
        Assert.Equal(22 * 60 + 30, AirportRuleMapping.TimeToMin(new TimeOnly(22, 30)));
        Assert.Equal(0, AirportRuleMapping.TimeToMin(new TimeOnly(0, 0)));
        Assert.Null(AirportRuleMapping.TimeToMin(null));
    }

    [Fact]
    public void La_finestra_stagionale_vale_solo_con_giorno_E_mese()
    {
        // ⚠️ Metà data non è una data: in DB è un solo numero MMDD, e «novembre senza il giorno» non si
        // scrive. Null vuol dire «nessun estremo da questo lato», che è diverso da «il 1º di qualcosa».
        Assert.Equal(1101, AirportRuleMapping.CombineMd(11, 1));
        Assert.Equal(331, AirportRuleMapping.CombineMd(3, 31));
        Assert.Null(AirportRuleMapping.CombineMd(11, null));
        Assert.Null(AirportRuleMapping.CombineMd(null, 1));
        Assert.Null(AirportRuleMapping.CombineMd(null, null));
    }

    [Theory]
    [InlineData("dry", RunwaySurface.Dry)]
    [InlineData("wet", RunwaySurface.Wet)]
    [InlineData("any", RunwaySurface.Any)]
    [InlineData(null, RunwaySurface.Any)]
    [InlineData("qualcos'altro", RunwaySurface.Any)]
    public void La_superficie_sconosciuta_vale_come_QUALSIASI(string? scritta, RunwaySurface attesa) =>
        Assert.Equal(attesa, AirportRuleMapping.Surface(scritta));

    [Theory]
    [InlineData("even", DateParity.Even)]
    [InlineData("odd", DateParity.Odd)]
    [InlineData("", DateParity.Any)]
    [InlineData(null, DateParity.Any)]
    public void La_parita_sconosciuta_vale_come_QUALSIASI(string? scritta, DateParity attesa) =>
        Assert.Equal(attesa, AirportRuleMapping.Parity(scritta));

    [Fact]
    public void Nessun_giorno_scelto_vuol_dire_TUTTI_i_giorni()
    {
        // ⚠️ La maschera a zero non è «nessun giorno» — sarebbe una regola che non si applica mai. È
        // «tutti», e per dirlo al dominio si passa null.
        var mai = new RuleEdit { DaysMask = 0 };
        mai.Dep.Add("16L");
        Assert.Null(AirportRuleMapping.ToEval(mai).DaysOfWeekMask);

        var soloLunedi = new RuleEdit { DaysMask = 0b0000001 };
        soloLunedi.Dep.Add("16L");
        Assert.Equal(0b0000001, AirportRuleMapping.ToEval(soloLunedi).DaysOfWeekMask);
    }
}
