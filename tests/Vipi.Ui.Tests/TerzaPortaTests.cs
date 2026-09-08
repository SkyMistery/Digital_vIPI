namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 <b>La terza porta: chi ha uno scope proprio deve ASPETTARE prima di chiuderlo.</b>
///
/// <para>Su un componente interattivo se ne conoscevano due, e servono a due cose diverse: lo <b>scope
/// proprio</b> protegge dagli ALTRI (nessuno usa il tuo <c>DbContext</c>) e la <b>sentinella di rientro</b>
/// protegge da SÉ STESSI (un gesto non parte sopra il precedente). <b>Nessuna delle due protegge dal
/// TEMPO</b>: l'utente cambia pagina, Blazor smonta il componente, <c>OwningComponentBase</c> chiude lo
/// scope — e con lui il <c>DbContext</c> e la sua connessione — <b>mentre una query è ancora aperta</b>.</para>
///
/// <para>🔴 <b>Misurato in produzione l'8 settembre 2026</b>, non dedotto, e per di più <b>dopo</b> le
/// correzioni di §CD: <c>StatsDivisionPage</c> ha entrambe le altre porte ed è caduta lo stesso alle
/// 20:26:02 (<c>ObjectDisposedException</c> in <c>EfAtcStatsQueries.ByPositionAsync</c>), già sotto 1.16.1.
/// Con lei sette <c>ObjectDisposedException</c> e sei voci della <b>faccia dal lato del pool</b>
/// («<c>Connection must be Open</c>», «<c>another read operation is pending</c>»,
/// «<c>Packet received out-of-order</c>»), che è il conto che paga un terzo quando una sessione MySQL torna
/// nel pool con una lettura aperta. Vedi <c>docs/lavori-aperti.md</c> §CF.</para>
///
/// <para>✅ La porta è <c>ScopeProprioCheAspetta</c>: chiude <b>e aspetta</b> chi era dentro (tetto 15 s),
/// e solo dopo smaltisce lo scope. Non è nuova — è la stessa di
/// <c>DocumentEditorShell.ChiudiAsync</c>, che dal 7 settembre fa questo per i cinque editor; quella vive
/// nella shell, che le pagine non hanno, questa sta nella base, dove ce l'hanno tutti.</para>
/// </summary>
public sealed class TerzaPortaTests
{
    /// <summary>
    /// Chi ha uno scope proprio e <b>non aspetta</b>: ventuno file, misurati l'8 settembre 2026.
    ///
    /// <para>⚠️ Come <c>DebitoNoto</c> in <c>ScopeProprioDellePagineTests</c>, questo elenco <b>non</b> è un
    /// obiettivo raggiunto: è un debito <b>scritto</b>, e serve a una cosa sola — che non ne nascano di nuovi
    /// senza che nessuno se ne accorga. Convertirne uno vuol dire toglierlo da qui, e il test resta verde da
    /// sé.</para>
    ///
    /// <para>🔴 <b>E un debito scritto va riletto quando la produzione parla.</b> La sera dell'8 settembre
    /// <c>AdminRolesPage</c> stava in un elenco come questo dal 4, e nello stesso pomeriggio è finita due
    /// volte nel registro degli errori: la lista diceva «non ne nascano di nuovi», e intanto una vecchia
    /// era diventata un guasto. Quando un nome di qui compare in <c>errori-richieste.txt</c>, la riga si
    /// converte — non si aggiorna la data.</para>
    /// </summary>
    private static readonly string[] SenzaAttesaNoto =
    {
        "AccLanding", "AeroportoEditorPage", "AeroportoPage", "AirportListPanel", "AirportQuickPanel",
        "AppEditorPage", "AtcWorldArchivePage", "AttachmentBlockEditor", "CoordinateConverterPage",
        "DocReviewBar", "DocumentSectionsEditor", "EditLockBar", "ImageBlockEditor", "ImportaTabella",
        "MediaCleanupCard", "MilEditorPage", "MilListPage", "NewDocumentPage", "PageIntroZone",
        "UnionPanel", "VloaDocumentView",
    };

    [Fact]
    public void Nessuno_scope_proprio_NUOVO_senza_la_terza_porta()
    {
        var senzaAttesa = SenzaLaTerzaPorta();
        var nuovi = senzaAttesa.Except(SenzaAttesaNoto, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(nuovi.Count == 0,
            "Componenti con uno scope proprio che NON aspettano il caricamento in volo prima di chiuderlo:\n" +
            string.Join("\n", nuovi.Select(p => "  " + p)) +
            "\n\nLo scope proprio protegge dagli ALTRI, la sentinella da SE' STESSI: nessuna delle due " +
            "protegge dal TEMPO. Se il componente si smonta con una query aperta, lo scope si porta via il " +
            "DbContext sotto quella query — misurato in produzione l'8 settembre 2026, anche su una pagina " +
            "che aveva gia' tutt'e due le altre porte.\n" +
            "La cura e' `@inherits ScopeProprioCheAspetta` al posto di `@inherits OwningComponentBase`, " +
            "coi CARICAMENTI dentro `InFilaAsync(...)`.\n" +
            "Chi ha una porta SUA che aspetta gia' (i cinque editor, via `DocumentEditorShell.ChiudiAsync`) " +
            "non conta: questo test lo riconosce da solo.");
    }

    /// <summary>⚠️ Il rovescio, e serve quanto l'altro: un elenco che nomina file convertiti — o spariti —
    /// smette di misurare e nessuno se ne accorge, perché resta verde.</summary>
    [Fact]
    public void L_elenco_di_chi_non_aspetta_non_nomina_lavoro_gia_fatto()
    {
        var vivi = SenzaLaTerzaPorta().ToHashSet(StringComparer.Ordinal);
        var fantasmi = SenzaAttesaNoto.Where(p => !vivi.Contains(p)).ToList();

        Assert.True(fantasmi.Count == 0,
            "L'elenco nomina componenti che la terza porta ce l'hanno gia' (o che non esistono piu'): " +
            string.Join(", ", fantasmi) + ". Se il lavoro e' fatto, la riga va tolta.");
    }

    /// <summary>
    /// 🔴 La forma peggiore, di nuovo: la base che aspetta <b>dichiarata</b> e poi non usata. Senza almeno un
    /// <c>InFilaAsync</c> la chiusura non ha nessuno da aspettare — la porta c'è, è scritta in testa al file,
    /// e non copre niente.
    /// </summary>
    [Fact]
    public void Chi_dichiara_la_terza_porta_ci_fa_passare_i_caricamenti()
    {
        var bugiardi = Razor()
            .Where(f => File.ReadAllText(f).Contains("@inherits ScopeProprioCheAspetta"))
            .Where(f => !File.ReadAllText(f).Contains("InFilaAsync("))
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(bugiardi.Count == 0,
            "Componenti che dichiarano `ScopeProprioCheAspetta` e non fanno passare NIENTE da `InFilaAsync`:\n" +
            string.Join("\n", bugiardi.Select(p => "  " + p)) +
            "\n\nLa chiusura aspetta chi e' dentro: se non entra nessuno, non aspetta nessuno e la difesa " +
            "e' solo dichiarata.");
    }

    /// <summary>
    /// Chi dichiara uno scope proprio con la base <b>vecchia</b> e non ha una porta sua che aspetta.
    /// ⚠️ I cinque editor passano da <c>DocumentEditorShell.ChiudiAsync</c>, che fa esattamente questo: si
    /// riconoscono dal nome del metodo, non da un elenco a mano che invecchierebbe da solo.
    /// </summary>
    private static IEnumerable<string> SenzaLaTerzaPorta() =>
        Razor()
            .Select(f => (Nome: Path.GetFileNameWithoutExtension(f), Testo: File.ReadAllText(f)))
            .Where(x => x.Testo.Contains("@inherits OwningComponentBase"))
            .Where(x => !x.Testo.Contains("ChiudiAsync"))
            .Select(x => x.Nome);

    private static IEnumerable<string> Razor() =>
        Directory.EnumerateFiles(Radice(), "*.razor", SearchOption.AllDirectories);

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
