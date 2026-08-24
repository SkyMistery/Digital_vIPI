using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// DbContext SQLite della vIPI, separato da quello del sito host (ADR-0002 nota impl).
/// Enum salvati come stringa; concorrenza ottimistica su entità editabili (SPEC_Modello_Dati §6).
/// </summary>
public class VipiDbContext : DbContext
{
    public VipiDbContext(DbContextOptions<VipiDbContext> options) : base(options) { }

    // ─── Concorrenza ottimistica: la rotazione del token sta QUI, non nei repository ────────────────────
    //
    // Il RowVersion è un byte[] gestito dall'applicazione: MariaDB non ha un `rowversion` automatico e
    // nessuna delle tre configurazioni chiede a EF di generarlo. Se non lo riscrive nessuno, il token resta
    // costante (o NULL), la clausola `WHERE … AND RowVersion = @old` è sempre vera, e il secondo editor che
    // salva sovrascrive il primo SENZA che venga sollevata una DbUpdateConcurrencyException.
    //
    // Fino al 14 agosto 2026 era esattamente così: sette entità dichiaravano il token, e a ruotarlo era un
    // metodo solo (EfEditingRepository.UpdateBlockAsync). La garanzia che serve non è «un repository si
    // ricorda di farlo» — è «passare dal context basta», perché i percorsi di scrittura sono decine e ne
    // nascono di nuovi. Vedi docs/history/audit-2026-08-14-database-mariadb.md §A1 e
    // ConcorrenzaOttimisticaTests, che prima di questo blocco erano otto test rossi.

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RuotaTokenDiConcorrenza();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RuotaTokenDiConcorrenza();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Assegna un token nuovo a ogni entità in inserimento o modifica che ne dichiari uno.
    ///
    /// <para>⚠️ Tocca <c>CurrentValue</c> e <b>mai</b> <c>OriginalValue</c>: l'originale è il valore che
    /// finisce nella <c>WHERE</c>, cioè il confronto stesso. <c>EfEditingRepository</c> lo imposta a mano con
    /// il token arrivato dal browser (per accorgersi di una modifica avvenuta mentre l'editor era aperto, non
    /// solo mentre la richiesta era in volo), e quel percorso deve continuare a funzionare.</para>
    ///
    /// <para>Le due sovrascritture con parametro <c>bool</c> bastano a coprire anche <c>SaveChanges()</c> e
    /// <c>SaveChangesAsync(ct)</c> senza argomenti: EF li implementa delegando a queste.</para>
    ///
    /// <para>⚠️ Restano fuori <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>, che non passano dal change-tracker
    /// né da qui. Oggi è innocuo — l'unica entità che li usa (<c>EditResourceLock</c>) non ha token, e ha un
    /// meccanismo di esclusione suo — ma è la condizione da ricontrollare prima di convertire una scrittura
    /// su entità versionata in ExecuteUpdate.</para>
    /// </summary>
    private void RuotaTokenDiConcorrenza()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            foreach (var prop in entry.Properties)
                if (prop.Metadata.IsConcurrencyToken && prop.Metadata.ClrType == typeof(byte[]))
                    prop.CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }

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
    public DbSet<CoordinationAgreement> CoordinationAgreements => Set<CoordinationAgreement>();
    public DbSet<AgreementSection> AgreementSections => Set<AgreementSection>();
    public DbSet<AgreementAirport> AgreementAirports => Set<AgreementAirport>();
    public DbSet<AgreementClause> AgreementClauses => Set<AgreementClause>();
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
    public DbSet<SpecialAreaCenter> SpecialAreaCenters => Set<SpecialAreaCenter>();
    public DbSet<NeighbourCandidate> NeighbourCandidates => Set<NeighbourCandidate>();
    public DbSet<DocumentProfile> DocumentProfiles => Set<DocumentProfile>();
    public DbSet<DocRelease> DocReleases => Set<DocRelease>();
    public DbSet<EditorTask> EditorTasks => Set<EditorTask>();
    public DbSet<EditResourceLock> EditResourceLocks => Set<EditResourceLock>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<AtcSession> AtcSessions => Set<AtcSession>();
    public DbSet<AtcSessionTraffic> AtcSessionTraffic => Set<AtcSessionTraffic>();
    public DbSet<StatsSettings> StatsSettings => Set<StatsSettings>();
    public DbSet<AtcSessionRunway> AtcSessionRunways => Set<AtcSessionRunway>();

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

        b.Entity<Acc>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            // Default nel modello e non solo nella migration: su Postgres la colonna la aggiunge
            // PostgresSchemaReconciler, che legge di qui il valore con cui backfillare le righe esistenti.
            // Senza, gli ACC già in tabella — italiani compresi — nascerebbero con le aree spente.
            e.Property(x => x.SpecialAreasEnabled).HasDefaultValue(true);
        });

        b.Entity<SidFixAlias>().HasIndex(x => x.Prefix).IsUnique();   // un solo alias per prefisso
        b.Entity<ImportState>().HasKey(x => x.Category);               // una riga per categoria di import

        // Policy di import: i flag aggiunti DOPO la creazione della tabella devono nascere a `true`, altrimenti
        // la riga di policy già esistente si ritrova la categoria spenta (opt-out ribaltato). Il default sta nel
        // modello e non solo nella migration perché su Postgres lo schema si allinea con PostgresSchemaReconciler,
        // che legge i default da qui (deploy Render+Neon con EnsureCreated, ADR-0007).
        b.Entity<ImportPolicy>(e =>
        {
            e.Property(x => x.ImportSids).HasDefaultValue(true);
            e.Property(x => x.ImportSpecialAreas).HasDefaultValue(true);
            e.Property(x => x.ImportAtcSessions).HasDefaultValue(true);
        });

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

        // NB: niente token di concorrenza qui — decisione del 14 agosto 2026, come per CoordinationAgreement,
        // SharedBlock e DocumentProfile. Vedi il commento esteso su SharedBlock più sotto.
        b.Entity<UnificationRule>(e =>
        {
            e.HasIndex(x => new { x.AccId, x.Priority });
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
            // doc 10 §3a: default DB Frozen → le righe esistenti (pre-migration) e ogni insert senza valore
            // esplicito diventano Frozen (enum→stringa "Frozen"). L'editor imposta Live dove serve.
            e.Property(x => x.RenderMode).HasDefaultValue(RenderMode.Frozen);
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

        // ─── Perché queste quattro entità NON hanno un token di concorrenza ─────────────────────────────
        // SharedBlock, UnificationRule, TransferFlow (oggi CoordinationAgreement) e DocumentProfile
        // dichiaravano un RowVersion che
        // nessun percorso di scrittura ha mai valorizzato: colonna sempre NULL, `WHERE … AND RowVersion IS
        // NULL` sempre vera, quindi una difesa solo nominale. Messi davanti alla scelta — ruotarlo o
        // toglierlo — il 14 agosto 2026 si è deciso di toglierlo: sono modificate da un editor alla volta,
        // sotto il lock di editing, e lì il last-write-wins è il comportamento voluto.
        //
        // La dichiarazione è sparita insieme alla colonna (DropColumn nelle due serie di migrazioni): una
        // difesa dichiarata e non funzionante è peggio della sua assenza, perché fa contare su qualcosa che
        // non c'è. A tenere ferma la decisione è ConcorrenzaOttimisticaTests, che verifica l'elenco esatto
        // delle entità con token — così la prossima non ne eredita uno per copia da qui.
        b.Entity<SharedBlock>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
        });

        b.Entity<NavReference>().HasIndex(x => new { x.Type, x.Ident, x.AiracCycle });
        b.Entity<CoordinationPoint>().HasIndex(x => x.Ident);

        // ─── Accordi di coordinamento ──────────────────────────────────────────────────────────────────
        // Unica scrittura dei coordinamenti (carta: docs/feature/2026-08-16-accordi-di-coordinamento.md).
        // Hanno preso il posto di TransferFlow/TransferPoint, droppate il 17 agosto 2026 dopo il travaso.
        b.Entity<CoordinationAgreement>(e =>
        {
            e.HasIndex(x => new { x.OwnerAccId, x.Order });
            // Niente token di concorrenza: vedi il commento su SharedBlock.
            e.HasOne(x => x.OwnerAcc).WithMany().HasForeignKey(x => x.OwnerAccId).OnDelete(DeleteBehavior.Cascade);

            // ⚠️ UNA scheda per coppia di enti. I lati stanno in forma canonica (id minore = A) perché in SQL
            // non esiste «insieme di due»: l'unicità di una coppia non orientata è un indice su due colonne
            // ordinate. Girare i lati non perde niente — il verso vive sulla SEZIONE e si ribalta con loro.
            e.HasIndex(x => new { x.SideASectorId, x.SideBSectorId }).IsUnique();

            // ⚠️ Restrict e non Cascade: sparire un settore non deve portarsi via l'accordo con tutte le sue
            // sezioni e clausole. Prima spariva solo la PARTE e l'accordo restava monco; adesso il capo è una
            // colonna NOT NULL, quindi il solo modo di non perdere lavoro editoriale è impedire la
            // cancellazione del settore finché un accordo lo cita.
            e.HasOne(x => x.SideASector).WithMany().HasForeignKey(x => x.SideASectorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SideBSector).WithMany().HasForeignKey(x => x.SideBSectorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AgreementSection>(e =>
        {
            e.HasOne(x => x.Agreement).WithMany(a => a.Sections).HasForeignKey(x => x.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AgreementId, x.Order });
            // Il traffico e il verso entrano nella chiave di lettura: l'editor cerca «la sezione gemella» e «il
            // verso opposto» a ogni render del riquadro.
            e.HasIndex(x => new { x.AgreementId, x.Kind, x.Direction });
        });

        b.Entity<AgreementAirport>(e =>
        {
            e.HasIndex(x => new { x.SectionId, x.Order });
            e.HasOne(x => x.Section).WithMany(s => s.Airports).HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
            // ICAO come soft-ref, esattamente come TransferFlow.AirportIcao: nessun FK (gli accordi citano anche
            // scali esteri fuori catalogo) e nessun indice, quindi nessuna lunghezza da dimensionare per MySQL.
        });

        b.Entity<AgreementClause>(e =>
        {
            e.HasOne(x => x.Section).WithMany(s => s.Clauses).HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
            // L'outline vive DENTRO una sezione: le clausole di un'altra non sono alternative delle prime, sono
            // un'altra tabella (Annex D.2 ne ha due). Fino al 18 agosto 2026 lo scopo era (accordo, verso), che
            // è la stessa cosa detta con due chiavi.
            e.HasIndex(x => new { x.SectionId, x.Order });
            e.HasIndex(x => new { x.SectionId, x.VariantGroup, x.Order });

            // Elenco dei punti: una stringa con separatore, come ConditionLabel fa già per le multi-pista.
            // Dimensionata anche fuori da MySQL perché è una lista corta per natura, non prosa.
            e.Property(x => x.Cops).HasMaxLength(200);
            e.Property(x => x.ConditionLabel).HasMaxLength(80);
            e.Property(x => x.ConditionAreaLabel).HasMaxLength(80);
            e.Property(x => x.ConditionCustomLabel).HasMaxLength(80);
            e.Property(x => x.HandoffLabel).HasMaxLength(80);
            e.Property(x => x.CommsHandoffLabel).HasMaxLength(80);

            // ⚠️ Default DICHIARATI NEL MODELLO, non solo nella migrazione. Questi enum stanno su colonna
            // testuale (conversione globale più sopra) e chi aggiunge la colonna a una tabella già piena deve
            // scriverci un valore che l'enum sappia rileggere. I percorsi sono due: la migrazione EF, e il
            // PostgresSchemaReconciler del deploy Render — che senza un default dichiarato backfilla con ''
            // (BackfillLiteral → DefaultLiteral) e la prima lettura andrebbe in eccezione. Dichiarandolo qui
            // vale per entrambi. Vale solo perché ognuno di questi default È lo zero del proprio enum: con un
            // default diverso, EF ometterebbe la colonna in INSERT sul valore CLR di default e la riga
            // tornerebbe indietro cambiata.
            e.Property(x => x.HandoffKind).HasDefaultValue(TransferHandoffKind.Unspecified);
            e.Property(x => x.CommsHandoffKind).HasDefaultValue(TransferHandoffKind.Unspecified);
            e.Property(x => x.HandoffLevelUnit).HasDefaultValue(LevelUnit.Fl);
            e.Property(x => x.HandoffLevelConstraint).HasDefaultValue(LevelConstraint.AtOrAbove);
            e.Property(x => x.SpeedConstraint).HasDefaultValue(SpeedConstraint.Unspecified);
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
            // NON aggiungere un indice unico su (AirportId, StableKey): la StableKey esclude di proposito la cifra
            // della revisione, quindi un file .sid con due revisioni della stessa SID (es. ROBOT1H e ROBOT2H)
            // produce legittimamente due righe con la stessa chiave. Misurato sul DB di sviluppo: 20 coppie così
            // su 1478 righe. Vedi ReplaceImportedSidsAsync, che per questo indicizza le righe precedenti con una
            // regola first-wins e non con un dizionario a chiave unica.
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
            // Niente token di concorrenza: vedi il commento su SharedBlock.
        });

        // --- Immagini dei documenti (blocchi Image). Content-addressed: lo sha256 È l'identità, quindi unico. ---
        // Nessuna FK verso blocchi o documenti: un asset sopravvive al blocco che lo cita, perché una release
        // pubblicata continua a citarne lo sha (docs/feature/2026-07-31-immagini-nei-blocchi §R2).
        b.Entity<MediaAsset>(e =>
        {
            e.HasIndex(x => x.Sha256).IsUnique();
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.OriginalFileName).HasMaxLength(200);
        });

        // --- Statistiche ATC (servizio /services/stats, carta del 24 agosto 2026) -----------------------
        b.Entity<AtcSession>(e =>
        {
            // La chiave è l'id di sessione IVAO: lo stesso numero nel whazzup e nello storico, quindi il
            // poller e il backfill scrivono sulla stessa riga. Non è generato da noi.
            e.HasKey(x => x.SessionId);
            e.Property(x => x.SessionId).ValueGeneratedNever();

            // Lunghezze dichiarate per TUTTI i provider, non nella mappa MySQL: la tabella nasce ora, quindi
            // su Postgres non c'è nessun `text` da convertire (il caso che il reconciler non sa fare) e su
            // MySQL non nasce `longtext`, che poi non si indicizzerebbe senza riscrivere mezzo milione di
            // righe. Stessa scelta di MediaAsset.Sha256. Misure: callsign più lungo osservato `LIMM_WS2_CTR`
            // (12), posizione `FSS`/`TWR` (3), frequenza `118.700` (7).
            e.Property(x => x.Callsign).HasMaxLength(32);
            e.Property(x => x.Position).HasMaxLength(16);
            e.Property(x => x.Frequency).HasMaxLength(16);

            e.HasIndex(x => new { x.UserId, x.StartUtc });    // «le mie sessioni», ordinate
            e.HasIndex(x => new { x.Callsign, x.StartUtc });  // «chi ha tenuto questa postazione»
            e.HasIndex(x => x.StartUtc);                      // finestre temporali (mese, anno, copertura)
            e.HasIndex(x => x.ShiftKey);                      // raccolta degli spezzoni in turni
        });

        b.Entity<AtcSessionTraffic>(e =>
        {
            // Chiave composita, senza Id surrogato: su ~500 000 righe l'anno sarebbe una colonna e un
            // secondo albero d'indice per niente. LegOrdinal distingue le tratte dello stesso callsign.
            e.HasKey(x => new { x.SessionId, x.PilotCallsign, x.LegOrdinal });

            // Il callsign pilota sta nella chiave: senza lunghezza, su MySQL è `longtext` e InnoDB rifiuta
            // l'indice. Misurato sullo snapshot whazzup del 24 agosto (467 piloti): massimo 7 caratteri
            // (`SIC0054`, `AFR94YB`), il formato IVAO ne ammette 10. Gli ICAO seguono Airport.Icao.
            e.Property(x => x.PilotCallsign).HasMaxLength(16);
            e.Property(x => x.DepIcao).HasMaxLength(8);
            e.Property(x => x.ArrIcao).HasMaxLength(8);
            e.Property(x => x.AircraftIcao).HasMaxLength(8);   // misurato 4 (`B38M`, `C700`)

            // ⚠️ Niente `HasMaxLength` per FirstPhase/LastPhase: sono enum, e la lunghezza degli enum su
            // MySQL la mette `MySqlStringLengths.Apply` (32 caratteri per tutti). Dichiararla qui non
            // eviterebbe il `longtext` — ci pensa già quella regola — e lascerebbe due misure diverse nei
            // due provider per la stessa colonna.

            e.HasOne(x => x.Session)
                .WithMany(x => x.Traffic)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);   // potando le sessioni sparisce anche il loro traffico

            e.HasIndex(x => x.PilotCallsign);        // «dove ho volato io», e i controlli di doppio conteggio
        });

        b.Entity<AtcSessionRunway>(e =>
        {
            e.HasOne(x => x.Session).WithMany(x => x.Runways)
                .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);

            // Si legge sempre «le configurazioni di QUESTA sessione, in ordine»: l'indice è quello.
            e.HasIndex(x => new { x.SessionId, x.FromUtc });

            // Corte per definizione: «16L/16R» è il caso lungo. Dichiarate per tutti i provider, come il
            // resto delle statistiche: la tabella nasce adesso e non c'è nessun `text` da convertire.
            e.Property(x => x.Arrival).HasMaxLength(32);
            e.Property(x => x.Departure).HasMaxLength(32);
        });

        // --- Aree speciali/regolamentate importate dalla sorgente. L'appartenenza agli ACC è molti-a-molti
        //     (la sorgente espone la stessa area sotto più centri): vive in SpecialAreaCenters. ---
        b.Entity<SpecialArea>(e =>
        {
            e.HasIndex(x => x.IvaoId).IsUnique();             // chiave naturale (reference update)
        });

        b.Entity<SpecialAreaCenter>(e =>
        {
            e.HasKey(x => new { x.IvaoId, x.CenterId });
            e.HasIndex(x => x.CenterId);
            e.HasOne(x => x.Area).WithMany(a => a.Centers)
                .HasForeignKey(x => x.IvaoId).HasPrincipalKey(a => a.IvaoId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Acc).WithMany()
                .HasForeignKey(x => x.CenterId).HasPrincipalKey(a => a.Code)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Due aggiustamenti che valgono SOLO su MySQL, entrambi indispensabili e per motivi diversi:
        //   - le lunghezze, senza cui il CREATE TABLE fallisce (InnoDB non indicizza longtext). Su SQLite
        //     sarebbero ignorate, su Postgres sarebbero un cambio di tipo che il reconciler non sa fare;
        //   - la collation case- e accent-sensitive, senza cui i confronti cambiano semantica in silenzio
        //     (LIRF == lirf) e gli indici unici collidono su dati legali.
        if (Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true)
        {
            MySqlStringLengths.Apply(b);
            MySqlCollation.Apply(b);
        }
    }
}
