using Microsoft.Extensions.Localization;
using Vipi.Application.Diagnostics;
using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il contenuto del report si legge nella lingua dell'interfaccia.
///
/// <para>Fino al 22 agosto 2026 in pagina inglese le intestazioni erano tradotte e le righe no:
/// «SEVERE | Gerarchia dangling | ParentCallsign «LIRR_XX_CTR» non esiste nei cataloghi». Era l'unica pagina
/// admin il cui contenuto è prosa scritta dall'applicazione, non dati.</para>
///
/// <para>⚠️ Il rischio della cura è perdere il testo di un rilievo per una chiave sbagliata. Da qui il patto
/// — chiave sconosciuta ⇒ testo grezzo — e il test che percorre <b>tutte</b> le categorie prodotte.</para>
/// </summary>
public class ConsistencyNarratorTests
{
    /// <summary>Rende la chiave e vi appende gli argomenti: le asserzioni parlano di chiavi, non di
    /// traduzioni — che cambiano. <c>ResourceNotFound</c> resta <c>false</c>: qui la chiave c'è sempre.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + "(" + string.Join("|", arguments) + ")", resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Il localizzatore che NON trova niente: è così che si comporta davanti a una chiave sbagliata.</summary>
    private sealed class MissingLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: true);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: true);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private static readonly KeyLocalizer L = new();

    /// <summary>
    /// ⚠️ Il cuore della rete: <b>ogni</b> rilievo che i produttori sanno generare deve portare le sue chiavi.
    /// Un controllo nuovo che se le dimentica esce in italiano anche in inglese, e nessun'altra asserzione lo
    /// vedrebbe.
    /// </summary>
    [Fact]
    public void Ogni_rilievo_prodotto_porta_le_sue_chiavi()
    {
        // ⚠️ Prima di tutto: che le famiglie ci siano DAVVERO tutte. Senza questa riga il test passerebbe
        // identico anche se il campionario smettesse di produrne metà, e non se ne accorgerebbe nessuno.
        // Tredici: sei soft-ref, una per le impostazioni del server (i suoi quattro rami dicono la stessa
        // famiglia), tre di schema, l'avvio, la sonda rotta e «nessun amministratore».
        var famiglie = TuttiIRilievi().Select(f => f.Category).Distinct().ToList();
        Assert.Equal(13, famiglie.Count);

        foreach (var f in TuttiIRilievi())
        {
            Assert.False(string.IsNullOrWhiteSpace(f.CategoryKey), $"«{f.Category}» non ha CategoryKey.");
            Assert.False(string.IsNullOrWhiteSpace(f.DetailKey), $"«{f.Category}» non ha DetailKey.");
            // La chiave si vede tradotta, e gli argomenti arrivano tutti al testo.
            Assert.Equal(f.CategoryKey, ConsistencyNarrator.Categoria(f, L));
            Assert.StartsWith(f.DetailKey!, ConsistencyNarrator.Dettaglio(f, L));
        }
    }

    /// <summary>Le chiavi sono tante e devono essere distinte: due categorie con la stessa chiave direbbero
    /// la stessa cosa di due problemi diversi (⚠️ due chiavi resx con la stessa traduzione sfuggono al test
    /// che confronta i valori: qui si confrontano le CHIAVI).</summary>
    [Fact]
    public void Categorie_diverse_hanno_chiavi_diverse()
    {
        var coppie = TuttiIRilievi()
            .Select(f => (f.Category, f.CategoryKey))
            .Distinct()
            .ToList();

        Assert.Equal(coppie.Select(c => c.Category).Distinct().Count(),
                     coppie.Select(c => c.CategoryKey).Distinct().Count());
    }

    /// <summary>
    /// ⚠️ Chiave sconosciuta ⇒ <b>testo grezzo</b>, mai il nome della chiave a video. Il localizzatore, quando
    /// non trova, restituisce la chiave <i>come valore</i>: senza il controllo su <c>ResourceNotFound</c> a
    /// schermo comparirebbe «Diag_Msg_Qualcosa», che è peggio dell'italiano in pagina inglese.
    /// </summary>
    [Fact]
    public void Una_chiave_che_non_esiste_ripiega_sul_testo_grezzo()
    {
        var f = new ConsistencyFinding("Pista orfana", ConsistencySeverity.Error, "Clausola #1",
            "ConditionRefId=99001 non corrisponde a nessuna pista.", ConsistencyArea.Dati,
            CategoryKey: "Diag_Cat_MaiVista", DetailKey: "Diag_Msg_MaiVisto");

        var mancante = new MissingLocalizer();
        Assert.Equal("Pista orfana", ConsistencyNarrator.Categoria(f, mancante));
        Assert.Equal(f.Detail, ConsistencyNarrator.Dettaglio(f, mancante));
    }

    /// <summary>
    /// ⚠️ Anche il <b>bersaglio</b> va tradotto quando è una frase. La prima stesura di questo giro aveva
    /// tradotto categoria e dettaglio e lasciato indietro l'entità: in pagina inglese si leggeva
    /// «severe | Broken hierarchy | <b>Settore ACC</b> LGGG_W_CTR». Una cura a metà si vede.
    /// </summary>
    [Fact]
    public void Anche_il_bersaglio_si_traduce_quando_e_una_frase()
    {
        var conFrase = TuttiIRilievi().Where(f => f.EntityKey is not null).ToList();
        Assert.NotEmpty(conFrase);
        Assert.All(conFrase, f => Assert.StartsWith(f.EntityKey!, ConsistencyNarrator.Bersaglio(f, L)));

        // ⚠️ E chi NON è una frase resta com'è: «sql_mode» e «Documents.Title» sono identificatori, non
        // prosa, e tradurli sarebbe inventare un secondo nome per la stessa cosa.
        var identificatori = TuttiIRilievi().Where(f => f.EntityKey is null).ToList();
        Assert.NotEmpty(identificatori);
        Assert.All(identificatori, f => Assert.Equal(f.Entity, ConsistencyNarrator.Bersaglio(f, L)));
    }

    /// <summary>Un rilievo senza chiavi (venuto da codice più vecchio) si legge lo stesso.</summary>
    [Fact]
    public void Un_rilievo_senza_chiavi_mostra_il_testo_grezzo()
    {
        var f = new ConsistencyFinding("Cosa nuova", ConsistencySeverity.Warning, "X", "Y", ConsistencyArea.Dati);

        Assert.Equal("Cosa nuova", ConsistencyNarrator.Categoria(f, L));
        Assert.Equal("Y", ConsistencyNarrator.Dettaglio(f, L));
    }

    [Theory]
    [InlineData(ConsistencyArea.Dati)]
    [InlineData(ConsistencyArea.Schema)]
    [InlineData(ConsistencyArea.Server)]
    [InlineData(ConsistencyArea.Avvio)]
    [InlineData(ConsistencyArea.Configurazione)]
    public void Ogni_area_ha_la_sua_etichetta(ConsistencyArea area) =>
        Assert.Equal("Diag_Area_" + area, ConsistencyNarrator.Area(area, L));

    /// <summary>
    /// Un rilievo per ogni famiglia che i produttori sanno generare. ⚠️ Va tenuto in pari: quando nasce un
    /// controllo nuovo, aggiungere il caso qui è il modo in cui i test se ne accorgono.
    /// </summary>
    private static IEnumerable<ConsistencyFinding> TuttiIRilievi()
    {
        // Le sei famiglie dei soft-ref, tutte in un dataset solo.
        var d = new ConsistencyDataset
        {
            TransferConditions = new[]
            {
                new TransferConditionRow(1, "LIRR", "VALMA", 99001, null, "LI R99Z"),
                new TransferConditionRow(2, "LIRR", "EKMUR", 10, "Pista 34R", null),
            },
            RunwayIdents = new Dictionary<int, string> { [10] = "16R" },
            // ⚠️ La chiave del tipo di nodo la dichiara il REPOSITORY: qui va passata come in produzione,
            // altrimenti il test proverebbe un caso che nell'app non esiste.
            ParentRefs = new[] { new ParentRefRow("Settore APT", "LIRF_TWR", "LIXX_APP", "Diag_Ent_SettoreApt") },
            ValidCallsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIRF_TWR", "LIRF_TWR_X" },
            RegulatedRefs = new[] { new RegulatedRefRow("vIPI", "Roma ACC", """{"OwnIds":["999"],"ExtraIds":[]}""") },
        };
        foreach (var f in ConsistencyReportService.Analyze(d)) yield return f;

        // I quattro rami delle impostazioni del server: quattro rilievi, UNA famiglia.
        foreach (var f in ServerSettingsAnalyzer.Analyze(null, 8_388_608L)) yield return f;
        foreach (var f in ServerSettingsAnalyzer.Analyze("NO_STRICT", null)) yield return f;
        foreach (var f in ServerSettingsAnalyzer.Analyze("NO_STRICT", 1024L)) yield return f;

        // I tre del drift di schema.
        foreach (var f in SchemaDriftAnalyzer.Compare(
                     model: new[] { new SchemaColumn("D", "Titolo", "TEXT"), new SchemaColumn("D", "N", "INTEGER") },
                     actual: new[] { new SchemaColumn("D", "Title", "TEXT"), new SchemaColumn("D", "N", "TEXT") }))
            yield return f;

        // Il guasto di una passata d'avvio.
        var avvio = new StartupMaintenanceReport();
        avvio.Record("proiezione dei settori dai cataloghi", new InvalidOperationException("boom"));
        foreach (var f in avvio.Findings) yield return f;

        // ⚠️ Le due famiglie che nascono da un servizio e non da un analizzatore puro. Vanno percorse lo
        // stesso: sono le uniche che una copertura fatta di sole funzioni pure lascerebbe fuori, e una di
        // loro — «nessuno può editare» — è il rilievo più grave che l'applicazione sappia produrre.
        var conSondaRotta = new ConsistencyReportService(new RepoVuoto(), schema: new SondaRotta());
        foreach (var f in conSondaRotta.RunAsync().GetAwaiter().GetResult()) yield return f;

        var senzaAdmin = new Vipi.Application.Auth.AdminCoverageService(
            new RosterSenzaAdmin(),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.Auth.AuthOptions()),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.DivisionOptions()));
        foreach (var f in senzaAdmin.RunAsync().GetAwaiter().GetResult()) yield return f;
    }

    private sealed class RepoVuoto : Vipi.Application.Abstractions.IConsistencyReportRepository
    {
        public Task<ConsistencyDataset> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(new ConsistencyDataset());
    }

    private sealed class SondaRotta : ISchemaDriftProbe
    {
        public Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("connessione caduta");
    }

    /// <summary>
    /// Uno staffista nel roster, con un codice che nessun pattern admin riconosce. ⚠️ Dal 22 agosto 2026
    /// vale admin qualunque <c>IT-{ruolo}</c>, quindi per restare fuori il codice dev'essere <b>malformato</b>
    /// (qui: senza trattino) — un <c>IT-QUALCOSA</c> oggi sarebbe admin.
    /// </summary>
    private sealed class RosterSenzaAdmin : Vipi.Application.Abstractions.IStaffRosterRepository
    {
        public Task<IReadOnlyList<Vipi.Application.Abstractions.StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vipi.Application.Abstractions.StaffRosterEntry>>(new[]
            {
                new Vipi.Application.Abstractions.StaffRosterEntry(704798, "Tizio", "C3",
                    new[] { "ITQUALCOSA" }, DateTime.UtcNow),
            });

        public Task UpsertLoginAsync(int userId, string? displayName, IReadOnlyList<string> positions, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task UpdateVerifiedAsync(int userId, string? displayName, string? atcRating, IReadOnlyList<string> positions, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
    }
}
