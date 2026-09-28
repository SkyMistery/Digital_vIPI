using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// U-184 (revisione 3, S37): «Salva» nella pagina Sorgenti scriveva l'intera policy letta all'apertura, e
/// riportava indietro in silenzio la categoria che un altro amministratore aveva cambiato nel frattempo.
/// </summary>
public class PolicySorgentiSalvaSoloLeToccateTests
{
    private const int Vid = 704798;

    private static (ImportPolicyService Servizio, StoreFinto Store) Costruisci(ImportPolicySnapshot iniziale)
    {
        var auth = new AuthOptions { FounderVids = { Vid } };
        var authz = new EditAuthorizationService(new UtenteFinto(new CurrentUser(Vid, "Admin", null, Array.Empty<string>())),
            new RoleResolver(auth, new DivisionOptions()), SenzaPromozioni.Instance);
        var store = new StoreFinto(iniziale);
        return (new ImportPolicyService(store, authz), store);
    }

    [Fact]
    public async Task La_categoria_cambiata_da_un_altro_nel_frattempo_resta_come_l_ha_messa()
    {
        var (servizio, store) = Costruisci(ImportPolicySnapshot.AllImported);
        var letta = await servizio.GetAsync();                      // A apre la pagina

        store.Corrente = store.Corrente with { Sids = false };      // B, in un'altra scheda, rende manuali le SID

        var tenute = await servizio.SaveChangesAsync(letta, letta with { Runways = false });   // A: piste manuali

        Assert.False(store.Corrente.Runways);
        Assert.False(store.Corrente.Sids);                          // prima tornava «da sorgente»
        Assert.Equal(new[] { ImportCategory.Sids }, tenute);
    }

    [Fact]
    public async Task Senza_altri_nel_mezzo_non_si_segnala_niente()
    {
        var (servizio, store) = Costruisci(ImportPolicySnapshot.AllImported);
        var letta = await servizio.GetAsync();

        var tenute = await servizio.SaveChangesAsync(letta, letta with { Navaids = false, Sectors = false });

        Assert.Empty(tenute);
        Assert.Equal(ImportPolicySnapshot.AllImported with { Navaids = false, Sectors = false }, store.Corrente);
    }

    private sealed class StoreFinto : IImportPolicyStore
    {
        public ImportPolicySnapshot Corrente;
        public StoreFinto(ImportPolicySnapshot p) => Corrente = p;
        public Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default) => Task.FromResult(Corrente);
        public Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default) =>
            Task.FromResult(new ImportPolicyInfo(Corrente, null, 0));
        public Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default)
        {
            Corrente = policy;
            return Task.CompletedTask;
        }
    }

    private sealed class UtenteFinto : ICurrentUserProvider
    {
        private readonly CurrentUser? _u;
        public UtenteFinto(CurrentUser? u) => _u = u;
        public CurrentUser? Get() => _u;
    }
}
