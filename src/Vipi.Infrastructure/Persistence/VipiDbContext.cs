using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// DbContext SQLite della vIPI, separato da quello del sito host (ADR-0002 nota impl).
/// Enum salvati come stringa; concorrenza ottimistica su entità editabili (SPEC_Modello_Dati §6).
/// </summary>
public class VipiDbContext : DbContext
{
    public VipiDbContext(DbContextOptions<VipiDbContext> options) : base(options) { }

    public DbSet<Fir> Firs => Set<Fir>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<SectorGeometry> SectorGeometries => Set<SectorGeometry>();
    public DbSet<PositionSector> PositionSectors => Set<PositionSector>();
    public DbSet<HierarchyRelation> HierarchyRelations => Set<HierarchyRelation>();
    public DbSet<UnificationRule> UnificationRules => Set<UnificationRule>();
    public DbSet<Frequency> Frequencies => Set<Frequency>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentParty> DocumentParties => Set<DocumentParty>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentSection> DocumentSections => Set<DocumentSection>();
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
    public DbSet<SharedBlock> SharedBlocks => Set<SharedBlock>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NavReference> NavReferences => Set<NavReference>();
    public DbSet<CoordinationPoint> CoordinationPoints => Set<CoordinationPoint>();
    public DbSet<VectoringMinimaSet> VectoringMinimaSets => Set<VectoringMinimaSet>();
    public DbSet<VectoringMinimaRow> VectoringMinimaRows => Set<VectoringMinimaRow>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Tutti gli enum → stringa (più leggibili e stabili nel DB). SPEC §6.
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
            {
                var t = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                if (t.IsEnum) prop.SetProviderClrType(typeof(string));
            }

        b.Entity<Fir>().HasIndex(x => x.Code).IsUnique();

        b.Entity<Position>(e =>
        {
            e.HasIndex(x => x.Callsign).IsUnique();
            e.HasIndex(x => x.FirId);
            e.HasOne(x => x.Fir).WithMany(f => f.Positions).HasForeignKey(x => x.FirId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Sector>(e =>
        {
            e.HasIndex(x => new { x.FirId, x.Key }).IsUnique();
            e.HasOne(x => x.Fir).WithMany(f => f.Sectors).HasForeignKey(x => x.FirId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Geometry).WithMany().HasForeignKey(x => x.GeometryId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PositionSector>(e =>
        {
            e.HasKey(x => new { x.PositionId, x.SectorId });
            e.HasOne(x => x.Position).WithMany(p => p.PositionSectors).HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sector).WithMany().HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<HierarchyRelation>(e =>
        {
            e.HasIndex(x => new { x.ParentPositionId, x.ChildPositionId }).IsUnique();
            e.HasOne(x => x.ParentPosition).WithMany().HasForeignKey(x => x.ParentPositionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ChildPosition).WithMany().HasForeignKey(x => x.ChildPositionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UnificationRule>(e =>
        {
            e.HasIndex(x => new { x.FirId, x.Priority });
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne(x => x.Fir).WithMany(f => f.UnificationRules).HasForeignKey(x => x.FirId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Frequency>(e =>
            e.HasOne(x => x.Position).WithMany(p => p.Frequencies).HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Cascade));

        b.Entity<Document>(e =>
        {
            e.HasIndex(x => new { x.Type, x.ScopePositionId, x.Status });
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne(x => x.ScopePosition).WithMany().HasForeignKey(x => x.ScopePositionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CurrentVersion).WithMany().HasForeignKey(x => x.CurrentVersionId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<DocumentParty>(e =>
        {
            e.HasOne(x => x.Document).WithMany(d => d.Parties).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DocumentVersion>(e =>
        {
            e.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
            e.HasOne(x => x.Document).WithMany(d => d.Versions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DocumentSection>(e =>
        {
            e.HasIndex(x => new { x.DocumentVersionId, x.ParentSectionId, x.Order });
            e.HasOne(x => x.DocumentVersion).WithMany(v => v.Sections).HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ParentSection).WithMany(s => s.Children).HasForeignKey(x => x.ParentSectionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ContentBlock>(e =>
        {
            e.HasIndex(x => new { x.DocumentVersionId, x.SectionId, x.Order });
            e.HasIndex(x => x.ScopeSectorId);
            e.HasOne(x => x.DocumentVersion).WithMany(v => v.Blocks).HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Section).WithMany(s => s.Blocks).HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ScopeSector).WithMany().HasForeignKey(x => x.ScopeSectorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FromSector).WithMany().HasForeignKey(x => x.FromSectorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ToSector).WithMany().HasForeignKey(x => x.ToSectorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SharedBlock).WithMany().HasForeignKey(x => x.SharedBlockId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SharedBlock>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        b.Entity<NavReference>().HasIndex(x => new { x.Type, x.Ident, x.AiracCycle });
        b.Entity<CoordinationPoint>().HasIndex(x => x.Ident);

        b.Entity<VectoringMinimaSet>(e =>
            e.HasOne(x => x.ScopeSector).WithMany().HasForeignKey(x => x.ScopeSectorId).OnDelete(DeleteBehavior.Restrict));
        b.Entity<VectoringMinimaRow>(e =>
            e.HasOne(x => x.Set).WithMany(s => s.Rows).HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Cascade));
    }
}
