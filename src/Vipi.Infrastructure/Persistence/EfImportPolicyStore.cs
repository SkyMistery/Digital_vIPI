using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: policy di import globale come riga singola (Id=1), creata col default opt-out se assente.</summary>
public sealed class EfImportPolicyStore : IImportPolicyStore
{
    private readonly VipiDbContext _db;
    public EfImportPolicyStore(VipiDbContext db) => _db = db;

    public async Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.ImportPolicies.AsNoTracking().FirstOrDefaultAsync(ct);
        return row is null
            ? ImportPolicySnapshot.AllImported
            : new ImportPolicySnapshot(row.ImportTransitionAltitude, row.ImportRunways, row.ImportSectors,
                row.ImportSids, row.ImportSpecialAreas);
    }

    public async Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default)
    {
        var row = await _db.ImportPolicies.FirstOrDefaultAsync(ct);

        // Lo stato di partenza: la riga se c'è, altrimenti il default (è quello che GetAsync restituisce, e
        // quindi quello che l'applicazione stava usando davvero).
        var prima = row is null
            ? ImportPolicySnapshot.AllImported
            : new ImportPolicySnapshot(row.ImportTransitionAltitude, row.ImportRunways, row.ImportSectors,
                row.ImportSids, row.ImportSpecialAreas);

        // ⚠️ Il non-evento non si scrive (regola del giro Audit): un salvataggio che non cambia niente non
        // è un atto, e riscriverebbe «deciso da X il <oggi>» su una decisione presa da qualcun altro mesi fa.
        // La riga si crea comunque quando manca: serve a registrare CHI ha salvato, ed è l'unica cosa che
        // distingue una policy decisa da una nata dai default delle colonne (vedi ImportSids, luglio 2026).
        var diverse = Differenze(prima, policy);
        if (row is not null && diverse.Count == 0) return;

        if (row is null)
        {
            row = new ImportPolicy { Id = 1 };
            _db.ImportPolicies.Add(row);
        }
        row.ImportTransitionAltitude = policy.TransitionAltitude;
        row.ImportRunways = policy.Runways;
        row.ImportSectors = policy.Sectors;
        row.ImportSids = policy.Sids;
        row.ImportSpecialAreas = policy.SpecialAreas;
        row.UpdatedUtc = DateTime.UtcNow;
        row.UpdatedByUserId = updatedByUserId;

        // Nella STESSA SaveChanges dell'atto che descrive. Cambiare questa riga cambia il regime di scrittura
        // di tutta l'applicazione — quali dati la sorgente può sovrascrivere — ed era l'ultimo atto
        // amministrativo rimasto muto dopo il giro Audit del 22 agosto 2026: se domani le piste di un
        // aeroporto smettono di aggiornarsi, senza questa riga non c'è modo di sapere chi ha tolto la spunta.
        if (diverse.Count > 0)
            AuditScribe.Write(_db, updatedByUserId, AuditAction.Update, "ImportPolicy", row.Id.ToString(), new
            {
                // Le due direzioni non sono simmetriche e vanno distinte a colpo d'occhio: «manuale → da
                // sorgente» è quella che al prossimo import sovrascrive il lavoro fatto a mano.
                Manuali = diverse.Where(d => !d.A).Select(d => d.Categoria).ToArray(),
                DaSorgente = diverse.Where(d => d.A).Select(d => d.Categoria).ToArray(),
            });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Le sole categorie che cambiano, col valore di arrivo (<c>true</c> = da sorgente). I nomi sono
    /// quelli di <see cref="ImportCategory"/>: lo stesso vocabolario che usa la pagina Sorgenti.</summary>
    private static List<(string Categoria, bool A)> Differenze(ImportPolicySnapshot prima, ImportPolicySnapshot dopo)
    {
        var diff = new List<(string, bool)>();
        foreach (var c in Enum.GetValues<ImportCategory>())
            if (prima.IsImported(c) != dopo.IsImported(c))
                diff.Add((c.ToString(), dopo.IsImported(c)));
        return diff;
    }
}
