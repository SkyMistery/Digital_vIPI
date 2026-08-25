using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.ReleaseTargets;

namespace Vipi.Infrastructure.Tests;

/// <summary>Registry dei descrittori di release cablato sui 4 tipi reali, per i test che costruiscono i repo a mano.</summary>
internal static class TestReleaseTargets
{
    public static IReleaseTargetRegistry Registry(VipiDbContext db) =>
        new ReleaseTargetRegistry(new IReleaseTarget[]
        {
            new VloaReleaseTarget(db),
            new AppReleaseTarget(db),
            new AccVipiReleaseTarget(db),
            new AirportReleaseTarget(db),
        });

    public static EfReleaseRepository ReleaseRepo(VipiDbContext db) => new(db, Registry(db), new EfMediaMaintenance(db));

    /// <summary>Registry delle rotte pubbliche, cablato sui 4 descrittori reali (doc 13 §3e).</summary>
    public static IDocRoutesRegistry Routes() =>
        new DocRoutesRegistry(new IDocKindRoutes[]
        {
            new VloaDocRoutes(), new AppDocRoutes(), new AccVipiDocRoutes(), new AirportDocRoutes(),
        });

    public static EfDocumentAdminRepository AdminRepo(VipiDbContext db)
    {
        var registry = Registry(db);
        return new(db, registry, new EfReleaseRepository(db, registry, new EfMediaMaintenance(db)), new EfMediaMaintenance(db));
    }
}
