using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Chi scrive sul database non lo fa col <c>DbContext</c> del <b>circuito</b>.
///
/// <para>
/// ⚠️ <b>Perché una guardia strutturale e non un test di comportamento.</b> Questo guasto è una CORSA: due
/// operazioni che si sovrappongono sullo stesso contesto danno
/// «<c>A second operation was started on this context</c>», l'eccezione esce dal gestore dell'evento e il
/// circuito muore — a schermo una pagina che non risponde più, da ricaricare. Riprodurlo a comando non
/// riesce (in locale, su SQLite, la finestra è di millisecondi); su MariaDB, con la latenza vera, si apre.
/// È il guasto già pagato sei volte su questo prodotto, e l'unica difesa che regge nel tempo è
/// <b>strutturale</b>: la pagina possiede il proprio scope di DI.
/// </para>
///
/// <para>
/// ⚠️ <b>In Blazor Server «scoped» vuol dire PER CIRCUITO</b>, cioè per sessione e per ore — non per
/// richiesta. Un <c>@@inject IEditingService</c> in una pagina prende quindi il contesto che la sessione
/// condivide con la barra, le isole e i pannelli: basta che una di quelle letture capiti mentre la pagina
/// scrive. Il rimedio è <c>@@inherits OwningComponentBase</c> + <c>ScopedServices</c>, che
/// <c>DocumentSectionsEditor</c> e <c>VloaEditor</c> usavano già.
/// </para>
///
/// <para>La segnalazione che ha aperto il giro (1 settembre 2026): «si crea una sezione, la pagina si blocca
/// in salvataggio e si deve ricaricare per farla salvare», premendo <b>Fine modifica</b>.</para>
/// </summary>
public class ScopeDellEditingTests
{
    /// <summary>Le pagine e i componenti che scrivono documenti passando da <c>IEditingService</c>.</summary>
    public static TheoryData<string> ChiScrive => new()
    {
        // ⚠️ Anche il militare e' passato dalla PAGINA al COMPONENTE (carta 2026-09-03 §5b).
        "Components/Doc/MilSectionsEditor.razor",
        // ⚠️ L'APP e' passato dalla PAGINA al COMPONENTE (carta 2026-09-03 §5b): l'invariante segue chi
        // scrive davvero. La pagina ora e' un guscio sottile che non tocca IEditingService — se un giorno
        // tornasse a toccarlo, va rimessa in questo elenco.
        "Components/Doc/AppSectionsEditor.razor",
        "Pages/AccEditorPage.razor",
        // ⚠️ Anche l'aeroporto e' passato dalla PAGINA al COMPONENTE (carta 2026-09-03 §5b).
        "Components/Doc/AirportSectionsEditor.razor",
        "Pages/NewDocumentPage.razor",
        "Pages/VersioniPage.razor",
        "Components/VloaEditor.razor",
        "Components/DocumentSectionsEditor.razor",
    };

    /// <summary>
    /// Gli <b>altri</b> servizi che toccano il database. 🔴 Fino al 3 settembre 2026 questa guardia
    /// guardava il solo <c>IEditingService</c>, e per questo non ha visto i due che in produzione hanno
    /// prodotto <c>A second operation was started on this context instance</c>:
    /// <c>IMilitaryDocumentService</c> in <c>MilSectionsEditor</c> (a <c>CreaAsync</c>, che <b>scrive</b>) e
    /// <c>IAppDocumentService</c> in <c>AppSectionsEditor</c>. Una regola scritta per un nome solo copre un
    /// nome solo.
    /// </summary>
    private static readonly string[] ServiziCheToccanoIlDatabase =
    {
        "IEditingService", "IAirportEditingService", "IAirportSectorService",
        "IMilitaryDocumentService", "IAppDocumentService", "IDocumentAdminService",
        "IDocumentUnionService", "IReleaseService",
    };

    /// <summary>
    /// Chi è ancora sul contesto del circuito, con la ragione. ⚠️ <b>Non sono assolti</b>: sono noti e non
    /// ancora decisi, e stanno qui perché la rete valga da subito su tutto il resto. Chi ne sistema uno
    /// toglie anche la riga.
    ///
    /// <para>✅ <b>Vuoto dal 3 settembre 2026 (sera).</b> Ci sono stati dentro sette casi per poche ore —
    /// i tre di <c>AirportSectionsEditor</c>, quello di <c>NewDocumentPage</c> e i tre di
    /// <c>VersioniPage</c> — e sono stati spostati tutti. ⚠️ Su <c>VersioniPage</c> la domanda vera era se
    /// valesse anche qui la scelta <b>opposta</b> di <c>ReleasePanel</c> (che <c>IReleaseService</c> lo
    /// prende apposta dal circuito, perché il publish è composto col <c>BeforePublishAsync</c> della pagina
    /// ospite): no, perché quella pagina <b>non monta <c>ReleasePanel</c></b>. Verificato col <c>grep</c>,
    /// non dedotto.</para>
    ///
    /// <para>ℹ️ Resta qui, vuoto, perché è il posto giusto in cui scrivere il prossimo caso invece di
    /// spegnere la guardia — e perché il test qui sotto lo tiene onesto.</para>
    /// </summary>
    private static readonly HashSet<string> Tollerati = new(StringComparer.Ordinal);

    /// <summary>
    /// 🔴 La regola vale per <b>ogni</b> servizio che tocca il database, non per un nome solo.
    /// <para>In Blazor Server «scoped» vuol dire <b>per circuito</b>: un <c>@@inject</c> prende il contesto
    /// che la sessione condivide con barra, isole e pannelli, e basta che una di quelle letture capiti
    /// mentre la pagina scrive.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(ChiScrive))]
    public void Nessun_servizio_che_tocca_il_database_arriva_dal_circuito(string relativo)
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), relativo));
        var colpevoli = ServiziCheToccanoIlDatabase
            .Where(nome => Regex.IsMatch(sorgente, $@"^@inject\s+[\w\.]*{nome}\b", RegexOptions.Multiline))
            .Where(nome => !Tollerati.Contains(relativo + "|" + nome))
            .ToArray();

        Assert.True(colpevoli.Length == 0,
            $"{relativo} prende dal CIRCUITO servizi che toccano il database: {string.Join(", ", colpevoli)}. "
            + "Vanno da `ScopedServices.GetRequiredService<...>()`, come i loro vicini nello stesso file.");
    }

    /// <summary>⚠️ La tolleranza non è un parcheggio: se uno di quelli viene sistemato, la riga va tolta,
    /// o l'elenco invecchia e smette di dire la verità.</summary>
    [Fact]
    public void I_tollerati_sono_ancora_sul_circuito()
    {
        var ancora = Tollerati.Where(v =>
        {
            var pezzi = v.Split('|');
            var f = Path.Combine(Radice(), pezzi[0].Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(f)
                && Regex.IsMatch(File.ReadAllText(f), $@"^@inject\s+[\w\.]*{pezzi[1]}\b", RegexOptions.Multiline);
        }).ToArray();

        Assert.True(ancora.Length == Tollerati.Count,
            "Qualcuno dei tollerati NON e' piu' sul circuito: togli la sua riga dalla tolleranza. Restano: "
            + string.Join(", ", ancora));

        // ⚠️ Con l'elenco vuoto l'asserzione qui sopra e' VERA per costruzione (0 == 0), cioe' vacua. Questa
        // riga dice che essere vuoto e' lo stato ATTESO e non un elenco che si e' perso per strada: il
        // giorno in cui qualcuno ne aggiunge uno, va tolta insieme al suo commento.
        Assert.Empty(Tollerati);
    }

    [Theory]
    [MemberData(nameof(ChiScrive))]
    public void Il_servizio_di_editing_non_si_prende_dal_circuito(string relativo)
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), relativo));

        Assert.False(
            Regex.IsMatch(sorgente, @"^@inject\s+[\w\.]*IEditingService\b", RegexOptions.Multiline),
            $"{relativo} inietta IEditingService: così arriva dallo scope del CIRCUITO, e il suo DbContext è "
            + "condiviso con barra, isole e pannelli. Serve `@inherits OwningComponentBase` e "
            + "`ScopedServices.GetRequiredService<IEditingService>()`.");

        Assert.Matches(@"^@inherits\s+OwningComponentBase\b", TrovaRiga(sorgente, "@inherits"));
        Assert.Contains("ScopedServices.GetRequiredService<IEditingService>()", sorgente);
    }

    /// <summary>
    /// ⚠️ Una pagina <c>IAsyncDisposable</c> che possiede uno scope deve chiuderlo <b>a mano</b>: il renderer,
    /// quando il componente è asincrono-disposabile, chiama solo <c>DisposeAsync</c> — mai il <c>Dispose</c>
    /// sincrono, che è quello con cui <c>OwningComponentBase</c> chiude lo scope. Senza, ogni visita
    /// all'editor lascia in piedi uno scope con dentro un <c>DbContext</c>.
    /// </summary>
    [Fact]
    public void Una_pagina_async_disposable_chiude_lo_scope_a_mano()
    {
        // ⚠️ Lo scope lo possiede il COMPONENTE, non piu' la pagina: l'invariante segue chi lo possiede.
        var sorgente = File.ReadAllText(Path.Combine(Radice(), "Components/Doc/AirportSectionsEditor.razor"));

        Assert.Contains("IAsyncDisposable", sorgente);
        Assert.Contains("((IDisposable)this).Dispose();", sorgente);
    }

    /// <summary>
    /// ⚠️ E una pagina che possiede uno scope non può avere un <c>public void Dispose()</c>:
    /// <c>OwningComponentBase</c> implementa <c>IDisposable</c> in modo <b>esplicito</b>, quindi quel metodo
    /// non lo chiamerebbe nessuno — la pulizia salta in silenzio. Si scrive
    /// <c>protected override void Dispose(bool)</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ChiScrive))]
    public void Chi_possiede_uno_scope_non_ha_un_Dispose_pubblico(string relativo)
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), relativo));

        // ⚠️ Il metodo si riconosce a INIZIO RIGA, non con un `Contains`: la prima stesura di questa guardia
        // ha acceso il rosso su due pagine già a posto, perché quella frase compare dentro il commento che
        // spiega perché il metodo pubblico non va scritto. Una scansione sul testo vede anche i commenti.
        Assert.False(Regex.IsMatch(sorgente, @"^\s*public\s+void\s+Dispose\s*\(\s*\)", RegexOptions.Multiline),
            $"{relativo} ha un `public void Dispose()`: con OwningComponentBase non viene chiamato mai. "
            + "Va scritto `protected override void Dispose(bool disposing)`.");
    }

    private static string TrovaRiga(string sorgente, string prefisso) =>
        sorgente.Split('\n').FirstOrDefault(r => r.TrimStart().StartsWith(prefisso, StringComparison.Ordinal))?.Trim()
        ?? $"(nessuna riga che comincia per {prefisso})";

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
