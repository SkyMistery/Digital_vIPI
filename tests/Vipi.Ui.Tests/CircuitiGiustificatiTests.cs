using System.Text.RegularExpressions;

namespace Vipi.Ui.Tests;

/// <summary>
/// Una pagina che dichiara <c>@rendermode InteractiveServer</c> deve avere qualcosa da rendere interattivo.
///
/// <para><b>Il costo che questo test protegge.</b> Quel rendermode non è un'annotazione: ogni visitatore
/// di quella pagina apre un <b>WebSocket</b> e uno <b>stato lato server</b> che vive quanto la sessione. Il
/// sito gira su una sola istanza, senza backplane, con venticinque circuiti trattenuti (vedi
/// <c>VipiStartup</c>): sono una risorsa contata. Il 27 agosto 2026 l'INGRESSO del sito —
/// <c>/services/vsop</c> — ne apriva uno per ogni visitatore senza avere un solo comando dentro.</para>
///
/// <para><b>Perché guarda anche i componenti figli.</b> Il primo giro di questa verifica, fatto a mano,
/// cercava <c>@onclick</c> nel file della pagina e dichiarava «zero comandi» per <c>ChangedPage</c> — che
/// invece ha due filtri, scritti come <c>&lt;Chip OnActivate="…"&gt;</c>. Il comando c'era, stava un livello
/// più in basso. Un controllo che non scendesse di un livello direbbe di togliere il circuito a una pagina
/// che ne ha bisogno: farebbe più danno del difetto che cerca.</para>
///
/// <para>⚠️ Questo test è volutamente <b>permissivo</b>: davanti a un dubbio lascia passare. Serve a
/// impedire il caso evidente — una pagina senza niente da cliccare che apre un circuito — non a decidere
/// al posto di chi scrive.</para>
/// </summary>
public sealed class CircuitiGiustificatiTests
{
    /// <summary>
    /// I segni che su una pagina c'è qualcosa da fare. <c>@bind</c> e i gestori di evento sono comandi;
    /// <c>EventCallback</c> è un comando che il componente riceve da fuori; <c>IJSRuntime</c> e
    /// <c>OnAfterRender</c> hanno bisogno di un circuito per esistere.
    /// </summary>
    private static readonly Regex Comandi = new(
        @"@on(click|change|input|submit|keydown|keyup|keypress|focus|blur|drag\w*|drop|mouse\w*)\s*=" +
        @"|@bind\b|@bind-\w+|EventCallback|IJSRuntime|OnAfterRender|<EditForm|<Input[A-Z]",
        RegexOptions.Compiled);

    /// <summary>Un componente usato dalla pagina: <c>&lt;Chip …&gt;</c>, <c>&lt;VloaEditor …&gt;</c>.</summary>
    private static readonly Regex Componenti = new(@"<([A-Z][A-Za-z0-9]*)", RegexOptions.Compiled);

    public static TheoryData<string> PagineInterattive()
    {
        var dati = new TheoryData<string>();
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Radice(), "Pages"), "*.razor"))
            if (File.ReadAllText(f).Contains("@rendermode InteractiveServer", StringComparison.Ordinal))
                dati.Add(f);
        return dati;
    }

    [Theory]
    [MemberData(nameof(PagineInterattive))]
    public void Una_pagina_interattiva_ha_qualcosa_da_rendere_interattivo(string percorso)
    {
        var sorgente = File.ReadAllText(percorso);
        if (Comandi.IsMatch(sorgente)) return;   // il comando è sulla pagina stessa

        foreach (Match m in Componenti.Matches(sorgente))
        {
            var figlio = File.Exists(Sorgente(m.Groups[1].Value)) ? File.ReadAllText(Sorgente(m.Groups[1].Value)) : null;
            if (figlio is not null && Comandi.IsMatch(figlio)) return;   // il comando è un livello più in basso
        }

        Assert.Fail(
            $"{Path.GetFileName(percorso)} dichiara «@rendermode InteractiveServer» ma non c'è niente da " +
            "cliccare, né sulla pagina né nei componenti che usa. Ogni visitatore aprirebbe un WebSocket e " +
            "uno stato lato server per niente, e i circuiti sono venticinque in tutto.\n" +
            "Se il comando esiste ed è più in profondità di un livello, questo controllo non lo vede: in quel " +
            "caso si aggiunga il componente intermedio all'elenco, con scritto perché.");
    }

    /// <summary>
    /// E il contrario, per le due pagine da cui il circuito è stato tolto il 27 agosto 2026: devono restare
    /// senza. È il caso in cui un ritorno indietro non darebbe nessun segnale — la pagina funzionerebbe
    /// benissimo, solo con un WebSocket per visitatore in più.
    /// </summary>
    [Theory]
    [InlineData("SopHome.razor")]
    [InlineData("ReleasePreviewPage.razor")]
    public void Le_pagine_senza_comandi_restano_senza_circuito(string nome)
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), "Pages", nome));

        // ⚠️ La DIRETTIVA, cioè una riga che comincia con «@rendermode» — non la parola. Entrambe queste
        // pagine spiegano in un commento perché il rendermode non c'è, e in un commento Razor la chiocciola
        // si raddoppia: cercare la sottostringa nuda trova quel «@@rendermode» e il test fallisce sul
        // proprio stesso commento. (Successo al primo giro.)
        Assert.False(Regex.IsMatch(sorgente, @"^@rendermode\b", RegexOptions.Multiline),
            $"{nome} ha di nuovo un @rendermode: ogni visitatore torna ad aprire un WebSocket per una " +
            "pagina che non ha comandi.");
    }

    private static string Sorgente(string componente)
    {
        var trovati = Directory.GetFiles(Radice(), componente + ".razor", SearchOption.AllDirectories);
        return trovati.Length > 0 ? trovati[0] : "";
    }

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
