using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.ReleaseTargets;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Registry dei descrittori di release cablato sui tipi reali, per i test che costruiscono i repo a mano.
///
/// <para>
/// ⚠️ <b>SEI, non quattro.</b> Fino al 29 agosto 2026 questo elenco si fermava ai quattro civili, e con esso
/// tutta la suite di Infrastructure: nessun test ha mai visto un documento militare passare da un elenco
/// generico, ed è per questo che gli <c>.Include</c> mancanti in <c>EfDocumentAdminRepository</c>,
/// <c>EfChangesRepository</c> e <c>EfSearchRepository</c> non hanno fatto rumore. Un aiutante di test che
/// non conosce una famiglia è una <b>rete col buco disegnato dentro</b>: aggiungere qui un descrittore è
/// parte dell'aggiungere una famiglia, non un di più.
/// </para>
/// </summary>
internal static class TestReleaseTargets
{
    public static IReleaseTargetRegistry Registry(VipiDbContext db) =>
        new ReleaseTargetRegistry(new IReleaseTarget[]
        {
            new VloaReleaseTarget(db),
            new AppReleaseTarget(db),
            new AccVipiReleaseTarget(db),
            new AirportReleaseTarget(db),
            new AirportMilReleaseTarget(db),
            new AppMilReleaseTarget(db),
        });

    public static EfReleaseRepository ReleaseRepo(VipiDbContext db) => new(db, Registry(db), new EfMediaMaintenance(db));

    /// <summary>Registry delle rotte pubbliche, cablato sui descrittori reali (doc 13 §3e). Sei come sopra:
    /// un tipo che il registry non conosce fa esplodere <c>DocRoutes.For</c>, non tornare null.</summary>
    public static IDocRoutesRegistry Routes() =>
        new DocRoutesRegistry(new IDocKindRoutes[]
        {
            new VloaDocRoutes(), new AppDocRoutes(), new AccVipiDocRoutes(), new AirportDocRoutes(),
            new AirportMilDocRoutes(), new AppMilDocRoutes(),
        });

    public static EfDocumentAdminRepository AdminRepo(VipiDbContext db)
    {
        var registry = Registry(db);
        return new(db, registry, new EfReleaseRepository(db, registry, new EfMediaMaintenance(db)), new EfMediaMaintenance(db));
    }
}
