using Vipi.Application.Abstractions;
using Vipi.Application.Content;
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

    public static EfReleaseRepository ReleaseRepo(VipiDbContext db) => new(db, Registry(db));

    public static EfDocumentAdminRepository AdminRepo(VipiDbContext db) => new(db, Registry(db));
}
