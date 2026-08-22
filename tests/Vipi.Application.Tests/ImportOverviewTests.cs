using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// L'elenco della pagina Sorgenti: policy e stato degli import nella <b>stessa</b> riga.
///
/// <para>Presidia le tre bugie che la pagina raccontava fino al 22 agosto 2026: il verde regalato alle
/// categorie escluse (<c>GatedImportLoop</c> marca il successo anche quando il run esce subito senza fare
/// niente), la riga <c>SpecialAreaForeignOptOut</c> in un elenco di import — che import non è — e la
/// mancanza della riga dell'anagrafica ACC, che invece si importa ogni giorno.</para>
/// </summary>
public class ImportOverviewTests
{
    private static ImportOverviewService Servizio(ImportPolicySnapshot policy, params ImportState[] stati) =>
        new(new PolicyFinta(policy), new StatiFinti(stati), new CadenzaFinta());

    [Fact]
    public async Task Ci_sono_sei_righe_e_l_anagrafica_ACC_e_la_prima()
    {
        var righe = await Servizio(ImportPolicySnapshot.AllImported).ListAsync();

        Assert.Equal(6, righe.Count);
        Assert.Null(righe[0].Categoria);                            // l'anagrafica non ha una spunta
        Assert.Equal(ImportCategories.Acc, righe[0].StateKey);
        Assert.True(righe[0].DaSorgente);
        Assert.Equal(new[]
        {
            ImportCategory.TransitionAltitude, ImportCategory.Runways, ImportCategory.Sectors,
            ImportCategory.Sids, ImportCategory.SpecialAreas,
        }, righe.Skip(1).Select(r => r.Categoria!.Value));
    }

    /// <summary>⚠️ Il segnaposto della riconciliazione delle aree estere non è un import e non si mostra.</summary>
    [Fact]
    public async Task Il_segnaposto_delle_aree_estere_non_e_una_riga()
    {
        var righe = await Servizio(ImportPolicySnapshot.AllImported,
            Stato(ImportCategories.SpecialAreaForeignOptOut, DateTime.UtcNow)).ListAsync();

        Assert.DoesNotContain(righe, r => r.StateKey == ImportCategories.SpecialAreaForeignOptOut);
    }

    /// <summary>
    /// ⚠️ Il cuore del giro: la categoria esclusa ha una riga di stato «riuscita» e recente, perché il loop
    /// marca il successo anche quando il run non ha importato niente per scelta. La policy vince.
    /// </summary>
    [Fact]
    public async Task Una_categoria_esclusa_non_e_verde_anche_se_lo_stato_dice_di_si()
    {
        var righe = await Servizio(new ImportPolicySnapshot(true, true, true, true, SpecialAreas: false),
            Stato(ImportCategories.SpecialArea, DateTime.UtcNow)).ListAsync();

        var aree = righe.Single(r => r.Categoria == ImportCategory.SpecialAreas);
        Assert.False(aree.DaSorgente);
        Assert.Equal(ImportHealth.Esclusa, aree.Stato);
    }

    /// <summary>
    /// Dal 22 agosto 2026 TA e Piste hanno il loro giro (<c>AirportDataImportUseCase</c>): la riga deve
    /// raccontarlo, cadenza e prossimo compresi. Prima questo stesso test asseriva il contrario — la
    /// pagina diceva «su richiesta», che era vero e non diceva quanto fosse vecchio il dato.
    /// </summary>
    [Theory]
    [InlineData(ImportCategory.TransitionAltitude)]
    [InlineData(ImportCategory.Runways)]
    public async Task TA_e_Piste_dichiarano_il_loro_giro(ImportCategory categoria)
    {
        var quando = DateTime.UtcNow.AddHours(-3);
        var riga = (await Servizio(ImportPolicySnapshot.AllImported, Stato(ImportCategories.AirportData, quando)).ListAsync())
            .Single(r => r.Categoria == categoria);

        Assert.Equal(ImportHealth.Aggiornata, riga.Stato);
        Assert.Equal(TimeSpan.FromHours(24), riga.Cadenza);
        Assert.Equal(quando.AddHours(24), riga.ProssimoUtc);
    }

    /// <summary>
    /// ⚠️ L'invariante della chiave condivisa: TA e Piste leggono la <b>stessa</b> riga di stato, ma la
    /// policy resta per categoria. Escludere le Piste non deve spegnere il racconto della TA — se un giorno
    /// il gate scivolasse dal merge al loop, è questo test a cadere.
    /// </summary>
    [Fact]
    public async Task Con_una_chiave_sola_la_categoria_esclusa_resta_l_unica_esclusa()
    {
        var righe = await Servizio(new ImportPolicySnapshot(TransitionAltitude: true, Runways: false, true, true, true),
            Stato(ImportCategories.AirportData, DateTime.UtcNow.AddHours(-3))).ListAsync();

        Assert.Equal(ImportHealth.Esclusa, righe.Single(r => r.Categoria == ImportCategory.Runways).Stato);
        Assert.Equal(ImportHealth.Aggiornata, righe.Single(r => r.Categoria == ImportCategory.TransitionAltitude).Stato);
    }

    [Fact]
    public async Task Un_giro_riuscito_dice_quando_e_atteso_il_prossimo()
    {
        var quando = DateTime.UtcNow.AddHours(-2);
        var riga = (await Servizio(ImportPolicySnapshot.AllImported, Stato(ImportCategories.Sid, quando)).ListAsync())
            .Single(r => r.Categoria == ImportCategory.Sids);

        Assert.Equal(ImportHealth.Aggiornata, riga.Stato);
        Assert.Equal(quando.AddHours(24), riga.ProssimoUtc);
    }

    /// <summary>Due periodi, non uno: alla scadenza il giro sta partendo, al secondo periodo è saltato.</summary>
    [Theory]
    [InlineData(30, ImportHealth.Aggiornata)]
    [InlineData(60, ImportHealth.Ferma)]
    public async Task Un_giro_fermo_si_vede_dopo_due_periodi(int oreFa, ImportHealth atteso)
    {
        var riga = (await Servizio(ImportPolicySnapshot.AllImported,
                Stato(ImportCategories.AirportSector, DateTime.UtcNow.AddHours(-oreFa))).ListAsync())
            .Single(r => r.Categoria == ImportCategory.Sectors);

        Assert.Equal(atteso, riga.Stato);
    }

    [Fact]
    public async Task L_errore_batte_l_ultimo_successo()
    {
        var stato = Stato(ImportCategories.Acc, DateTime.UtcNow.AddHours(-1));
        stato.LastError = "404 su /v2/centers";

        var riga = (await Servizio(ImportPolicySnapshot.AllImported, stato).ListAsync())[0];

        Assert.Equal(ImportHealth.InErrore, riga.Stato);
        Assert.Equal("404 su /v2/centers", riga.UltimoErrore);
    }

    /// <summary>
    /// ⚠️ L'errore batte «su richiesta». Un errore in archivio significa che quel giro c'era e ha fallito:
    /// se poi la sorgente viene sconfigurata (cadenza <c>null</c>), la pill diceva «su richiesta» mentre la
    /// riga sotto mostrava il messaggio dell'errore — due frasi accanto che si smentivano.
    /// </summary>
    [Fact]
    public async Task Un_errore_batte_anche_la_mancanza_di_cadenza()
    {
        var stato = Stato(ImportCategories.Sid, DateTime.UtcNow.AddHours(-1));
        stato.LastError = "sectorfile non raggiungibile";

        var riga = (await new ImportOverviewService(new PolicyFinta(ImportPolicySnapshot.AllImported),
                new StatiFinti(new[] { stato }), new SenzaCadenza()).ListAsync())
            .Single(r => r.Categoria == ImportCategory.Sids);

        Assert.Equal(ImportHealth.InErrore, riga.Stato);
    }

    [Fact]
    public async Task Senza_nessuno_stato_le_categorie_periodiche_dicono_mai_eseguita()
    {
        var righe = await Servizio(ImportPolicySnapshot.AllImported).ListAsync();

        Assert.Equal(ImportHealth.MaiEseguita, righe[0].Stato);
        Assert.All(righe.Where(r => r.Cadenza is not null), r => Assert.Equal(ImportHealth.MaiEseguita, r.Stato));
    }

    private static ImportState Stato(string categoria, DateTime successo) =>
        new() { Category = categoria, LastSuccessUtc = successo, LastAttemptUtc = successo };

    private sealed class PolicyFinta : IImportPolicyStore
    {
        private readonly ImportPolicySnapshot _p;
        public PolicyFinta(ImportPolicySnapshot p) => _p = p;
        public Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default) => Task.FromResult(_p);
        public Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default) =>
            Task.FromResult(new ImportPolicyInfo(_p, null, 0));
        public Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class StatiFinti : IImportStateStore
    {
        private readonly ImportState[] _stati;
        public StatiFinti(ImportState[] stati) => _stati = stati;
        public Task<IReadOnlyList<ImportState>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ImportState>>(_stati);
        public Task<DateTime?> GetLastSuccessAsync(string category, CancellationToken ct = default) =>
            Task.FromResult<DateTime?>(null);
        public Task MarkSuccessAsync(string category, DateTime utc, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkFailureAsync(string category, DateTime utc, string error, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    /// <summary>Nessun giro automatico da nessuna parte: è il caso della sorgente sconfigurata.</summary>
    private sealed class SenzaCadenza : IImportSchedule
    {
        public TimeSpan? PeriodOf(string category) => null;
    }

    /// <summary>Le cadenze di serie (24h ovunque), così le asserzioni parlano di soglie e non di config.</summary>
    private sealed class CadenzaFinta : IImportSchedule
    {
        public TimeSpan? PeriodOf(string category) => category switch
        {
            ImportCategories.Acc or ImportCategories.AirportSector or ImportCategories.SpecialArea
                or ImportCategories.Sid or ImportCategories.AirportData => TimeSpan.FromHours(24),
            _ => null,
        };
    }
}
