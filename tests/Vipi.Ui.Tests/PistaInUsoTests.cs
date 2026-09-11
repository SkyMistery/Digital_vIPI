using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.Doc;

namespace Vipi.Ui.Tests;

/// <summary>
/// La pista in uso ADESSO: prima le regole piste, poi il vento. Dall'11 settembre 2026 la chiedono due
/// documenti — la vIPI d'aeroporto e il vSOP militare — e la risposta deve essere la stessa sullo stesso campo,
/// quindi il calcolo è uno solo (<see cref="PistaInUso"/>) e si prova qui, senza pagine.
/// </summary>
public class PistaInUsoTests
{
    private static readonly string[] Piste = { "16", "34" };

    private static AirportData Scalo(params RunwayRuleRow[] regole) => new()
    {
        AirportId = 1, Icao = "LIBG", Name = "Grottaglie", AccCode = "LIBB",
        TransitionLevels = Array.Empty<TlRow>(), Runways = Array.Empty<RunwayRow>(), Rules = regole,
        Sids = Array.Empty<SidRow>(), Links = Array.Empty<FrequencyLinkRow>(),
    };

    /// <summary>Pista 34 preferenziale finché il vento in coda non supera i 10 nodi.</summary>
    private static RunwayRuleRow Preferenziale34() =>
        new(0, "34", "34", "Preferenziale", MaxTailwindKt: 10, MaxCrosswindKt: null, RunwaySurface.Any, Note: null);

    [Fact]
    public void La_regola_vince_sul_vento_finche_le_sue_soglie_reggono()
    {
        // Vento 160/05: da solo sceglierebbe la 16, ma sulla 34 sono 5 nodi in coda e la regola ne ammette 10.
        var esito = PistaInUso.Calcola(Scalo(Preferenziale34()), AirportSidView.Empty, Piste, 160, 5, metar: null);

        Assert.NotNull(esito.Regola);
        Assert.Equal(new[] { "34" }, esito.Dep);
        Assert.Equal(new[] { "34" }, esito.Arr);
    }

    [Fact]
    public void Oltre_le_soglie_la_regola_cade_e_decide_il_vento()
    {
        // 160/15: sulla 34 sono 15 nodi in coda, oltre i 10 della regola ⇒ nessuna regola vale, e la pista la
        // sceglie il vento — «altrimenti l'altra».
        var esito = PistaInUso.Calcola(Scalo(Preferenziale34()), AirportSidView.Empty, Piste, 160, 15, metar: null);

        Assert.Null(esito.Regola);
        Assert.Equal(new[] { "16" }, esito.Dep);
    }

    [Fact]
    public void Senza_regole_decide_il_vento()
    {
        // È il caso di tutti i vSOP militari fino all'11 settembre 2026: nessuna regola scritta per lo scalo.
        var esito = PistaInUso.Calcola(Scalo(), AirportSidView.Empty, Piste, 340, 12, metar: null);

        Assert.Null(esito.Regola);
        Assert.Equal(new[] { "34" }, esito.Dep);
        Assert.Null(esito.SidRwy);   // nessuna SID, nessun seme per il filtro
    }
}
