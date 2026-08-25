using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Quanto traffico di un aeroporto ha trovato un controllore.
///
/// <para>⚠️ Il test che vale più di tutti è quello sull'<b>istante</b>: arrivo e partenza si attribuiscono
/// a due momenti diversi, e prenderne uno solo per tutti e due sposterebbe metà dei movimenti di ore.</para>
/// </summary>
public class AirportCoverageTests
{
    private static readonly DateTimeOffset Giorno = new(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset H(double ore) => Giorno.AddHours(ore);

    private static SourceAirportMovement Arrivo(string cs, double collegato, double ultimo, long? fp = null) =>
        new(AirportMovementKind.Inbound, cs, 1, fp, "LIML", "LIRF", "A320", H(collegato), H(ultimo));

    private static SourceAirportMovement Partenza(string cs, double collegato, double ultimo, long? fp = null) =>
        new(AirportMovementKind.Outbound, cs, 1, fp, "LIRF", "LIML", "A320", H(collegato), H(ultimo));

    private static IReadOnlyList<OnlineSpan> Aperto(double da, double a) =>
        new[] { new OnlineSpan(H(da), H(a)) };

    /// <summary>
    /// ⚠️ Il cuore della regola. Un volo collegato alle 08 e atterrato alle 10 con la torre aperta dalle 09
    /// alle 11: come ARRIVO è coperto (conta l'atterraggio), come PARTENZA no (conta il collegamento).
    /// Chi usasse un istante solo per tutti e due sbaglierebbe metà dei movimenti.
    /// </summary>
    [Fact]
    public void L_arrivo_conta_all_ultimo_avvistamento_la_partenza_al_collegamento()
    {
        var spans = Aperto(9, 11);

        var arrivo = AirportCoverage.Tally(new[] { Arrivo("AZA1", 8, 10) }, spans, H(0), H(24));
        var partenza = AirportCoverage.Tally(new[] { Partenza("AZA2", 8, 10) }, spans, H(0), H(24));

        Assert.Equal(1, arrivo.Covered);
        Assert.Equal(0, partenza.Covered);
    }

    [Fact]
    public void Conta_arrivi_partenze_e_movimenti()
    {
        var t = AirportCoverage.Tally(
            new[] { Arrivo("A", 9, 9.5), Arrivo("B", 9, 9.6), Partenza("C", 10, 12) },
            Aperto(9, 11), H(0), H(24));

        Assert.Equal(2, t.Inbound);
        Assert.Equal(1, t.Outbound);
        Assert.Equal(3, t.Movements);
        Assert.Equal(3, t.Covered);
    }

    /// <summary>Il sorvolo non è traffico DEL campo: sta a parte e non entra nei movimenti (§15.2).</summary>
    [Fact]
    public void Il_sorvolo_non_e_un_movimento_del_campo()
    {
        var t = AirportCoverage.Tally(
            new[]
            {
                new SourceAirportMovement(AirportMovementKind.Overflight, "OVF", 1, 7, "LFPG", "LGAV", "B738", H(9), H(9.4)),
                Arrivo("A", 9, 9.5),
            },
            Aperto(9, 11), H(0), H(24));

        Assert.Equal(1, t.Overflight);
        Assert.Equal(1, t.Movements);
        Assert.Equal(1, t.Covered);
    }

    /// <summary>
    /// ⚠️ Senza istante il movimento sparisce dal conto: metterlo fra gli scoperti gonfierebbe la parte
    /// mancante con righe di cui non sappiamo niente — e la percentuale scoperta è proprio il numero che si
    /// va a leggere.
    /// </summary>
    [Fact]
    public void Un_movimento_senza_istante_non_si_conta_affatto()
    {
        var muto = new SourceAirportMovement(AirportMovementKind.Inbound, "MUTO", 1, 5, "LIML", "LIRF", "A320");

        var t = AirportCoverage.Tally(new[] { muto, Arrivo("A", 9, 9.5) }, Aperto(9, 11), H(0), H(24));

        Assert.Equal(1, t.Movements);
        Assert.Equal(1, t.Covered);
    }

    /// <summary>La sorgente restituisce anche voli appena fuori dalla finestra chiesta: si ritagliano.</summary>
    [Fact]
    public void Quel_che_cade_fuori_dalla_finestra_resta_fuori()
    {
        var t = AirportCoverage.Tally(
            new[] { Arrivo("DENTRO", 9, 9.5), Arrivo("PRIMA", -3, -2.5), Arrivo("DOPO", 25, 25.5) },
            Aperto(9, 11), H(0), H(24));

        Assert.Equal(1, t.Movements);
    }

    /// <summary>Una riconnessione ripresenta lo stesso volo: l'identità è il piano di volo, dove c'è.</summary>
    [Fact]
    public void Lo_stesso_volo_due_volte_conta_una()
    {
        var t = AirportCoverage.Tally(
            new[] { Arrivo("AZA1", 9, 9.5, fp: 900), Arrivo("AZA1", 9.2, 9.6, fp: 900) },
            Aperto(9, 11), H(0), H(24));

        Assert.Equal(1, t.Movements);
    }

    /// <summary>
    /// ⚠️ Un LIRF→LIRF è una partenza <b>e</b> un arrivo dello stesso campo: due movimenti, non uno.
    /// Il verso fa parte dell'identità, o il circuito sparirebbe a metà.
    /// </summary>
    [Fact]
    public void Un_circuito_conta_come_partenza_e_come_arrivo()
    {
        var t = AirportCoverage.Tally(
            new[]
            {
                new SourceAirportMovement(AirportMovementKind.Outbound, "IVAO1", 1, 900, "LIRF", "LIRF", "C172", H(9), H(10)),
                new SourceAirportMovement(AirportMovementKind.Inbound, "IVAO1", 1, 900, "LIRF", "LIRF", "C172", H(9), H(10)),
            },
            Aperto(9, 11), H(0), H(24));

        Assert.Equal(1, t.Inbound);
        Assert.Equal(1, t.Outbound);
        Assert.Equal(2, t.Movements);
    }

    [Fact]
    public void Senza_nessuna_apertura_niente_e_coperto()
    {
        var t = AirportCoverage.Tally(
            new[] { Arrivo("A", 9, 9.5), Partenza("B", 10, 12) },
            Array.Empty<OnlineSpan>(), H(0), H(24));

        Assert.Equal(2, t.Movements);
        Assert.Equal(0, t.Covered);
    }

    /// <summary>Il bordo: l'apertura è inclusiva all'inizio ed esclusiva alla fine, come ogni intervallo qui.</summary>
    [Fact]
    public void I_bordi_dell_apertura_si_comportano_come_ovunque()
    {
        var spans = Aperto(9, 11);

        Assert.Equal(1, AirportCoverage.Tally(new[] { Arrivo("A", 8, 9) }, spans, H(0), H(24)).Covered);
        Assert.Equal(0, AirportCoverage.Tally(new[] { Arrivo("B", 8, 11) }, spans, H(0), H(24)).Covered);
    }
}
