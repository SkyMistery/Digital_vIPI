using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.Doc;

namespace Vipi.Ui.Tests;

/// <summary>
/// La pista in uso ADESSO: prima le regole, poi il vento. Dall'11 settembre 2026 la chiedono due documenti —
/// la vIPI d'aeroporto e il vSOP militare — e la risposta deve essere la stessa sullo stesso campo, quindi il
/// calcolo è uno solo (<see cref="PistaInUso"/>) e si prova qui, senza pagine.
///
/// <para>Dal 12 settembre 2026 si valuta sulle regole <b>che il lettore sta guardando</b> (quelle della
/// sezione: congelate o vive), non più sempre sulle vive.</para>
/// </summary>
public class PistaInUsoTests
{
    private static readonly string[] Piste = { "16", "34" };

    /// <summary>Pista 34 preferenziale finché il vento in coda non supera i 10 nodi.</summary>
    private static RunwayRuleRow Preferenziale34(string nome = "Preferenziale") =>
        new(0, "34", "34", nome, MaxTailwindKt: 10, MaxCrosswindKt: null, RunwaySurface.Any, Note: null);

    private static RunwayRuleRow Preferenziale16() =>
        new(0, "16", "16", "Nord", MaxTailwindKt: 10, MaxCrosswindKt: null, RunwaySurface.Any, Note: null);

    [Fact]
    public void La_regola_vince_sul_vento_finche_le_sue_soglie_reggono()
    {
        // Vento 160/05: da solo sceglierebbe la 16, ma sulla 34 sono 5 nodi in coda e la regola ne ammette 10.
        var esito = PistaInUso.Calcola(new[] { Preferenziale34() }, AirportSidView.Empty, Piste, 160, 5, metar: null);

        Assert.NotNull(esito.Regola);
        Assert.Equal(new[] { "34" }, esito.Dep);
        Assert.Equal(new[] { "34" }, esito.Arr);
        Assert.True(esito.SulleRegoleMostrate);
    }

    [Fact]
    public void Oltre_le_soglie_la_regola_cade_e_decide_il_vento()
    {
        // 160/15: sulla 34 sono 15 nodi in coda, oltre i 10 della regola ⇒ nessuna regola vale, e la pista la
        // sceglie il vento — «altrimenti l'altra».
        var esito = PistaInUso.Calcola(new[] { Preferenziale34() }, AirportSidView.Empty, Piste, 160, 15, metar: null);

        Assert.Null(esito.Regola);
        Assert.Equal(new[] { "16" }, esito.Dep);
    }

    [Fact]
    public void Senza_regole_decide_il_vento()
    {
        // Il documento dice che regole non ce ne sono: lista VUOTA, che è un fatto — non «non si sa».
        var esito = PistaInUso.Calcola(Array.Empty<RunwayRuleRow>(), AirportSidView.Empty, Piste, 340, 12, metar: null);

        Assert.Null(esito.Regola);
        Assert.Equal(new[] { "34" }, esito.Dep);
        Assert.Null(esito.SidRwy);   // nessuna SID, nessun seme per il filtro
        Assert.True(esito.SulleRegoleMostrate);
    }

    // ---- Il verdetto segue la sezione (12 settembre 2026) ------------------------------------------------

    /// <summary>
    /// 🔴 Il difetto che questa modifica chiude: la tabella è la fotografia della release e le regole
    /// nell'anagrafica sono cambiate dopo. Si valutano quelle <b>pubblicate</b>, o la pagina marcherebbe una
    /// pista che la tabella che ha sotto non spiega.
    /// </summary>
    [Fact]
    public void Si_valutano_le_regole_MOSTRATE_non_quelle_vive()
    {
        var esito = PistaInUso.Calcola(
            regole: new[] { Preferenziale34() },          // quelle della release, che il lettore vede
            AirportSidView.Empty, Piste, 160, 5, metar: null,
            viveDiRipiego: new[] { Preferenziale16() });  // quelle di adesso, cambiate dopo aver pubblicato

        Assert.Equal("Preferenziale", esito.Regola!.RuleName);
        Assert.Equal(new[] { "34" }, esito.Dep);
        Assert.True(esito.SulleRegoleMostrate);
    }

    /// <summary>
    /// Una release scattata prima del 12 settembre 2026 non porta le regole calcolabili: si ricade sulle vive
    /// — il comportamento di allora — e lo si <b>dichiara</b>, così la pastiglia «adesso» non si mostra su una
    /// riga di un'altra lista.
    /// </summary>
    [Fact]
    public void Una_release_vecchia_ricade_sulle_regole_vive_e_lo_DICE()
    {
        var esito = PistaInUso.Calcola(
            regole: null, AirportSidView.Empty, Piste, 160, 5, metar: null,
            viveDiRipiego: new[] { Preferenziale34() });

        Assert.NotNull(esito.Regola);
        Assert.Equal(new[] { "34" }, esito.Dep);
        Assert.False(esito.SulleRegoleMostrate);
    }

    [Fact]
    public void Senza_regole_ne_mostrate_ne_vive_decide_il_vento()
    {
        var esito = PistaInUso.Calcola(regole: null, AirportSidView.Empty, Piste, 340, 12, metar: null);

        Assert.Null(esito.Regola);
        Assert.Equal(new[] { "34" }, esito.Dep);
        Assert.False(esito.SulleRegoleMostrate);
    }
}
