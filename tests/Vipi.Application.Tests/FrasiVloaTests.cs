using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Tests;

/// <summary>
/// Le frasi di partenza di una vLOA sono <b>parola nostra</b>, e si mettono in memoria invece di comprarle.
///
/// <para>⚠️ Il caso vero che ha fatto nascere questo codice: il 30 agosto 2026 una di quelle frasi tornava
/// <b>rotta a ogni giro</b> — il motore fondeva i due segnaposti attaccati di <c>LIBB/LGGG</c> — e siccome
/// una frase rotta non si salva, il giro dopo la rispediva. 155 caratteri ogni quindici minuti, per sempre.</para>
/// </summary>
public class FrasiVloaTests
{
    /// <summary>I confinanti: le coppie da cui le vLOA sono nate.</summary>
    private sealed class Confinanti : INeighbourRepository
    {
        private readonly List<NeighbourCandidate> _righe;

        public Confinanti(params (string Home, string Foreign, string Nome)[] coppie) =>
            _righe = coppie.Select((c, i) => new NeighbourCandidate
            {
                Id = i + 1,
                HomeAccCode = c.Home,
                ForeignAccCode = c.Foreign,
                ForeignAccName = c.Nome,
                CountryId = "XX",
                ForeignRootCallsign = c.Foreign + "_CTR",
            }).ToList();

        public Task<IReadOnlyList<NeighbourCandidate>> ListCandidatesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NeighbourCandidate>>(_righe);

        public Task<IReadOnlyList<DomesticSectorPoly>> ListDomesticSectorPolygonsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DomesticSectorPoly>>(Array.Empty<DomesticSectorPoly>());
        public Task<IReadOnlyList<string>> ListDomesticAccCodesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<Vipi.Application.Content.ForeignAccData>> ListForeignAccDataAsync(
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vipi.Application.Content.ForeignAccData>>(
                Array.Empty<Vipi.Application.Content.ForeignAccData>());
        public Task<(int Created, int Updated)> UpsertCandidatesAsync(
            IReadOnlyList<NeighbourCandidateUpsert> items, CancellationToken ct = default) => Task.FromResult((0, 0));
        public Task<NeighbourCandidate?> GetAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<NeighbourCandidate?>(_righe.FirstOrDefault(r => r.Id == id));
        public Task<int> AddManualAsync(NeighbourCandidateUpsert item, CancellationToken ct = default) =>
            Task.FromResult(0);
        public Task<SectorOwner?> FindSectorOwnerAsync(string callsign, CancellationToken ct = default) =>
            Task.FromResult<SectorOwner?>(null);
        public Task<int> MaterializeAndCreateVloaAsync(int candidateId, CancellationToken ct = default) =>
            Task.FromResult(0);
        public Task PersistForeignCatalogAsync(
            IReadOnlyList<ForeignAccImport> accs, bool manuale = false, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task SetStatusAsync(int id, NeighbourCandidateStatus status, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task SetPolygonAsync(int id, string? regionMapPolygon, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task Semina_Litaliano_Delle_Frasi_Di_Partenza()
    {
        var memoria = new MemoriaDiTraduzioneFinta();

        var scritte = await FrasiVloa.SeminaAsync(memoria, new Confinanti(("LIBB", "LGGG", "Greece")));

        Assert.Equal(7, scritte);   // quattro frasi con i codici + tre fisse
        var aree = memoria.Umane.Keys.Single(k => k.StartsWith("Both areas of responsibility"));
        Assert.Contains("LIBB/LGGG", aree);
        Assert.Contains("il confine comune è il limite fra gli ACC LIBB/LGGG", memoria.Umane[aree]);
    }

    /// <summary>
    /// ⚠️ Il testo seminato dev'essere <b>identico</b> a quello che sta nei documenti, o l'impronta non
    /// corrisponde e il seme non serve a niente: sarebbe una traduzione in memoria che nessun segmento cerca.
    /// È il motivo per cui le frasi hanno una fonte sola, dentro <c>VloaSections</c>.
    /// </summary>
    [Fact]
    public async Task Il_Testo_Seminato_E_Quello_Che_Sta_Nei_Documenti()
    {
        var memoria = new MemoriaDiTraduzioneFinta();
        await FrasiVloa.SeminaAsync(memoria, new Confinanti(("LIBB", "LGGG", "Greece")));

        var nelDocumento = VloaSections.Canonical("LIBB", "LGGG", "Greece")
            .SelectMany(s => s.Blocks)
            .Where(b => b.Format == BlockFormat.Prose && b.Body is not null)
            .Select(b => b.Body!)
            .ToList();

        Assert.NotEmpty(nelDocumento);
        foreach (var frase in nelDocumento)
            Assert.True(memoria.Umane.ContainsKey(frase), $"non seminata: {frase[..Math.Min(70, frase.Length)]}");
    }

    [Fact]
    public async Task Chi_Ce_Gia_Non_Si_Riscrive()
    {
        var gia = VloaSections.FrasiDaSeminare("LIBB", "LGGG", "Greece")[0].En;
        var memoria = new MemoriaDiTraduzioneFinta().GiaUmana(gia);

        var scritte = await FrasiVloa.SeminaAsync(memoria, new Confinanti(("LIBB", "LGGG", "Greece")));

        Assert.Equal(6, scritte);
        Assert.DoesNotContain(gia, memoria.Umane.Keys);
    }

    /// <summary>Le tre frasi senza parametri sono uguali per tutte le vLOA: si scrivono una volta, non una per coppia.</summary>
    [Fact]
    public async Task Le_Frasi_Fisse_Si_Scrivono_Una_Volta_Sola()
    {
        var memoria = new MemoriaDiTraduzioneFinta();

        var scritte = await FrasiVloa.SeminaAsync(memoria,
            new Confinanti(("LIBB", "LGGG", "Greece"), ("LIBB", "LDZO", "Zagreb"), ("LIBB", "LAAA", "Tirana")));

        // 4 frasi con i codici × 3 coppie, più 3 fisse una volta sola.
        Assert.Equal(4 * 3 + 3, scritte);
    }

    [Fact]
    public async Task Senza_Confinanti_Non_Scrive_Niente()
    {
        var memoria = new MemoriaDiTraduzioneFinta();

        Assert.Equal(0, await FrasiVloa.SeminaAsync(memoria, new Confinanti()));
        Assert.Empty(memoria.Umane);
    }
}
