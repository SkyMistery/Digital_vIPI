using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il metro contro cui un timbro d'import è «vecchio»: l'ultimo giro riuscito più <b>vecchio</b> fra le due
/// famiglie, meno un giorno di margine — oppure niente, quando non si può dire.
/// </summary>
public class SogliaTimbroTests
{
    private static readonly DateTime Adesso = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void La_soglia_e_il_giro_piu_vecchio_meno_il_margine()
    {
        var aeroporti = Adesso.AddHours(-2);
        var acc = Adesso.AddHours(-9);      // slittato: è lui il metro

        Assert.Equal(acc - SogliaTimbro.Margine, SogliaTimbro.Calcola(aeroporti, acc));
        Assert.Equal(acc - SogliaTimbro.Margine, SogliaTimbro.Calcola(acc, aeroporti));
    }

    [Fact]
    public void Senza_un_giro_riuscito_per_famiglia_non_si_dice_niente()
    {
        Assert.Null(SogliaTimbro.Calcola(null, Adesso));
        Assert.Null(SogliaTimbro.Calcola(Adesso, null));
        Assert.Null(SogliaTimbro.Calcola(null, null));
    }

    /// <summary>
    /// 🔴 4 ottobre 2026: database nuovo e sorgente irraggiungibile. Il primo tentativo fallito lasciava la
    /// riga di stato con l'ultimo successo a <c>0001-01-01</c>, quella data arrivava fin qui come se fosse un
    /// giro, e «meno un giorno» non è una data: <c>ArgumentOutOfRangeException</c>, cioè 500 sulla pagina
    /// Struttura. Una data da cui il margine non si può togliere non è un giro: è «non lo sappiamo».
    /// </summary>
    [Fact]
    public void Una_data_da_cui_il_margine_non_si_toglie_non_e_un_giro()
    {
        Assert.Null(SogliaTimbro.Calcola(DateTime.MinValue, Adesso));
        Assert.Null(SogliaTimbro.Calcola(Adesso, DateTime.MinValue));
        Assert.Null(SogliaTimbro.Calcola(default(DateTime), default(DateTime)));
    }
}
