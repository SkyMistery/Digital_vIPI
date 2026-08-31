using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Airspace;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La mappa degli spazi aerei (<c>/services/vsop/airspace</c>): dal 1 settembre 2026 <b>non è più pubblica</b>
/// — la apre lo staff di divisione e chi sta più in su, per decisione del committente.
///
/// <para>
/// ⚠️ <b>Il cancello sta in DUE sedi</b>, ed è la stessa regola del convertitore di coordinate: l'hub nasconde
/// la scheda a chi non può aprirla (<see cref="ServicesHomeTests"/>) e la <b>pagina rifiuta</b> chi scrive
/// l'indirizzo a mano. Nascondere e basta è un cancello che non c'è.
/// </para>
///
/// <para>
/// ⚠️ E il rifiuto arriva <b>prima delle query</b>: un rifiuto disegnato sopra un dato già letto è un dato
/// già letto. Il doppio dell'archivio conta le chiamate proprio per questo.
/// </para>
/// </summary>
public class PaginaSpaziAereiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class FakeAuthz(VipiRole livello) : IEditAuthorizationService
    {
        public VipiRole Role { get; } = livello;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public void EnsureAdmin() { }
    }

    /// <summary>L'archivio finto: un caricamento in vigore, e il conto delle chiamate ricevute.</summary>
    private sealed class CatalogoFinto : IAirspaceCatalog
    {
        public int Chiamate { get; private set; }

        public Task<AirspaceImportRow?> GetCurrentAsync(CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult<AirspaceImportRow?>(new AirspaceImportRow(
                1, "italia.kmz", "abc", 1024, "2609", null, DateTime.UtcNow, "Tizio", 10, 10, 0, 100, true));
        }

        public Task<IReadOnlyDictionary<AirspaceFamily, int>> CountByFamilyAsync(int? importId = null, CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult<IReadOnlyDictionary<AirspaceFamily, int>>(
                new Dictionary<AirspaceFamily, int> { [AirspaceFamily.Ctr] = 3 });
        }

        public Task<IReadOnlyList<AirspaceVolumeRow>> ListVolumesAsync(AirspaceVolumeQuery query, CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult<IReadOnlyList<AirspaceVolumeRow>>(Array.Empty<AirspaceVolumeRow>());
        }

        public Task<IReadOnlyList<AirspaceImportRow>> ListImportsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<AirspaceImportRow> SaveAsync(NewAirspaceImport nuovo, AirspaceReadResult letto,
            DateTime quando, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AirspaceVolumeRow>> GetVolumesAsync(IReadOnlyList<int> ids, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<AirspaceIssue>> GetIssuesAsync(int importId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<(string FileName, byte[] Content)?> GetFileAsync(int importId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task SetCurrentAsync(int importId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(int importId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FormeFinte : ISectorShapeRepository
    {
        public int Chiamate { get; private set; }

        public Task<IReadOnlyList<SectorShapeRow>> ListShapeCandidatesAsync(CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult<IReadOnlyList<SectorShapeRow>>(Array.Empty<SectorShapeRow>());
        }

        public Task ApplyShapeAsync(ShapeWrite write, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PromoteDueShapesAsync(DateTime nowUtc, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private (IRenderedComponent<AirspacePage> Cut, CatalogoFinto Catalogo, FormeFinte Forme) Render(VipiRole livello)
    {
        var catalogo = new CatalogoFinto();
        var forme = new FormeFinte();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz(livello));
        Services.AddSingleton<IAirspaceCatalog>(catalogo);
        Services.AddSingleton<ISectorShapeRepository>(forme);
        return (RenderComponent<AirspacePage>(), catalogo, forme);
    }

    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.IvaoStaff)]
    public void Sotto_lo_staff_di_divisione_la_pagina_dice_di_no(VipiRole livello)
    {
        var (cut, catalogo, forme) = Render(livello);

        Assert.Contains("Asp_PubStaffOnly", cut.Markup);
        Assert.Empty(cut.FindAll(".sh-chip"));          // nessun filtro: non c'è niente da filtrare
        // ⚠️ E soprattutto: nessuna query. Il rifiuto arriva PRIMA di leggere l'archivio.
        Assert.Equal(0, catalogo.Chiamate);
        Assert.Equal(0, forme.Chiamate);
    }

    [Theory]
    [InlineData(VipiRole.DivisionStaff)]
    [InlineData(VipiRole.Editor)]
    [InlineData(VipiRole.Admin)]
    public void Dallo_staff_di_divisione_in_su_la_mappa_si_apre(VipiRole livello)
    {
        var (cut, catalogo, _) = Render(livello);

        Assert.DoesNotContain("Asp_PubStaffOnly", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".sh-chip"));       // le famiglie ci sono
        Assert.True(catalogo.Chiamate > 0);
    }

    /// <summary>
    /// ⚠️ Il <b>vecchio</b> indirizzo pubblico resta una rotta della stessa pagina: è stato pubblico per
    /// giorni, sta nei collegamenti già scritti, e una rotta in più costa una riga mentre un 404 costa una
    /// segnalazione. Chi ci arriva trova la stessa pagina — quindi anche lo stesso rifiuto.
    /// </summary>
    [Fact]
    public void I_due_indirizzi_portano_alla_stessa_pagina()
    {
        var rotte = typeof(AirspacePage)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(r => r.Template)
            .ToList();

        Assert.Contains("/services/vsop/airspace", rotte);
        Assert.Contains("/services/airspace", rotte);
    }
}
