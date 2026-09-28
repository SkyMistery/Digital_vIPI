using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Domain.Tests;

/// <summary>
/// La tabella delle quattro categorie (carta <c>docs/feature/2026-09-11-categorie-aeroporto.md</c>), scritta
/// come la chiede il committente: quale documento ammette ciascuna. È la regola che sei punti dell'applicazione
/// chiedono, e che per questo sta in un posto solo.
/// </summary>
public class AirportCategoriesTests
{
    [Theory]
    [InlineData(AirportCategory.Civil, true, false)]
    [InlineData(AirportCategory.MilitaryOnly, false, true)]
    [InlineData(AirportCategory.CivilWithMilitaryPresence, true, false)]
    [InlineData(AirportCategory.MilitaryWithCivilPresence, true, true)]
    public void Ogni_categoria_ammette_i_suoi_documenti(AirportCategory c, bool vipi, bool vsop)
    {
        Assert.Equal(vipi, c.AllowsCivil());
        Assert.Equal(vsop, c.AllowsMilitary());
    }

    /// <summary>⚠️ Ogni valore dell'enum deve stare nella tabella qui sopra: una categoria nuova senza riga
    /// passerebbe dalle regole col comportamento di un'altra, in silenzio.</summary>
    [Fact]
    public void La_tabella_copre_tutte_le_categorie()
    {
        Assert.Equal(4, Enum.GetValues<AirportCategory>().Length);
    }

    /// <summary>Le scelte di un amministratore sono tre: «Civile» lo dice la sorgente.</summary>
    [Fact]
    public void Si_scelgono_le_tre_categorie_militari_e_non_Civile()
    {
        Assert.Equal(3, AirportCategories.Selectable.Count);
        Assert.DoesNotContain(AirportCategory.Civil, AirportCategories.Selectable);
    }

    // 🔴 U-028 (revisione totale 3, decisione del committente del 27-set-2026): senza presenza una categoria militare
    // NON scende da sola a Civile. Una notte di «military» falso cancellava per sempre la scelta di una persona;
    // ora la riga resta com'è, è una DIVERGENZA, e a chiuderla è l'amministratore scegliendo «Civile».
    [Theory]
    [InlineData(false, AirportCategory.MilitaryOnly, AirportCategory.MilitaryOnly)]
    [InlineData(false, AirportCategory.Civil, AirportCategory.Civil)]
    [InlineData(true, AirportCategory.Civil, AirportCategory.CivilWithMilitaryPresence)]
    [InlineData(true, AirportCategory.MilitaryWithCivilPresence, AirportCategory.MilitaryWithCivilPresence)]
    public void L_invariante_con_la_presenza_militare(bool presenza, AirportCategory prima, AirportCategory dopo)
    {
        Assert.Equal(dopo, AirportCategories.Normalize(presenza, prima));
    }

    [Theory]
    [InlineData(false, AirportCategory.MilitaryOnly, true)]
    [InlineData(false, AirportCategory.CivilWithMilitaryPresence, true)]
    [InlineData(false, AirportCategory.Civil, false)]
    [InlineData(true, AirportCategory.MilitaryOnly, false)]
    public void La_divergenza_e_una_categoria_militare_senza_presenza(bool presenza, AirportCategory categoria, bool divergente)
    {
        Assert.Equal(divergente, AirportCategories.Divergente(presenza, categoria));
    }
}
