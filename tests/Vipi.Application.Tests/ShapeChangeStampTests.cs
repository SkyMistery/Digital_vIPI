using Vipi.Application.Airspace;
using Vipi.Application.Stats;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il gettone dei cambi di forma (<see cref="ShapeChangeStamp"/>) e la tratta che dice <b>con quale forma</b>
/// è stata contata. Carta <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c> §3g e §3h.
///
/// <para>⚠️ Il caso che conta è quello di mezzo: agganciare un settore <b>non</b> è un giro d'import — è una
/// persona che preme un tasto e si aspetta di vedere l'effetto. Senza il gettone, «da adesso conta il CTR e
/// non il monoblocco» entrerebbe in vigore fra zero e sessanta minuti.</para>
/// </summary>
public class ShapeChangeStampTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Appena_nato_non_ha_mai_visto_cambiare_niente()
    {
        var gettone = new ShapeChangeStamp();

        Assert.Equal(DateTimeOffset.MinValue, gettone.LastChangeUtc);
        Assert.False(gettone.IsStale(DateTimeOffset.MinValue));
    }

    [Fact]
    public void Una_cache_letta_PRIMA_del_cambio_e_vecchia_quella_letta_DOPO_no()
    {
        var gettone = new ShapeChangeStamp();
        gettone.Touch(T0);

        Assert.True(gettone.IsStale(T0.AddMinutes(-1)));    // letta prima: da rileggere
        Assert.False(gettone.IsStale(T0.AddMinutes(1)));    // letta dopo: va bene così
        Assert.False(gettone.IsStale(T0));                  // letta nello stesso istante: non è vecchia
    }

    [Fact]
    public void L_ultimo_cambio_vince_sul_precedente()
    {
        var gettone = new ShapeChangeStamp();
        gettone.Touch(T0);
        gettone.Touch(T0.AddHours(2));

        Assert.Equal(T0.AddHours(2), gettone.LastChangeUtc);
        Assert.True(gettone.IsStale(T0.AddHours(1)));
    }

    /// <summary>
    /// La tratta si porta in archivio la fonte dell'<b>ultimo</b> avvistamento: i minuti si sommano, e se un
    /// settore viene agganciato mentre un volo è dentro, la risposta utile è con che confine lo si stava
    /// contando.
    /// </summary>
    [Fact]
    public void La_tratta_scrive_la_forma_con_cui_e_stata_contata()
    {
        var registro = new TrafficLedger();

        registro.Observe(100, Osservazione(ShapeSource.Source), T0);
        registro.Observe(100, Osservazione(ShapeSource.Aip), T0.AddMinutes(1));

        var flush = registro.Take(T0.AddMinutes(1), TimeSpan.FromMinutes(10), new HashSet<long> { 100 });
        var riga = Assert.Single(flush.Legs);

        Assert.Equal(ShapeSource.Aip, riga.ShapeSource);
        Assert.Equal(2, riga.SeenMinutes);   // e i minuti sono tutti e due, non solo quelli dopo l'aggancio
    }

    /// <summary>Senza dire niente resta <c>Source</c>: è la forma dell'anagrafica, quella di sempre.</summary>
    [Fact]
    public void Senza_dire_niente_la_forma_e_quella_dell_anagrafica()
    {
        var registro = new TrafficLedger();
        registro.Observe(100, new LegObservation("AZA123", 785031, 900, "LIRF", "LIRN", "B38M",
            FlightPhase.Airborne, 24_000), T0);

        var flush = registro.Take(T0, TimeSpan.FromMinutes(10), new HashSet<long> { 100 });

        Assert.Equal(ShapeSource.Source, Assert.Single(flush.Legs).ShapeSource);
    }

    private static LegObservation Osservazione(ShapeSource fonte) =>
        new("AZA123", 785031, 900, "LIRF", "LIRN", "B38M", FlightPhase.Airborne, 24_000, fonte);
}
