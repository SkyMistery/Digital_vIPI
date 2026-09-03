using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il guscio dei quattro editor documentali: quel che succede quando un gesto <b>fallisce</b>.
///
/// <para>
/// ⚠️ <b>Il modo in cui un editor si rompe peggio non è l'errore: è il silenzio.</b> Segnalazione dal campo
/// (1 settembre 2026): «la pagina si blocca in salvataggio e si deve ricaricare per farla salvare». Il badge
/// resta su «Salvataggio…», non compare nessun messaggio, e chi sta scrivendo non sa se ha perso il lavoro.
/// Questi test presidiano le due strade per cui quel silenzio arriva — un'eccezione FUORI dal guardiano, e
/// un guardiano che rimette a posto lo stato ma non chiede il ridisegno.
/// </para>
///
/// <para>Sono test sul guscio e non sull'editor montato: il guscio è una classe apposta perché il
/// comportamento si potesse provare senza una fixture con DbContext, lock e JS.</para>
/// </summary>
public class DocumentEditorShellTests
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>JS che non fa niente: il guscio lo chiama solo per aprire/chiudere le sezioni.</summary>
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => default;
    }

    /// <summary>
    /// Servizio di editing finto: implementa i tre metodi che il guscio usa davvero e rifiuta gli altri.
    /// <para>⚠️ Gli altri ventitré <b>sollevano</b> invece di tornare un valore innocuo: se un giorno il
    /// guscio ne chiamasse uno, deve cadere il test — non passare in silenzio.</para>
    /// </summary>
    private sealed class EditingFinto : IEditingService
    {
        public Func<int, Task>? SulRilascio { get; init; }
        public Func<int, Task<LockInfo>>? SullIspezione { get; init; }
        public Func<int, Task>? SullaBozza { get; init; }

        public int RilasciChiesti { get; private set; }

        public Task ReleaseLockAsync(int documentId, CancellationToken ct = default)
        {
            RilasciChiesti++;
            return SulRilascio?.Invoke(documentId) ?? Task.CompletedTask;
        }

        public Task<LockInfo> InspectLockAsync(int documentId, CancellationToken ct = default) =>
            SullIspezione?.Invoke(documentId) ?? Task.FromResult(LockInfo.Free());

        public Task<int> CreateDraftAsync(int documentId, CancellationToken ct = default)
        {
            SullaBozza?.Invoke(documentId).GetAwaiter().GetResult();
            return Task.FromResult(1);
        }

        public Task<LockInfo> AcquireLockAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult(LockInfo.Free());

        // ---- il resto non lo tocca il guscio ----
        private static Exception NonUsato([System.Runtime.CompilerServices.CallerMemberName] string? m = null) =>
            new NotSupportedException($"Il guscio non deve chiamare {m}.");

        public Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default) => throw NonUsato();
        public Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default) => throw NonUsato();
        public Task<int?> ResolveVloaIdByPairAsync(string homeAccCode, string foreignAccCode, CancellationToken ct = default) => throw NonUsato();
        public Task<int> CreateDocumentAsync(DocumentType type, string title, IReadOnlyList<int>? scopeSectorIds,
            int? primarySectorId, int? homeSectorId, int? neighbourSectorId, CancellationToken ct = default) => throw NonUsato();
        public Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default) => throw NonUsato();
        public Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default) => throw NonUsato();
        public Task DeleteBlockAsync(int blockId, CancellationToken ct = default) => throw NonUsato();
        public Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionRenderModeAsync(int sectionId, RenderMode mode, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionHiddenAsync(int sectionId, bool hidden, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionAudienceAsync(int sectionId, SectionAudience audience, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionBeforeParentBodyAsync(int sectionId, bool before, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionLeadSentenceAsync(int sectionId, bool lead, CancellationToken ct = default) => throw NonUsato();
        public Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default) => throw NonUsato();
        public Task DeleteSectionAsync(int sectionId, CancellationToken ct = default) => throw NonUsato();
        public Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default) => throw NonUsato();
        public Task MoveSectionBeforeAsync(int sectionId, int? beforeSectionId, CancellationToken ct = default) => throw NonUsato();
        public Task MoveSectionToParentAsync(int sectionId, int? newParentSectionId, int? beforeSectionId, CancellationToken ct = default) => throw NonUsato();
        public Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default) => throw NonUsato();
        public Task PublishAsync(int versionId, string? note, CancellationToken ct = default) => throw NonUsato();
        public Task<int> DiscardDraftAsync(int versionId, CancellationToken ct = default) => throw NonUsato();
        public Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default) => throw NonUsato();
        public Task ForceUnlockAsync(int documentId, CancellationToken ct = default) => throw NonUsato();
    }

    private static (DocumentEditorShell Guscio, List<string> Ridisegni) Guscio(IEditingService editing)
    {
        var ridisegni = new List<string>();
        var guscio = new DocumentEditorShell(
            editing, new NoJs(), new KeyLocalizer(), NullLogger.Instance,
            famiglia: "prova", chiaveNoPermesso: "Ed_NoPermission",
            ridisegna: () => { ridisegni.Add("ridisegna"); return Task.CompletedTask; })
        {
            DocumentId = 7,
            IsEditing = true,
        };
        return (guscio, ridisegni);
    }

    /// <summary>
    /// ⚠️ <b>La segnalazione dal campo, riprodotta.</b> Uscire dalla modifica fa DUE cose: rilascia il lock e
    /// poi rilegge com'è rimasto. La seconda stava fuori dal guardiano, quindi una sua eccezione — una corsa
    /// sul DbContext, un guasto passeggero del database — non veniva presa da nessuno: usciva dal gestore
    /// dell'evento, abbatteva il circuito Blazor, e a schermo restava una pagina che non risponde più. Il
    /// lavoro era già salvato (i gesti salvano uno per uno), quindi ricaricare «lo faceva salvare».
    /// </summary>
    [Fact]
    public async Task Uscire_dalla_modifica_non_lascia_scappare_l_eccezione()
    {
        var (guscio, _) = Guscio(new EditingFinto
        {
            SullIspezione = _ => throw new InvalidOperationException("A second operation was started on this context."),
        });

        // Non deve sollevare: un'eccezione qui non la prende più nessuno.
        await guscio.FinishEditingAsync();

        Assert.NotNull(guscio.Error);
        // E si esce comunque dalla modifica: il lock è stato rilasciato, restare «in modifica» sarebbe una bugia.
        Assert.False(guscio.IsEditing);
    }

    /// <summary>Il caso normale non cambia: lock rilasciato, stato riletto, fuori dalla modifica.</summary>
    [Fact]
    public async Task Uscire_dalla_modifica_rilascia_il_lock_e_chiude()
    {
        var editing = new EditingFinto();
        var (guscio, _) = Guscio(editing);

        await guscio.FinishEditingAsync();

        Assert.Equal(1, editing.RilasciChiesti);
        Assert.False(guscio.IsEditing);
        Assert.Null(guscio.Error);
    }

    /// <summary>
    /// ⚠️ <b>Il badge deve tornare indietro DA SOLO.</b> Il guardiano accende «Salvataggio…» e chiede il
    /// ridisegno; quando l'azione fallisce rimette lo stato a riposo ma il ridisegno non lo chiedeva più, e
    /// contava sul render automatico dell'evento — che ridisegna il componente che l'evento l'ha ricevuto,
    /// non per forza la pagina che disegna il badge. Un gesto nato dentro un componente figlio (l'allegato,
    /// l'immagine, gli editor strutturati) lasciava quindi il badge inchiodato e il messaggio d'errore
    /// invisibile: esattamente il sintomo segnalato.
    /// </summary>
    [Fact]
    public async Task Un_gesto_fallito_riporta_il_badge_a_riposo_E_chiede_il_ridisegno()
    {
        var (guscio, ridisegni) = Guscio(new EditingFinto());

        await guscio.GuardAsync(() => throw new InvalidOperationException("niente da fare"));

        Assert.Equal(SaveState.Idle, guscio.Save);
        Assert.Equal("niente da fare", guscio.Error);
        // Due: uno per accendere «Salvataggio…», uno per mostrare l'errore e spegnerlo.
        Assert.Equal(2, ridisegni.Count);
    }

    /// <summary>E il gesto riuscito lo chiede lo stesso: «Salvato» deve arrivare a chi lo disegna.</summary>
    [Fact]
    public async Task Un_gesto_riuscito_chiede_il_ridisegno()
    {
        var (guscio, ridisegni) = Guscio(new EditingFinto());

        await guscio.GuardAsync(() => Task.CompletedTask);

        Assert.Equal(SaveState.Saved, guscio.Save);
        Assert.Equal(2, ridisegni.Count);
    }

    /// <summary>
    /// Un gesto SILENZIOSO non tocca il badge — «Salvato» su un lock preso sarebbe una bugia — ma quando
    /// fallisce il messaggio deve arrivare a schermo lo stesso.
    /// </summary>
    [Fact]
    public async Task Anche_un_gesto_silenzioso_fallito_chiede_il_ridisegno()
    {
        var (guscio, ridisegni) = Guscio(new EditingFinto());

        await guscio.GuardCoreAsync(() => throw new InvalidOperationException("rotto"), silenziosa: true);

        Assert.Equal(SaveState.Idle, guscio.Save);
        Assert.Equal("rotto", guscio.Error);
        Assert.Single(ridisegni);   // niente badge da accendere: resta il ridisegno dell'errore
    }

    // ---- il tornello: una operazione per volta su questo contesto -------------------------------------

    /// <summary>
    /// ⚠️ <b>La segnalazione del 2 settembre 2026, riprodotta.</b> Aggiungendo una sotto-sezione tornava
    /// «A second operation was started on this context»: non un servizio iniettato male, ma <b>due catene
    /// di caricamento della stessa pagina</b> sovrapposte — un gesto che ricarica cede al primo
    /// <c>await</c>, il ridisegno che segue fa scattare <c>OnParametersSetAsync</c>, che ricarica di nuovo.
    /// Qui il secondo <b>aspetta</b> invece di partire in parallelo.
    /// </summary>
    [Fact]
    public async Task Due_caricamenti_insieme_non_si_sovrappongono()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var dentro = 0;
        var massimoInsieme = 0;
        var apri = new TaskCompletionSource();

        async Task Lento()
        {
            var quanti = Interlocked.Increment(ref dentro);
            massimoInsieme = Math.Max(massimoInsieme, quanti);
            await apri.Task;
            Interlocked.Decrement(ref dentro);
        }

        var primo = guscio.InFilaAsync(Lento);
        var secondo = guscio.InFilaAsync(Lento);

        Assert.False(secondo.IsCompleted);      // il secondo e' in coda, non in volo
        apri.SetResult();
        await Task.WhenAll(primo, secondo);

        Assert.Equal(1, massimoInsieme);
    }

    /// <summary>
    /// ⚠️ E il tornello <b>non deve chiudersi in faccia a se stesso</b>: le catene si annidano davvero —
    /// «inizia modifica» è un gesto (in fila) che chiama il ricarico della pagina (in fila). Senza la
    /// memoria del flusso corrente l'editor si pianterebbe invece di morire, che è peggio: sembra lentezza.
    /// </summary>
    [Fact]
    public async Task Una_catena_annidata_non_aspetta_se_stessa()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var passi = new List<string>();

        var lavoro = guscio.InFilaAsync(async () =>
        {
            passi.Add("fuori");
            await guscio.InFilaAsync(() => { passi.Add("dentro"); return Task.CompletedTask; });
            passi.Add("fine");
        });

        await lavoro.WaitAsync(TimeSpan.FromSeconds(5));   // senza la guardia, qui si aspetterebbe per sempre
        Assert.Equal(new[] { "fuori", "dentro", "fine" }, passi);
    }

    /// <summary>Anche i GESTI passano dal tornello: due salvataggi a raffica sono due catene sullo stesso
    /// contesto quanto lo sono un gesto e un ricarico.</summary>
    [Fact]
    public async Task Anche_i_gesti_stanno_in_fila()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var insieme = 0;
        var massimo = 0;
        var apri = new TaskCompletionSource();

        async Task Gesto()
        {
            massimo = Math.Max(massimo, Interlocked.Increment(ref insieme));
            await apri.Task;
            Interlocked.Decrement(ref insieme);
        }

        var a = guscio.GuardAsync(Gesto);
        var b = guscio.GuardAsync(Gesto);
        apri.SetResult();
        await Task.WhenAll(a, b);

        Assert.Equal(1, massimo);
    }
}
