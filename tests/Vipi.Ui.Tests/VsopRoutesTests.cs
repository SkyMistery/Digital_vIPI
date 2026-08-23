using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La lettura del codice ACC dall'indirizzo (<see cref="VsopRoutes.AccFrom"/>).
///
/// <para>Presidia il difetto del 22 agosto 2026: il prefisso è passato da <c>/vsop</c> a
/// <c>/services/vsop</c> e il layout ha continuato a contare i segmenti come prima, guardando il primo invece
/// del terzo. Da allora nessun ACC è più stato evidenziato in barra. Qui il conto sta in un posto solo e ha
/// una rete sotto.</para>
/// </summary>
public class VsopRoutesTests
{
    [Theory]
    [InlineData("/services/vsop/lirr", "lirr")]
    [InlineData("/services/vsop/lirr/vipi", "lirr")]
    [InlineData("/services/vsop/lirr/airports/editor", "lirr")]
    [InlineData("/services/vsop/LIMM", "LIMM")]        // maiuscole: il confronto lo fa chi chiama
    [InlineData("/services/vsop/lirr/", "lirr")]        // barra finale
    public void Legge_il_segmento_dopo_il_prefisso(string percorso, string atteso) =>
        Assert.Equal(atteso, VsopRoutes.AccFrom(percorso));

    [Theory]
    [InlineData("/services/vsop")]                      // il prefisso nudo: non c'è nulla dopo
    [InlineData("/services/vsop/")]
    [InlineData("/services")]
    [InlineData("/")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("/vsop/lirr")]                          // il prefisso di IERI: qui non si risponde, si redirige (LegacyRoutes)
    [InlineData("/services/profile-swapper")]           // un altro servizio dell'hub
    public void Fuori_dal_prefisso_non_risponde(string? percorso) =>
        Assert.Null(VsopRoutes.AccFrom(percorso));

    /// <summary>
    /// ⚠️ Il segmento torna GREZZO: <c>admin</c> non è un ACC, ma non è questo il posto in cui si decide.
    /// Chi chiama confronta con l'elenco degli ACC veri, e tenere qui un secondo elenco di parole riservate
    /// vorrebbe dire ripetere la promessa che si è già rotta una volta.
    /// </summary>
    [Fact]
    public void Non_filtra_i_segmenti_riservati() =>
        Assert.Equal("admin", VsopRoutes.AccFrom("/services/vsop/admin/airports"));

    /// <summary>Il prefisso è quello, e i test lo dicono: se cambia, cambia deliberatamente.</summary>
    [Fact]
    public void Il_prefisso_e_quello_dichiarato() =>
        Assert.Equal("/services/vsop", VsopRoutes.Prefix);
}
