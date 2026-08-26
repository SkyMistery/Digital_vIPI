using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il gate AIRAC delle shape: quale geometria finisce in un documento pubblicato per un dato ciclo.
/// Il servizio AIRAC è quello vero — i cicli sono aritmetica, non c'è niente da simulare.
/// </summary>
public class ShapeAiracGateTests
{
    private static readonly IAiracService Airac = new AiracService();
    private const string Vecchia = "[[11.0,44.0],[11.5,44.0],[11.5,44.5]]";
    private const string Nuova = "[[12.0,45.0],[12.5,45.0],[12.5,45.5]]";

    /// <summary>Una shape dal sectorfile, differita al ciclo indicato, con una precedente da mostrare.</summary>
    private static ShapeState Differita(string dalCiclo) =>
        new(Nuova, Vecchia, dalCiclo, ShapeSource.Sectorfile, ForcePublished: false);

    // I due cicli usati: 2609 comincia prima di 2610.
    private const string Corrente = "2609";
    private const string Prossimo = "2610";

    [Fact]
    public void Pubblicando_per_il_ciclo_corrente_esce_quella_in_vigore() =>
        Assert.Equal(Vecchia, ShapeAiracGate.ForRelease(Differita(Prossimo), Corrente, Airac));

    /// <summary>
    /// Il verso che rende il gate indolore: chi prepara l'AIRAC pubblica <b>per il ciclo prossimo</b>, e lì la
    /// geometria nuova è quella giusta. Nessun interruttore da ricordare.
    /// </summary>
    [Fact]
    public void Pubblicando_in_anticipo_per_il_ciclo_prossimo_esce_quella_nuova() =>
        Assert.Equal(Nuova, ShapeAiracGate.ForRelease(Differita(Prossimo), Prossimo, Airac));

    [Fact]
    public void Una_shape_senza_differimento_esce_sempre() =>
        Assert.Equal(Nuova, ShapeAiracGate.ForRelease(
            new ShapeState(Nuova, Vecchia, null, ShapeSource.Sectorfile, false), Corrente, Airac));

    /// <summary>⚠️ Il gate vale solo per il sectorfile: l'anagrafica IVAO è in vigore per definizione.</summary>
    [Fact]
    public void Quel_che_viene_dall_anagrafica_non_si_differisce() =>
        Assert.Equal(Nuova, ShapeAiracGate.ForRelease(
            new ShapeState(Nuova, Vecchia, Prossimo, ShapeSource.Source, false), Corrente, Airac));

    /// <summary>⚠️ Il caso nominato per primo dal committente: la correzione di un errore va pubblicata subito.</summary>
    [Fact]
    public void La_forzatura_scavalca_il_differimento() =>
        Assert.Equal(Nuova, ShapeAiracGate.ForRelease(
            Differita(Prossimo) with { ForcePublished = true }, Corrente, Airac));

    /// <summary>
    /// ⚠️ La regola che evita il danno peggiore: senza una precedente da mostrare, differire vorrebbe dire
    /// <b>nessuna area</b> fino a 28 giorni. Una in anticipo è meno peggio.
    /// </summary>
    [Fact]
    public void La_prima_shape_di_un_settore_non_si_differisce_mai() =>
        Assert.Equal(Nuova, ShapeAiracGate.ForRelease(
            new ShapeState(Nuova, InForce: null, Prossimo, ShapeSource.Sectorfile, false), Corrente, Airac));

    [Fact]
    public void Un_ciclo_illeggibile_non_nasconde_niente() =>
        Assert.Equal(Nuova, ShapeAiracGate.ForRelease(Differita("boh"), Corrente, Airac));

    /// <summary>⚠️ Il confronto è sulle DATE: "2701" viene dopo "2613", ma non in ordine alfabetico.</summary>
    [Fact]
    public void Il_confronto_fra_cicli_non_e_alfabetico()
    {
        Assert.True(ShapeAiracGate.IsDeferredAt(Differita("2701"), "2613", Airac));
        Assert.False(ShapeAiracGate.IsDeferredAt(Differita("2613"), "2701", Airac));
    }

    // ---- la promozione ---------------------------------------------------------------------------------

    [Fact]
    public void Prima_del_ciclo_non_si_promuove() =>
        Assert.False(ShapeAiracGate.IsPromotable(
            Differita(Prossimo), Airac.EffectiveUtcForCycle(Prossimo).AddDays(-1), Airac));

    [Fact]
    public void Dal_giorno_del_ciclo_si_promuove() =>
        Assert.True(ShapeAiracGate.IsPromotable(
            Differita(Prossimo), Airac.EffectiveUtcForCycle(Prossimo), Airac));

    [Fact]
    public void Chi_non_e_differita_non_ha_niente_da_promuovere() =>
        Assert.False(ShapeAiracGate.IsPromotable(
            new ShapeState(Nuova, null, null, ShapeSource.Sectorfile, false), DateTime.UtcNow, Airac));

    /// <summary>Un ciclo illeggibile si chiude invece di restare appeso per sempre.</summary>
    [Fact]
    public void Un_differimento_con_ciclo_illeggibile_si_chiude() =>
        Assert.True(ShapeAiracGate.IsPromotable(Differita("boh"), DateTime.UtcNow, Airac));
}
