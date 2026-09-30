using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Quali pagine sono «di sola lettura» per il riquadro di riconnessione (30 settembre 2026): le SSR statiche sì — i
/// documenti — le interattive no. La regola legge l'attributo che il compilatore scrive per <c>@rendermode</c>: se un
/// giorno smettesse di scriverlo, tutte le pagine diventerebbero «di lettura», editor compresi, e questo lo direbbe.
/// </summary>
public class PaginaDiLetturaTests
{
    [Theory]
    [InlineData(typeof(AeroportoPage))]
    [InlineData(typeof(AccVipiPage))]
    public void I_documenti_sono_pagine_di_lettura(Type pagina) => Assert.True(PaginaDiLettura.E(pagina));

    [Theory]
    [InlineData(typeof(RichiestePage))]
    [InlineData(typeof(StatsDivisionPage))]
    public void Le_pagine_interattive_tengono_il_riquadro(Type pagina) => Assert.False(PaginaDiLettura.E(pagina));
}
