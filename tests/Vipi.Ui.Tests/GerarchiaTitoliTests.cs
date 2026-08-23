using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Ogni pagina ha una testata, e la testata è un <c>&lt;h1&gt;</c>.
///
/// <para><b>Il difetto che presidia.</b> Fino al 23 agosto 2026 nessuna pagina ne aveva uno: la testata era
/// un <c>&lt;h2&gt;</c> e sulle vIPI lo erano anche i blocchi, quindi il titolo del documento e le sue
/// sezioni stavano allo stesso livello. Chi naviga per intestazioni — che è il modo in cui si legge un
/// documento operativo lungo con uno screen reader — non aveva una gerarchia da seguire, e chi arrivava su
/// una pagina non aveva modo di sentirne il titolo.</para>
///
/// <para><b>Perché si legge il SORGENTE e non il DOM.</b> Le pagine sono cinquanta, molte con rami
/// mutuamente esclusivi (documento trovato / non trovato / accesso negato) e dipendenze che renderle tutte
/// costerebbe più di quello che il test vale. Qui la domanda è strutturale — «quel file dichiara una
/// testata di livello 1?» — e sul testo si risponde in millisecondi e senza impalcature. ⚠️ Il limite va
/// detto: un <c>&lt;h1&gt;</c> dentro un ramo che non si rende mai passerebbe questo test. È il prezzo, ed è
/// piccolo rispetto al difetto che copre.</para>
/// </summary>
public class GerarchiaTitoliTests
{
    /// <summary>
    /// Pagine senza testata, con la ragione. Un elenco corto e motivato, non un interruttore: chi ne
    /// aggiunge una deve scrivere perché quella pagina non ha un titolo.
    /// </summary>
    private static readonly Dictionary<string, string> SenzaTestata = new()
    {
        // Reindirizza al viewer tipizzato: mostra una riga d'attesa e se ne va. Non è una pagina che si legge.
        ["ReleasePreviewPage.razor"] = "è un redirect, non una pagina",
    };

    public static TheoryData<string> Pagine()
    {
        var dati = new TheoryData<string>();
        foreach (var f in Directory.EnumerateFiles(CartellaPagine(), "*.razor")) dati.Add(Path.GetFileName(f));
        return dati;
    }

    [Theory]
    [MemberData(nameof(Pagine))]
    public void Ogni_pagina_dichiara_una_testata_di_livello_uno(string nomeFile)
    {
        var testo = File.ReadAllText(Path.Combine(CartellaPagine(), nomeFile));
        var h1 = Regex.Matches(testo, @"<h1\b", RegexOptions.IgnoreCase).Count;

        if (SenzaTestata.TryGetValue(nomeFile, out var motivo))
        {
            Assert.True(h1 == 0,
                $"{nomeFile} è nell'elenco delle pagine senza testata ({motivo}) ma ora ne ha una: " +
                "va tolta dall'elenco.");
            return;
        }

        Assert.True(h1 >= 1,
            $"{nomeFile} non dichiara nessun <h1>: la pagina non ha un titolo per chi la naviga per " +
            "intestazioni. La testata di pagina è `<h1 class=\"page-h1\">` — la classe tiene la misura di " +
            "prima (vedi vipi-theme.css), quindi la promozione non cambia il disegno.");
    }

    /// <summary>
    /// Il rovescio: la testata non deve tornare a essere un <c>&lt;h2&gt;</c> di fianco a un <c>&lt;h1&gt;</c>.
    /// Le due classi che disegnavano le testate (<c>st-h2</c>, <c>xt-h2</c>) sopravvivono per i titoli di
    /// SCHEDA, che sono un'altra cosa: quello che non deve più esistere è una di quelle classi su un
    /// elemento che intitola l'intera pagina, cioè dentro una <c>.doc-head</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pagine))]
    public void Nessuna_testata_di_pagina_e_rimasta_un_h2(string nomeFile)
    {
        var testo = File.ReadAllText(Path.Combine(CartellaPagine(), nomeFile));

        var sospetti = Regex.Matches(testo, @"<h2\b[^>]*class=""[^""]*\b(?:st-h2|xt-h2|page-h1)\b", RegexOptions.IgnoreCase)
            .Select(m => m.Value).ToList();

        Assert.True(sospetti.Count == 0,
            $"{nomeFile}: una testata di pagina è tornata <h2>.\n  " + string.Join("\n  ", sospetti));
    }

    /// <summary>
    /// Dal file di test alla cartella delle pagine. Si risale finché non si trova <c>src/Vipi.Ui/Pages</c>:
    /// la profondità della cartella di build cambia col TFM (net8/net10) e con la configurazione.
    /// </summary>
    private static string CartellaPagine()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidata = Path.Combine(dir.FullName, "src", "Vipi.Ui", "Pages");
            if (Directory.Exists(candidata)) return candidata;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("src/Vipi.Ui/Pages non trovata risalendo da " + AppContext.BaseDirectory);
    }
}
