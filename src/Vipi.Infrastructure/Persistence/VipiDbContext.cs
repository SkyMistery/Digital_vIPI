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

    /// <summary>Le promozioni a mano: una riga per persona promossa. Carta del 28 agosto 2026 §5.</summary>
    public DbSet<RoleOverride> RoleOverrides => Set<RoleOverride>();
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
    public DbSet<CallsignAlias> CallsignAliases => Set<CallsignAlias>();
    public DbSet<SpecialArea> SpecialAreas => Set<SpecialArea>();
    public DbSet<SpecialAreaCenter> SpecialAreaCenters => Set<SpecialAreaCenter>();
    public DbSet<NeighbourCandidate> NeighbourCandidates => Set<NeighbourCandidate>();
    public DbSet<DocumentProfile> DocumentProfiles => Set<DocumentProfile>();
    public DbSet<DocRelease> DocReleases => Set<DocRelease>();
    public DbSet<EditorTask> EditorTasks => Set<EditorTask>();
    public DbSet<EditResourceLock> EditResourceLocks => Set<EditResourceLock>();
    public DbSet<DocumentImpact> DocumentImpacts => Set<DocumentImpact>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<AtcSession> AtcSessions => Set<AtcSession>();
    public DbSet<AtcSessionTraffic> AtcSessionTraffic => Set<AtcSessionTraffic>();
    public DbSet<StatsSettings> StatsSettings => Set<StatsSettings>();
    public DbSet<AtcSessionRunway> AtcSessionRunways => Set<AtcSessionRunway>();
    public DbSet<AirportDayTraffic> AirportDayTraffic => Set<AirportDayTraffic>();
    public DbSet<AtcMonthRollup> AtcMonthRollups => Set<AtcMonthRollup>();

    /// <summary>La memoria di traduzione: una riga per FRASE, non per documento. Carta del 27 agosto 2026.</summary>
    public DbSet<TranslationUnit> TranslationUnits => Set<TranslationUnit>();

    /// <summary>Il glossario di fraseologia: una riga per FORMULA, che vive dentro le frasi. §Q3.</summary>
    public DbSet<GlossaryTerm> GlossaryTerms => Set<GlossaryTerm>();

    /// <summary>L'anagrafica delle radioassistenze: una riga per codice+natura, condivisa da tutti i
    /// documenti che la citano (carta vSOP militari §12b).</summary>
    public DbSet<Navaid> Navaids => Set<Navaid>();

    /// <summary>I caricamenti del file dell'AIP: uno solo e' quello in vigore (carta del 29 agosto 2026).</summary>
    public DbSet<AirspaceImport> AirspaceImports => Set<AirspaceImport>();

    /// <summary>I volumi di spazio aereo letti dal file caricato.</summary>
    public DbSet<AirspaceVolume> AirspaceVolumes => Set<AirspaceVolume>();

    /// <summary>
    /// Lettura tollerante dell'azione di registro: un valore che questa versione non conosce diventa
    /// <see cref="AuditAction.Unknown"/> invece di far esplodere la query. Metodo e non lambda in linea perché
    /// l'albero di espressione di <c>HasConversion</c> non ammette un <c>out</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Si controlla il <b>NOME</b>, non il valore. <c>Enum.TryParse</c> accetta anche la forma numerica, e
    /// <c>Enum.IsDefined(valore)</c> non la ferma: un <c>'3'</c> in colonna passava come <c>Archive</c> — un'azione
    /// SBAGLIATA, che è peggio di un'azione ignota. Trovato da un test, non a mente.
    /// </remarks>
    private static AuditAction LeggiAzione(string? s) =>
        s is not null && Enum.IsDefined(typeof(AuditAction), s) ? Enum.Parse<AuditAction>(s) : AuditAction.Unknown;

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

        // ⚠️ Il registro è l'unico enum che si legge TOLLERANTE, e non è una svista che gli altri non lo siano.
        // Un `SectorType` sconosciuto è una corruzione e deve fermare tutto; una riga di registro con un'azione
        // sconosciuta è invece la normalità di un archivio append-only che attraversa le versioni — codice più
        // nuovo che l'ha scritta (un ramo non ancora fuso, un rollback in produzione), codice più vecchio che la
        // rilegge. Senza questa conversione la pagina del Registro moriva INTERA per una riga sola: misurato il
        // 25 agosto 2026, due righe `View` scritte dal ramo `statistiche-atc` uccidevano `/services/vsop/admin/audit`
        // su `main`. Ed è proprio il registro che si va a leggere quando qualcosa è andato storto.
        // La stringa originale si perde nella conversione, ma la riga resta leggibile: EntityType, DetailsJson,
        // autore e ora sono intatti, e il narratore la mostra nella famiglia «Altro».
        b.Entity<AuditLog>().Property(x => x.Action).HasConversion(v => v.ToString(), s => LeggiAzione(s));

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
            e.Property(x => x.ImportNavaids).HasDefaultValue(true);
        });

        b.Entity<AccSector>(e =>
        {
            // ⚠️ DUE indici unici, e dicono due cose diverse. IvaoId è l'IDENTITÀ (chi è questa riga) e regge
            // l'upsert; ComposePosition è il CALLSIGN, unico perché due settori non possono rispondere allo
            // stesso nominativo — ma può cambiare, ed è proprio quel che succede a una rinomina.
            // I null di IvaoId restano molti (le righe aggiunte a mano): tutti e tre i provider li ammettono.
            e.HasIndex(x => x.IvaoId).IsUnique();
            e.HasIndex(x => x.ComposePosition).IsUnique();
            e.HasIndex(x => x.CenterId);
            e.HasIndex(x => x.ParentCallsign);               // gerarchia di copertura per callsign (Round 20)
            e.Property(x => x.ShapeAiracCycle).HasMaxLength(8);   // "YYNN": misurato 4
            // ⚠️ Il default DICHIARATO NEL MODELLO, non lasciato alla migrazione. Senza, lo scaffolding
            // emette `defaultValue: ""` — che NON è un nome di valore dell'enum — e ogni riga già in tabella
            // tornerebbe indietro illeggibile alla prima SELECT. Vale perché `Source` è lo zero dell'enum:
            // con un default diverso EF ometterebbe la colonna in INSERT sul valore CLR di default e la riga
            // tornerebbe cambiata. Stessa regola di AgreementClause, poco più sotto.
            e.Property(x => x.ShapeSource).HasDefaultValue(ShapeSource.Source);
            // FK su Acc.Code (chiave alternata): il centerId della sorgente è il codice ACC.
            e.HasOne(x => x.Acc).WithMany(a => a.AccSectors)
                .HasForeignKey(x => x.CenterId).HasPrincipalKey(a => a.Code)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AirportSector>(e =>
        {
            e.HasIndex(x => x.IvaoId).IsUnique();            // l'identità (vedi il commento su AccSector)
            e.HasIndex(x => x.ComposePosition).IsUnique();   // il callsign: unico, ma può cambiare
            e.HasIndex(x => x.AirportIcao);
            e.HasIndex(x => x.AccCode);
            e.HasIndex(x => x.ParentCallsign);               // gerarchia di copertura per callsign (Round 20)
            e.Property(x => x.ShapeAiracCycle).HasMaxLength(8);   // "YYNN": misurato 4
            // ⚠️ Il default DICHIARATO NEL MODELLO, non lasciato alla migrazione. Senza, lo scaffolding
            // emette `defaultValue: ""` — che NON è un nome di valore dell'enum — e ogni riga già in tabella
            // tornerebbe indietro illeggibile alla prima SELECT. Vale perché `Source` è lo zero dell'enum:
            // con un default diverso EF ometterebbe la colonna in INSERT sul valore CLR di default e la riga
            // tornerebbe cambiata. Stessa regola di AgreementClause, poco più sotto.
            e.Property(x => x.ShapeSource).HasDefaultValue(ShapeSource.Source);
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
            // Tre lettere piu' margine: senza misura Pomelo la renderebbe un longtext per un codice IATA.
            e.Property(x => x.Iata).HasMaxLength(4);
            // UNICO, e non è pignoleria: un documento d'aeroporto descrive UN aeroporto, e due scali che
            // puntano allo stesso documento sono un difetto che si vedrebbe solo mesi dopo, a schermo, come un
            // aeroporto che mostra le piste di un altro. I NULL restano molti (gli scali senza documento):
            // tutti e tre i provider ammettono più NULL in un indice unico.
            e.HasIndex(x => x.DocumentId).IsUnique();
            // Cancellare il documento non cancella l'aeroporto: come per Sector.Document, il legame si recide.
            e.HasOne(x => x.Document).WithOne(d => d.Airport).HasForeignKey<Airport>(x => x.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            // L'edizione MILITARE dello stesso scalo (carta vSOP militari §1b). Unico per la stessa ragione
            // del gemello civile: un documento militare descrive UN aeroporto.
            // ⚠️ Con navigazione inversa (Document.MilAirport), e serve: IReleaseTarget.TryDescribe decide
            // guardando il DOCUMENTO in mano e non ha modo di interrogare il database. Non e' un doppione
            // di Document.Airport -- quella dice «di quale scalo sono la vIPI civile», questa «di quale
            // scalo sono il vSOP militare» -- e su un documento ne e' valorizzata al piu' UNA.
            e.HasIndex(x => x.MilDocumentId).IsUnique();
            e.HasOne(x => x.MilDocument).WithOne(d => d.MilAirport).HasForeignKey<Airport>(x => x.MilDocumentId)
                .OnDelete(DeleteBehavior.SetNull);
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
            // L'edizione MILITARE del documento di questo settore (APP non remotizzato). Non unico: come il
            // gemello civile, piu' settori possono condividere lo stesso documento.
            e.HasIndex(x => x.MilDocumentId);
            e.HasOne(x => x.MilDocument).WithMany(d => d.MilSectors).HasForeignKey(x => x.MilDocumentId)
                .OnDelete(DeleteBehavior.SetNull);
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

        b.Entity<StaffMember>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).ValueGeneratedNever();   // il UserId è l'identità IVAO, non un id DB
            e.HasIndex(x => x.IsActive);
        });

        b.Entity<RoleOverride>(e =>
        {
            // ⚠️ La chiave è il VID, non un id di comodo: è la tabella stessa a garantire «una riga per
            // persona». Con una chiave surrogata, promuovere due volte lascerebbe due righe e a decidere
            // sarebbe l'ordine della query — cioè il caso, sul permesso più alto del prodotto.
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).ValueGeneratedNever();   // il VID è l'identità IVAO, non un id DB
            e.Property(x => x.Level).HasMaxLength(32);         // enum → stringa (SPEC §6)
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.DisplayName).HasMaxLength(120);
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
            // UNICO come quello di DocumentVersion (DocumentId, VersionNumber): il progressivo si assegna con
            // max+1 letto in memoria (SaveReleaseAsync) e due pubblicazioni concorrenti sullo stesso bersaglio
            // prenderebbero lo stesso numero in silenzio — meglio un conflitto rumoroso da ritentare.
            e.HasIndex(x => new { x.TargetType, x.TargetKey, x.VersionNumber }).IsUnique();
        });

        // --- Incarichi editoriali (task management). ---
        b.Entity<EditorTask>(e =>
        {
            e.HasIndex(x => x.AssigneeUserId);
            e.HasIndex(x => x.Status);
        });

        // --- Lock di editing esclusivo su risorse nominate (pagine admin di struttura, wizard nuovo doc). ---
        b.Entity<EditResourceLock>().HasIndex(x => x.ResourceKey).IsUnique();

        // --- Casella degli impatti: che cosa, a monte, ha toccato un documento (carta 25-ago-2026). ---
        b.Entity<DocumentImpact>(e =>
        {
            // ⚠️ L'unicità deve valere SOLO fra le righe aperte, e MariaDB non ha indici unici parziali: per
            // questo ClearedUtc è NOT NULL con la sentinella DocumentImpact.Aperto ed entra nella chiave.
            // Senza, due giri concorrenti della proiezione (tredici chiamanti, alcuni in parallelo col giro
            // notturno) scrivono la stessa segnalazione due volte, e la casella comincia a raddoppiare.
            e.HasIndex(x => new { x.DocumentId, x.Kind, x.SourceKey, x.ClearedUtc }).IsUnique();
            // L'elenco «cosa c'è di aperto», che è la query della pagina e del banner.
            e.HasIndex(x => new { x.ClearedUtc, x.RaisedUtc });
            e.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Nominativi dismessi: lo storico, non l'identità (vedi CallsignAlias). ---
        b.Entity<CallsignAlias>(e =>
        {
            // Un callsign è stato di uno solo: se ricomparisse su una riga diversa sarebbe una storia da
            // guardare, non una da scrivere in silenzio.
            e.HasIndex(x => x.OldCallsign).IsUnique();
            e.HasIndex(x => new { x.Catalog, x.IvaoId });    // «cos'altro ha chiamato questa riga»: la catena delle rinomine
            e.Property(x => x.OldCallsign).HasMaxLength(32);
            e.Property(x => x.NewCallsign).HasMaxLength(32);
            // Il settore può sparire; l'alias no — deve continuare a spiegare uno storico che è ancora lì.
            e.HasOne(x => x.Sector).WithMany().HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.SetNull);
        });

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

        // --- Edizione civile o militare (carta vSOP militari §1a) ---
        // ⚠️ Default DICHIARATO NEL MODELLO e non solo nella migrazione, per la ragione gia' pagata due
        // volte: lo scaffolder propone defaultValue: "" -- che NON e' un nome di valore dell'enum -- e ogni
        // documento gia' in tabella tornerebbe illeggibile alla prima SELECT. Su Postgres, poi, la colonna
        // la aggiunge il reconciler, che il valore di backfill lo legge di qui. Vale perche' Civil e' lo
        // zero dell'enum.
        b.Entity<Document>()
            .Property(x => x.Edition)
            .HasDefaultValue(DocumentEdition.Civil);

        // --- A chi si rivolge una sezione (carta vSOP militari del 27 agosto 2026) ---
        // ⚠️ Default DICHIARATO NEL MODELLO e non solo nella migrazione: su Postgres la colonna la aggiunge
        // PostgresSchemaReconciler, che legge di qui il valore con cui backfillare le righe esistenti.
        // Senza, le sezioni già scritte nascerebbero con una stringa vuota, che non è un nome dell'enum.
        // Vale perché Both è lo zero dell'enum.
        b.Entity<DocumentSection>()
            .Property(x => x.Audience)
            .HasDefaultValue(SectionAudience.Both);

        // --- Memoria di traduzione (carta del 27 agosto 2026) --------------------------------------------
        // Content-addressed come i MediaAsset, e per la stessa ragione: l'identità di una traduzione è il
        // TESTO che traduce, non il documento in cui quel testo si trova oggi. Nessuna FK verso sezioni o
        // blocchi — una voce sopravvive al blocco che l'ha fatta nascere, e serve ancora al blocco che
        // domani conterrà la stessa frase.
        b.Entity<TranslationUnit>(e =>
        {
            // ⚠️ UNICO: è ciò che rende la memoria una memoria. Due righe con la stessa terna sarebbero due
            // traduzioni della stessa frase, e la lettura pescherebbe «la prima che capita» — cioè talvolta
            // quella automatica al posto di quella corretta a mano.
            e.HasIndex(x => new { x.SourceLang, x.TargetLang, x.SourceHash }).IsUnique();

            // ⚠️ Le lunghezze non sono cosmesi: senza, MySQL non può costruire l'indice unico su colonne
            // di testo (chiave troppo lunga). 8+8+64 caratteri stanno larghi dentro il limite anche a
            // quattro byte per carattere.
            e.Property(x => x.SourceLang).HasMaxLength(8);
            e.Property(x => x.TargetLang).HasMaxLength(8);
            e.Property(x => x.SourceHash).HasMaxLength(64);
            e.Property(x => x.Engine).HasMaxLength(32);

            // L'elenco «cosa manca ancora di rivedere», che è la query della vista e del badge.
            e.HasIndex(x => new { x.TargetLang, x.ReviewedUtc });
        });

        // --- Glossario di fraseologia (lavori-aperti §Q3) ------------------------------------------------
        // Vicina alla memoria di traduzione e distinta da lei: là un SEGMENTO INTERO tradotto, qui un pezzo
        // di frase e come va reso dentro qualunque frase lo contenga. Nessuna FK verso l'una o verso i
        // documenti: una voce di glossario non appartiene a niente, vale ovunque.
        b.Entity<GlossaryTerm>(e =>
        {
            // ⚠️ UNICO sulla CHIAVE MINUSCOLA, non sul testo scritto: due voci che differiscono solo per le
            // maiuscole sono la stessa voce con due rese, e la ricerca nel testo — che le maiuscole non le
            // guarda — ne applicherebbe una a caso. Vedi GlossaryTerm.SourceKey per il perché la colonna
            // esiste invece di lasciar decidere al confronto del database.
            e.HasIndex(x => new { x.SourceLang, x.TargetLang, x.SourceKey }).IsUnique();

            // Stessa ragione della memoria: senza le lunghezze, MySQL non riesce a costruire l'indice unico.
            // ⚠️ 200 caratteri per la sorgente non sono generosità: una voce di glossario è una FORMULA, e
            // una formula più lunga di così quasi certamente non è una formula — è una frase, e le frasi
            // intere le tratta già la memoria di traduzione. 8+8+200 stanno dentro il limite di chiave anche
            // a quattro byte per carattere.
            e.Property(x => x.SourceLang).HasMaxLength(8);
            e.Property(x => x.TargetLang).HasMaxLength(8);
            e.Property(x => x.SourceText).HasMaxLength(200);
            e.Property(x => x.SourceKey).HasMaxLength(200);
            e.Property(x => x.TargetText).HasMaxLength(400);
        });

        // --- Anagrafica delle radioassistenze (carta vSOP militari §12b) --------------------------------
        // Scritta una volta, esce uguale ovunque: nel vSOP di Amendola e in quello di Gioia. Nessuna FK verso
        // aeroporti o documenti — una radioassistenza non appartiene a nessuno, e la stessa compare nei SOP di
        // campi diversi.
        b.Entity<Navaid>(e =>
        {
            // L'identità: codice + famiglia + canale, scritta in una colonna sola.
            // ⚠️ In una colonna sola perché il canale è NULLABLE, e in SQLite come in MySQL due NULL non si
            // considerano uguali: un indice unico su (Code, Kind, Channel) non impedirebbe i doppioni
            // proprio sulle righe senza canale, che sono la maggioranza. Stessa scelta di
            // `GlossaryTerm.SourceKey`. La chiave la compone `EfNavaidCatalog.Chiave`, in un posto solo.
            e.HasIndex(x => x.NaturalKey).IsUnique();

            // Lunghezze dichiarate: senza, MySQL non riesce a costruire l'indice unico (stessa ragione del
            // glossario). Misure vere del sectorfile: codici di 2-3 lettere, canali `99Y`, frequenze `115.25`.
            e.Property(x => x.Code).HasMaxLength(8);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.NaturalKey).HasMaxLength(32);
            e.Property(x => x.Type).HasMaxLength(16);
            e.Property(x => x.Frequency).HasMaxLength(16);
            e.Property(x => x.Channel).HasMaxLength(8);
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

            // ⚠️ Dal 28 agosto 2026 in tabella c'è anche il resto del mondo, che è la maggioranza delle
            // righe: le finestre temporali delle statistiche («il mese scorso», «l'anno») filtrano SEMPRE
            // anche sulla divisione, e senza questo indice diventerebbero una scansione su dieci volte le
            // righe di prima. Gli altri tre indici reggono da soli perché partono da una colonna selettiva
            // (VID, callsign, turno).
            e.HasIndex(x => new { x.IsOutsideDivision, x.StartUtc });
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

        b.Entity<AirportDayTraffic>(e =>
        {
            // Chiave naturale composita: un aeroporto, un giorno. Niente Id surrogato — sarebbe un secondo
            // albero d'indice su decine di migliaia di righe che si leggono sempre per (campo, periodo).
            e.HasKey(x => new { x.Icao, x.Day });

            // L'ICAO sta nella chiave: senza lunghezza, su MySQL è `longtext` e InnoDB rifiuta l'indice.
            // Otto caratteri come ogni altro ICAO delle statistiche.
            e.Property(x => x.Icao).HasMaxLength(8);

            // «Il traffico italiano del mese scorso»: si legge per giorno, su tutti i campi insieme.
            e.HasIndex(x => x.Day);
        });

        b.Entity<AtcMonthRollup>(e =>
        {
            // Chiave naturale: un mese, una persona, un callsign. Come per il traffico d'aeroporto, niente
            // Id surrogato: queste righe si leggono sempre per (periodo, persona) e mai per numero.
            e.HasKey(x => new { x.Month, x.UserId, x.Callsign });

            // Nella chiave: senza lunghezza è `longtext` su MySQL e InnoDB rifiuta l'indice.
            e.Property(x => x.Callsign).HasMaxLength(32);
            e.Property(x => x.Position).HasMaxLength(16);

            // «Chi ha controllato in quel mese»: si legge per periodo, su tutte le persone insieme.
            e.HasIndex(x => x.Month);
            e.HasIndex(x => x.UserId);
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

        // --- Spazi aerei dell'AIP (carta del 29 agosto 2026) -------------------------------------------
        // Il catalogo della GEOMETRIA dell'AIP. Non e' un gemello di SpecialArea: le aree regolamentate
        // restano di IVAO, e dal file arrivano lette ma non utilizzabili.
        b.Entity<AirspaceImport>(e =>
        {
            e.HasIndex(x => x.Sha256);        // «e' lo stesso file di prima?»
            e.HasIndex(x => x.IsCurrent);     // il caricamento in vigore, che e' uno solo

            // Lunghezze dichiarate per TUTTI i provider e non nella mappa MySQL: la tabella nasce ADESSO,
            // quindi su Postgres non c'e' nessun `text` da convertire (il caso che il reconciler non sa
            // fare) e su MySQL non nasce `longtext`, che poi non si indicizzerebbe. Stessa scelta di
            // `MediaAsset.Sha256` e di `AtcSession.Callsign`.
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.AiracCycle).HasMaxLength(8);
            e.Property(x => x.UploadedByName).HasMaxLength(128);
        });

        b.Entity<AirspaceVolume>(e =>
        {
            // L'identita' dentro un caricamento. ⚠️ Ci vuole l'ordinale: nel file ci sono TRE chiavi in
            // doppio (`CTA ROMA Z9 GOLFO MANFREDONIA` e due aree di airwork), e senza ordinale il secondo
            // volume non entrerebbe — o peggio cancellerebbe il primo.
            e.HasIndex(x => new { x.ImportId, x.NaturalKey, x.Ordinal }).IsUnique();
            e.HasIndex(x => new { x.ImportId, x.Family });   // l'elenco per famiglia, che e' come si guarda

            // ⚠️ La famiglia e' un ENUM, quindi in colonna e' una STRINGA (SPEC §6) — e sta in un indice.
            // Senza lunghezza su MySQL nasce longtext e InnoDB non lo indicizza: il CREATE TABLE fallisce.
            // Non e' un ragionamento da rifare a mente: l'ha respinta `IndexedStringLengthTests`.
            e.Property(x => x.Family).HasMaxLength(32);

            e.Property(x => x.NaturalKey).HasMaxLength(300);   // misurato: la piu' lunga del file e' 142
            e.Property(x => x.Name).HasMaxLength(200);         // misurato: 113
            e.Property(x => x.Category).HasMaxLength(64);      // misurato: 26
            e.Property(x => x.AirspaceClass).HasMaxLength(4);
            e.Property(x => x.BaseRaw).HasMaxLength(32);
            e.Property(x => x.TopRaw).HasMaxLength(32);

            // Cancellare un caricamento porta via i suoi volumi: sono suoi, non hanno vita propria.
            e.HasOne(x => x.Import).WithMany(i => i.Volumes)
                .HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Cascade);
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
