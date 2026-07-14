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

    public DbSet<Acc> Accs => Set<Acc>();
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<UnificationRule> UnificationRules => Set<UnificationRule>();
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
    public DbSet<TransferFlow> TransferFlows => Set<TransferFlow>();
    public DbSet<TransferPoint> TransferPoints => Set<TransferPoint>();
    public DbSet<EditGrant> EditGrants => Set<EditGrant>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<AirportTransitionLevel> AirportTransitionLevels => Set<AirportTransitionLevel>();
    public DbSet<AirportRunway> AirportRunways => Set<AirportRunway>();
    public DbSet<AirportRunwayRule> AirportRunwayRules => Set<AirportRunwayRule>();
    public DbSet<AirportSid> AirportSids => Set<AirportSid>();
    public DbSet<SidFixAlias> SidFixAliases => Set<SidFixAlias>();
    public DbSet<AirportFrequencyLink> AirportFrequencyLinks => Set<AirportFrequencyLink>();
    public DbSet<AirportExtraSection> AirportExtraSections => Set<AirportExtraSection>();
    public DbSet<ImportPolicy> ImportPolicies => Set<ImportPolicy>();
    public DbSet<ImportState> ImportStates => Set<ImportState>();
    public DbSet<AccSector> AccSectors => Set<AccSector>();
    public DbSet<AirportSector> AirportSectors => Set<AirportSector>();
    public DbSet<SpecialArea> SpecialAreas => Set<SpecialArea>();
    public DbSet<NeighbourCandidate> NeighbourCandidates => Set<NeighbourCandidate>();
    public DbSet<DocumentProfile> DocumentProfiles => Set<DocumentProfile>();
    public DbSet<DocRelease> DocReleases => Set<DocRelease>();
    public DbSet<EditorTask> EditorTasks => Set<EditorTask>();
    public DbSet<EditResourceLock> EditResourceLocks => Set<EditResourceLock>();

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

        b.Entity<Acc>().HasIndex(x => x.Code).IsUnique();

        b.Entity<SidFixAlias>().HasIndex(x => x.Prefix).IsUnique();   // un solo alias per prefisso
        b.Entity<ImportState>().HasKey(x => x.Category);               // una riga per categoria di import

        b.Entity<AccSector>(e =>
        {
            e.HasIndex(x => x.ComposePosition).IsUnique();   // chiave naturale
            e.HasIndex(x => x.CenterId);
            e.HasIndex(x => x.ParentCallsign);               // gerarchia di copertura per callsign (Round 20)
            // FK su Acc.Code (chiave alternata): il centerId della sorgente è il codice ACC.
            e.HasOne(x => x.Acc).WithMany(a => a.AccSectors)
                .HasForeignKey(x => x.CenterId).HasPrincipalKey(a => a.Code)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AirportSector>(e =>
        {
            e.HasIndex(x => x.ComposePosition).IsUnique();   // chiave naturale
            e.HasIndex(x => x.AirportIcao);
            e.HasIndex(x => x.AccCode);
            e.HasIndex(x => x.ParentCallsign);               // gerarchia di copertura per callsign (Round 20)
            // FK su Airport.Icao (chiave alternata): cascade alla rimozione dell'aeroporto.
            e.HasOne(x => x.Airport).WithMany(a => a.AirportSectors)
                .HasForeignKey(x => x.AirportIcao).HasPrincipalKey(a => a.Icao)
                .OnDelete(DeleteBehavior.Cascade);
            // FK su Acc.Code (chiave alternata): l'ACC di competenza, ereditato dall'aeroporto.
            e.HasOne(x => x.Acc).WithMany()
                .HasForeignKey(x => x.AccCode).HasPrincipalKey(a => a.Code)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Airport>(e =>
        {
            e.HasIndex(x => x.Icao).IsUnique();
            e.HasIndex(x => x.AccId);
            // Gerarchia di copertura per callsign (Round 20): l'aeroporto è foglia, il padre è un callsign APP/CTR
            // (cross-ACC ammesso). Nessuna FK: ParentCallsign attraversa i cataloghi (AccSector/AirportSector).
            e.HasIndex(x => x.ParentCallsign);
            e.HasOne(x => x.Acc).WithMany(f => f.Airports).HasForeignKey(x => x.AccId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Sector>(e =>
        {
            e.HasIndex(x => x.Callsign).IsUnique();
            e.HasIndex(x => x.AccId);
            e.HasIndex(x => x.DocumentId);
            e.HasOne(x => x.Acc).WithMany(f => f.Sectors).HasForeignKey(x => x.AccId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Airport).WithMany(a => a.Sectors).HasForeignKey(x => x.AirportId).OnDelete(DeleteBehavior.SetNull);
            // Contenimento ad albero: padre→figli (no cascata per evitare cicli di delete su SQLite).
            e.HasOne(x => x.ParentSector).WithMany(p => p.Children).HasForeignKey(x => x.ParentSectorId).OnDelete(DeleteBehavior.Restrict);
            // Documento di riferimento (uno-a-molti): cancellare il documento non cancella i settori.
            e.HasOne(x => x.Document).WithMany(d => d.Sectors).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<UnificationRule>(e =>
        {
            e.HasIndex(x => new { x.AccId, x.Priority });
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne(x => x.Acc).WithMany(f => f.UnificationRules).HasForeignKey(x => x.AccId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Document>(e =>
        {
            e.HasIndex(x => new { x.Type, x.Status });
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne(x => x.CurrentVersion).WithMany().HasForeignKey(x => x.CurrentVersionId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<DocumentParty>(e =>
        {
            e.HasOne(x => x.Document).WithMany(d => d.Parties).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sector).WithMany().HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DocumentVersion>(e =>
        {
            e.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
            e.HasOne(x => x.Document).WithMany(d => d.Versions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DocumentSection>(e =>
        {
            e.HasIndex(x => new { x.DocumentVersionId, x.ParentSectionId, x.Order });
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne(x => x.DocumentVersion).WithMany(v => v.Sections).HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ParentSection).WithMany(s => s.Children).HasForeignKey(x => x.ParentSectionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ContentBlock>(e =>
        {
            e.HasIndex(x => new { x.DocumentVersionId, x.SectionId, x.Order });
            e.Property(x => x.RowVersion).IsConcurrencyToken();
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

        b.Entity<TransferFlow>(e =>
        {
            e.HasIndex(x => new { x.AccId, x.OwningSectorId, x.Order });
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne(x => x.Acc).WithMany().HasForeignKey(x => x.AccId).OnDelete(DeleteBehavior.Cascade);
            // Il flusso segue il proprio settore: se il settore sparisce, sparisce il flusso.
            e.HasOne(x => x.OwningSector).WithMany().HasForeignKey(x => x.OwningSectorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TransferPoint>(e =>
        {
            e.HasIndex(x => new { x.FlowId, x.Order });
            e.HasOne(x => x.Flow).WithMany(f => f.Points).HasForeignKey(x => x.FlowId).OnDelete(DeleteBehavior.Cascade);
            // Il ricevente nominale è un riferimento debole: se il settore sparisce, il punto resta (solo fallback).
            e.HasOne(x => x.NextSector).WithMany().HasForeignKey(x => x.NextSectorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<EditGrant>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.AccId }).IsUnique();
            e.HasOne(x => x.Acc).WithMany().HasForeignKey(x => x.AccId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StaffMember>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).ValueGeneratedNever();   // il UserId è l'identità IVAO, non un id DB
            e.HasIndex(x => x.IsActive);
        });

        // --- Profilo strutturato aeroporto: tutte FK→Airport con cascade + ordinamento (AirportId, Order). ---
        b.Entity<AirportTransitionLevel>(e =>
        {
            e.HasIndex(x => new { x.AirportId, x.Order });
            e.HasOne(x => x.Airport).WithMany(a => a.TransitionLevels).HasForeignKey(x => x.AirportId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AirportRunway>(e =>
        {
            e.HasIndex(x => new { x.AirportId, x.Order });
            e.HasOne(x => x.Airport).WithMany(a => a.Runways).HasForeignKey(x => x.AirportId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AirportRunwayRule>(e =>
        {
            e.HasIndex(x => new { x.AirportId, x.Order });
            e.HasOne(x => x.Airport).WithMany(a => a.RunwayRules).HasForeignKey(x => x.AirportId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AirportSid>(e =>
        {
            e.HasIndex(x => new { x.AirportId, x.Order });
            e.HasOne(x => x.Airport).WithMany(a => a.Sids).HasForeignKey(x => x.AirportId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AirportFrequencyLink>(e =>
        {
            e.HasIndex(x => new { x.AirportId, x.Order });
            e.HasOne(x => x.Airport).WithMany(a => a.FrequencyLinks).HasForeignKey(x => x.AirportId).OnDelete(DeleteBehavior.Cascade);
            // La sorgente è un altro settore (Sector.DefaultFrequency): se sparisce, sparisce il link (cascade).
            e.HasOne(x => x.SourceSector).WithMany().HasForeignKey(x => x.SourceSectorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AirportExtraSection>(e =>
        {
            e.HasIndex(x => new { x.AirportId, x.Order });
            e.HasOne(x => x.Airport).WithMany(a => a.ExtraSections).HasForeignKey(x => x.AirportId).OnDelete(DeleteBehavior.Cascade);
        });

        // (APP standalone: storage migrato su Document + DocumentProfile, doc 08e; entità AppProfile rimosse.)

        // --- Coppie ACC confinanti candidate a vLOA (staging del calcolo di adiacenza). ---
        b.Entity<NeighbourCandidate>(e =>
        {
            e.HasIndex(x => new { x.HomeAccCode, x.ForeignAccCode }).IsUnique();   // chiave naturale della coppia
        });

        // --- Release AIRAC (snapshot editoriale per ciclo di rilascio), modello unico per tutti i tipi. ---
        b.Entity<DocRelease>(e =>
        {
            e.HasIndex(x => new { x.TargetType, x.TargetKey, x.ReleaseEffectiveUtc });   // selezione della release effettiva
            e.HasIndex(x => new { x.TargetType, x.TargetKey, x.VersionNumber });
        });

        // --- Incarichi editoriali (task management). ---
        b.Entity<EditorTask>(e =>
        {
            e.HasIndex(x => x.AssigneeUserId);
            e.HasIndex(x => x.Status);
        });

        // --- Lock di editing esclusivo su risorse nominate (pagine admin di struttura, wizard nuovo doc). ---
        b.Entity<EditResourceLock>().HasIndex(x => x.ResourceKey).IsUnique();

        // --- Stato editoriale data-driven generico di un documento vIPI (1:1 col Document). Doc refactor 08e. ---
        b.Entity<DocumentProfile>(e =>
        {
            e.HasIndex(x => x.DocumentId).IsUnique();
            e.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        // --- Aree speciali/regolamentate importate dalla sorgente, legate all'ACC via Acc.Code. ---
        b.Entity<SpecialArea>(e =>
        {
            e.HasIndex(x => x.IvaoId).IsUnique();             // chiave naturale (reference update)
            e.HasIndex(x => x.CenterId);
            e.HasOne(x => x.Acc).WithMany()
                .HasForeignKey(x => x.CenterId).HasPrincipalKey(a => a.Code)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
