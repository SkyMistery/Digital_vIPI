using Vipi.Application.Abstractions;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il catalogo dei punti: che cosa promette a chi lo consuma. Le due forme (elenco ordinato e insieme dei nomi)
/// devono raccontare la stessa cosa — sono la ragione per cui il catalogo è UN oggetto e non due caricamenti.
/// </summary>
public class NavaidCatalogTests
{
    [Fact]
    public void Le_due_forme_contengono_gli_stessi_nomi()
    {
        var c = new NavaidCatalog(new[]
        {
            new NavaidName("OST", NavaidKind.Vor),
            new NavaidName("ALAXI", NavaidKind.Fix),
        });

        Assert.Equal(new[] { "ALAXI", "OST" }, c.Entries.Select(e => e.Name));   // ordine alfabetico
        Assert.Equal(c.Entries.Count, c.Names.Count);
        Assert.All(c.Entries, e => Assert.Contains(e.Name, c.Names));
    }

    [Fact]
    public void I_nomi_si_confrontano_senza_distinzione_di_maiuscole()
    {
        var c = new NavaidCatalog(new[] { new NavaidName("ALAXI", NavaidKind.Fix) });
        Assert.Contains("alaxi", c.Names);
    }

    [Fact]
    public void Un_omonimo_prende_la_natura_della_PRIMA_occorrenza()
    {
        // È il contratto su cui si regge l'ordine di accodamento del parser (VOR e NDB prima dei fix): su un
        // nome presente in due file vince la radioassistenza, che è l'informazione più specifica.
        var c = new NavaidCatalog(new[]
        {
            new NavaidName("ELB", NavaidKind.Vor),
            new NavaidName("ELB", NavaidKind.Fix),
        });

        Assert.Equal(NavaidKind.Vor, Assert.Single(c.Entries).Kind);
    }

    [Fact]
    public void Nomi_vuoti_o_di_soli_spazi_non_entrano()
    {
        // Le righe vuote in coda ai file del sectorfile sono la norma: un nome vuoto nel catalogo renderebbe
        // "valido" ogni campo lasciato in bianco.
        var c = new NavaidCatalog(new[]
        {
            new NavaidName("", NavaidKind.Fix),
            new NavaidName("   ", NavaidKind.Fix),
            new NavaidName(" ALAXI ", NavaidKind.Fix),
        });

        Assert.Equal("ALAXI", Assert.Single(c.Entries).Name);   // e il nome arriva già ripulito
    }

    [Fact]
    public void NamesOf_separa_le_nature()
    {
        var c = new NavaidCatalog(new[]
        {
            new NavaidName("OST", NavaidKind.Vor),
            new NavaidName("ALAXI", NavaidKind.Fix),
            new NavaidName("ESINO", NavaidKind.Fix),
            new NavaidName("AVI", NavaidKind.Ndb),
        });

        Assert.Equal(new[] { "ALAXI", "ESINO" }, c.NamesOf(NavaidKind.Fix));
        Assert.Equal(new[] { "OST" }, c.NamesOf(NavaidKind.Vor));
        Assert.Equal(new[] { "AVI" }, c.NamesOf(NavaidKind.Ndb));
    }

    [Fact]
    public void Il_catalogo_vuoto_non_e_null_e_non_valida_niente()
    {
        Assert.Empty(NavaidCatalog.Empty.Entries);
        Assert.Empty(NavaidCatalog.Empty.Names);
    }
}
