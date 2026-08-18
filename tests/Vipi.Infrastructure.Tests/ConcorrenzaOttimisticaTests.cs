using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La concorrenza ottimistica promessa da <c>VipiDbContext</c> («concorrenza ottimistica su entità editabili»,
/// SPEC_Modello_Dati §6) messa alla prova nell'unico modo che conta: <b>due editor, due contesti, la stessa
/// riga</b>. Il secondo che salva deve trovare la porta chiusa.
///
/// <para><b>Perché questi test nascono rossi.</b> Sono la prova del difetto A1 dell'audit del 14 agosto 2026
/// (<c>docs/history/audit-2026-08-14-database-mariadb.md</c>), scritti <b>prima</b> della correzione. Il
/// <c>RowVersion</c> è un <c>byte[]?</c> gestito dall'applicazione — MariaDB non ha un <c>rowversion</c>
/// automatico e nessuna configurazione chiede a EF di generarlo — quindi se non lo riscrive nessuno il token
/// resta costante (o <c>NULL</c>), la clausola <c>WHERE … AND RowVersion = @old</c> è sempre vera e il secondo
/// salvataggio sovrascrive il primo in silenzio.</para>
///
/// <para>⚠️ <b>Anche <c>ContentBlock</c> nasce rosso, ed è il dettaglio che sposta la diagnosi.</b> Quella è
/// l'unica entità con una rotazione vera (<c>EfEditingRepository.UpdateBlockAsync</c>, che riscrive il token
/// dopo il salvataggio e rilegge <c>OriginalValue</c> da quello mandato dal client). Ma la rotazione vive
/// <b>in quel metodo</b>, non nel modello: qualunque altro percorso che salvi un blocco attraverso il context
/// — e ce ne sono — non protegge niente. La garanzia che vogliamo non è «un repository si ricorda di farlo»,
/// è «passare dal context basta», ed è quello che questi test pretendono.</para>
///
/// <para>Diventano verdi con la rotazione centralizzata in <c>SaveChangesAsync</c> (passo 2 del blocco 1).
/// Le quattro entità per cui il committente ha confermato che il <b>last-write-wins è voluto</b>
/// (<c>UnificationRule</c>, <c>TransferFlow</c>, <c>SharedBlock</c>, <c>DocumentProfile</c>) perderanno il
/// token e la colonna: i loro test qui spariranno, e a presidiarle resterà
/// <see cref="Solo_le_entita_decise_dichiarano_un_token_di_concorrenza"/>.</para>
/// </summary>
public class ConcorrenzaOttimisticaTests : IAsyncLifetime
{
    /// <summary>
    /// Le entità che DEVONO dichiarare <c>IsConcurrencyToken()</c> a valle della decisione del 14 agosto 2026.
    /// Sono quelle che due editor aprono davvero insieme; per tutte le altre il last-write-wins è una scelta.
    /// </summary>
    private static readonly string[] ConTokenAtteso = ["ContentBlock", "Document", "DocumentSection"];

    // Connessione condivisa: il database in memoria vive finché resta aperta, e i due "editor" sono due
    // context distinti sopra la STESSA connessione — che è la parte che riproduce il caso reale.
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private DbContextOptions<VipiDbContext> _options = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;

        await using var db = NuovoContesto();
        await db.Database.EnsureCreatedAsync();
        await SeminaAsync(db);
    }

    public async Task DisposeAsync() => await _conn.DisposeAsync();

    private VipiDbContext NuovoContesto() => new(_options);

    /// <summary>
    /// Una riga per ciascuna delle sette entità con token. <b>Il <c>RowVersion</c> è assegnato esattamente
    /// come fa l'applicazione</b>, non come farebbe comodo: valorizzato dove i repository lo valorizzano alla
    /// creazione (<c>DocumentSection</c>, <c>ContentBlock</c>), lasciato <c>null</c> dove nessun percorso di
    /// scrittura lo tocca mai. Seminare tutti i token a un valore finto renderebbe il test più verde e meno
    /// vero.
    /// </summary>
    private static async Task SeminaAsync(VipiDbContext db)
    {
        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        db.Accs.Add(acc);
        await db.SaveChangesAsync();

        var settore = new Sector
        {
            Callsign = "LIRR_NE_CTR", Name = "Roma Nord-Est", AccId = acc.Id,
            Type = SectorType.Ctr, Kind = SectorKind.Acc,
        };
        var altro = new Sector
        {
            Callsign = "LIRR_TS_CTR", Name = "Roma Tirreno Sud", AccId = acc.Id,
            Type = SectorType.Ctr, Kind = SectorKind.Acc,
        };
        db.Sectors.Add(settore);
        db.Sectors.Add(altro);

        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedUtc = DateTime.UtcNow, LastUpdatedAiracCycle = "2608",
            // RowVersion: nessun percorso dell'applicazione lo assegna. Resta null, come in produzione.
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var versione = new DocumentVersion
        {
            DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Draft,
            CreatedByUserId = 1, CreatedUtc = DateTime.UtcNow, AiracCycle = "2608",
        };
        db.DocumentVersions.Add(versione);
        await db.SaveChangesAsync();

        var sezione = new DocumentSection
        {
            DocumentVersionId = versione.Id, Title = "Generalità", Order = 0, Depth = 0,
            SectionKey = "custom",
            RowVersion = Guid.NewGuid().ToByteArray(),   // come AddSectionAsync
        };
        db.DocumentSections.Add(sezione);
        await db.SaveChangesAsync();

        db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = versione.Id, SectionId = sezione.Id, Order = 0,
            Tier = BlockTier.Reduced, Format = BlockFormat.Prose, Visibility = BlockVisibility.Always,
            Body = "corpo iniziale",
            RowVersion = Guid.NewGuid().ToByteArray(),   // come AddBlockAsync
        });

        // Le quattro qui sotto non hanno (più) un token: servono al test speculare, quello che pretende che
        // il secondo salvataggio passi.
        db.SharedBlocks.Add(new SharedBlock
        {
            Key = "minime-generali", Title = "Minime generali", Format = BlockFormat.Prose,
            Body = "corpo iniziale",
        });

        db.UnificationRules.Add(new UnificationRule { AccId = acc.Id, Name = "Split WS2/WS5", Priority = 1 });

        db.DocumentProfiles.Add(new DocumentProfile { DocumentId = doc.Id });

        await db.SaveChangesAsync();

        db.CoordinationAgreements.Add(new CoordinationAgreement
        {
            OwnerAccId = acc.Id, SideASectorId = settore.Id, SideBSectorId = altro.Id,
            Note = "nota iniziale", Order = 0,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Il gesto che conta: A e B caricano la stessa riga da due context, A salva, poi salva B. Ritorna
    /// l'eccezione di B (o <c>null</c> se è passato liscio, che è il difetto).
    /// </summary>
    private async Task<Exception?> ConflittoFraDueEditor<T>(
        Func<VipiDbContext, Task<T>> carica, Action<T, string> scrivi) where T : class
    {
        await using var editorA = NuovoContesto();
        await using var editorB = NuovoContesto();

        var a = await carica(editorA);
        var b = await carica(editorB);   // stessa riga, stesso token originale: sono partiti insieme

        scrivi(a, "scritto da A");
        await editorA.SaveChangesAsync();

        scrivi(b, "scritto da B");
        return await Record.ExceptionAsync(() => editorB.SaveChangesAsync());
    }

    private static void PretendiConflitto(Exception? esito, string entita) =>
        Assert.True(esito is DbUpdateConcurrencyException,
            $"{entita}: il salvataggio del secondo editor è andato a buon fine invece di sollevare " +
            $"DbUpdateConcurrencyException. Il token di concorrenza dichiarato su questa entità non protegge " +
            $"nulla: la modifica del primo editor è stata sovrascritta in silenzio. " +
            $"(esito osservato: {esito?.GetType().Name ?? "nessuna eccezione"})");

    [Fact]
    public async Task Document_rileva_la_scrittura_concorrente() =>
        PretendiConflitto(
            await ConflittoFraDueEditor(db => db.Documents.FirstAsync(), (d, s) => d.Title = s),
            nameof(Document));

    [Fact]
    public async Task DocumentSection_rileva_la_scrittura_concorrente() =>
        PretendiConflitto(
            await ConflittoFraDueEditor(db => db.DocumentSections.FirstAsync(), (x, s) => x.Title = s),
            nameof(DocumentSection));

    [Fact]
    public async Task ContentBlock_rileva_la_scrittura_concorrente() =>
        PretendiConflitto(
            await ConflittoFraDueEditor(db => db.ContentBlocks.FirstAsync(), (x, s) => x.Body = s),
            nameof(ContentBlock));

    /// <summary>
    /// Il rovescio dei tre test qui sopra: dove il token è stato tolto di proposito, il secondo salvataggio
    /// deve **passare**. Non è una tautologia — è la differenza fra «abbiamo deciso il last-write-wins» e
    /// «ci siamo dimenticati di ruotare il token», che a schermo si assomigliano. Se un giorno una di queste
    /// quattro tornasse a dichiarare un token senza che nessuno lo ruoti, la rotazione centralizzata di
    /// <c>SaveChangesAsync</c> lo renderebbe comunque efficace e questo test lo direbbe.
    /// <para>Il caso era <c>TransferFlow</c> fino al 17 agosto 2026; è passato a
    /// <see cref="CoordinationAgreement"/>, che ne prende il posto <b>con la stessa decisione</b>. Sostituito e
    /// non cancellato apposta: togliere il caso insieme all'entità avrebbe perso silenziosamente la garanzia.</para>
    /// </summary>
    [Theory]
    [InlineData(nameof(SharedBlock))]
    [InlineData(nameof(UnificationRule))]
    [InlineData(nameof(CoordinationAgreement))]
    [InlineData(nameof(DocumentProfile))]
    public async Task Dove_il_last_write_wins_e_voluto_il_secondo_salvataggio_passa(string entita)
    {
        var esito = entita switch
        {
            nameof(SharedBlock) => await ConflittoFraDueEditor(db => db.SharedBlocks.FirstAsync(), (x, s) => x.Body = s),
            nameof(UnificationRule) => await ConflittoFraDueEditor(db => db.UnificationRules.FirstAsync(), (x, s) => x.Name = s),
            nameof(CoordinationAgreement) => await ConflittoFraDueEditor(db => db.CoordinationAgreements.FirstAsync(), (x, s) => x.Note = s),
            _ => await ConflittoFraDueEditor(db => db.DocumentProfiles.FirstAsync(), (x, s) => x.FreqOrderJson = s),
        };

        Assert.True(esito is null,
            $"{entita}: il secondo salvataggio è fallito con {esito?.GetType().Name}. Per questa entità il " +
            "last-write-wins è una decisione presa (14 ago 2026), non una svista: se ora serve il controllo " +
            "di concorrenza, va aggiunto il token E spostata l'entità fra quelle attese.");
    }

    /// <summary>
    /// Guardia complementare, e la ragione per cui esiste va detta: la decisione del 14 agosto è «per queste
    /// quattro entità il last-write-wins è <b>voluto</b>». Una decisione del genere si dimentica in tre mesi,
    /// e la prossima entità nasce con un <c>IsConcurrencyToken()</c> copiato dalla vicina — token che nessuno
    /// ruota, cioè il difetto A1 daccapo. Qui l'elenco è esplicito: aggiungere un token è legittimo, ma va
    /// fatto <b>con</b> la rotazione e <b>con</b> un test qui sopra.
    /// </summary>
    [Fact]
    public void Solo_le_entita_decise_dichiarano_un_token_di_concorrenza()
    {
        using var db = NuovoContesto();

        var conToken = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Where(p => p.IsConcurrencyToken).Select(_ => e.ClrType.Name))
            .Distinct()
            .OrderBy(n => n)
            .ToArray();

        Assert.True(conToken.SequenceEqual(ConTokenAtteso.OrderBy(n => n)),
            "l'insieme delle entità con token di concorrenza è cambiato.\n" +
            $"  atteso:    {string.Join(", ", ConTokenAtteso.OrderBy(n => n))}\n" +
            $"  osservato: {string.Join(", ", conToken)}\n" +
            "Un token aggiunto qui va accompagnato dalla rotazione e da un test di conflitto; un token " +
            "tolto va accompagnato dal DropColumn nelle DUE serie di migrazioni.");
    }
}
