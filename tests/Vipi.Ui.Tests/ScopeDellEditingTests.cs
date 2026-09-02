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
        "Pages/MilEditorPage.razor",
        "Pages/AppEditorPage.razor",
        "Pages/AccEditorPage.razor",
        "Pages/AeroportoEditorPage.razor",
        "Pages/NewDocumentPage.razor",
        "Pages/VersioniPage.razor",
        "Components/VloaEditor.razor",
        "Components/DocumentSectionsEditor.razor",
    };

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
        var sorgente = File.ReadAllText(Path.Combine(Radice(), "Pages/AeroportoEditorPage.razor"));

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
