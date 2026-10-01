using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Chi decide la lingua di una pagina che mostra <b>due documenti</b> (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §3).
///
/// <para>
/// 🔴 <b>Ogni documento la sua regola.</b> Il caso vero è LIRP (1° ottobre 2026): la vIPI bilingue e il vSOP
/// bloccato in inglese, uniti. Fino a quel giorno la lingua «della pagina» era una sola, quella della porta, e
/// il membro ne prendeva etichette, intestazioni e prosa generata: il vSOP inglese con le tabelle in italiano,
/// o — entrando dal vSOP — la vIPI tradotta in italiano con le intestazioni in inglese. Ora il caricamento di
/// un membro sta in un blocco che rimette la lingua della porta alla chiusura, e il suo corpo riceve la sua
/// lingua per cascata.
/// </para>
/// </summary>
public class LinguaDellaPaginaUnitaTests : TestContext
{
    // ---- Il caricamento: la lingua del membro dentro il blocco, quella della porta fuori -----------------

    [Fact]
    public void Un_documento_SOLO_e_bloccato_impone_la_sua_lingua_alla_pagina()
    {
        var ctx = new ReadingLanguageContext();

        var lettore = LinguaDelDocumento.Prepara(ctx, bloccato: true, Language.En, Language.It);

        Assert.Equal("en", lettore);
        Assert.Equal("en", ctx.Fissata);
    }

    /// <summary>
    /// 🔴 Quel che fa <c>UnionLoader</c> per ogni membro: un blocco che parte dalla lingua di chi legge. Dentro,
    /// il membro bloccato impone la sua (le derivate la vedono); alla chiusura la pagina torna della porta.
    /// </summary>
    [Fact]
    public void Un_MEMBRO_bloccato_decide_la_sua_lingua_DENTRO_il_blocco_e_la_restituisce_alla_porta()
    {
        var ctx = new ReadingLanguageContext();
        LinguaDelDocumento.Prepara(ctx, bloccato: false, Language.It, Language.It);   // porta bilingue

        using (ctx.Rendering(LinguaDiLettura.DelLettore()))
        {
            var membro = LinguaDelDocumento.Prepara(ctx, bloccato: true, Language.En, Language.It);
            Assert.Equal("en", membro);
            Assert.Equal("en", ctx.Corrente);
        }

        Assert.Null(ctx.Fissata);
    }

    /// <summary>
    /// 🔴 Il verso opposto, quello che prima nessuno guardava: la porta è bloccata, il membro è bilingue. Dentro
    /// il suo blocco il membro segue <b>chi legge</b>, non la porta — e la porta riprende la sua alla chiusura.
    /// </summary>
    [Fact]
    public void Un_MEMBRO_bilingue_sotto_una_porta_bloccata_segue_CHI_LEGGE()
    {
        var ctx = new ReadingLanguageContext();
        var lettore = LinguaDiLettura.DelLettore();
        var altra = lettore == "en" ? Language.It : Language.En;
        var codiceAltra = altra == Language.En ? "en" : "it";
        LinguaDelDocumento.Prepara(ctx, bloccato: true, altra, Language.It);           // porta bloccata

        using (ctx.Rendering(lettore))
        {
            var membro = LinguaDelDocumento.Prepara(ctx, bloccato: false, Language.It, Language.It);
            Assert.Equal(lettore, membro);
            Assert.Equal(lettore, ctx.Corrente);
        }

        Assert.Equal(codiceAltra, ctx.Fissata);
    }

    // ---- Il disegno: le etichette del corpo nella lingua del membro ----------------------------------------

    /// <summary>Il localizzatore di base: risponde con la chiave, così si vede quando NON si è imposta una
    /// lingua.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Monta la pagina com'è in produzione: il localizzatore iniettato è quello che segue la lingua
    /// imposta dalla porta, e chi legge parla <paramref name="lettore"/>.</summary>
    private ReadingLanguageContext Pagina(string lettore, string? portaBloccata)
    {
        var ctx = new ReadingLanguageContext();
        if (portaBloccata is not null) ctx.Fissa(portaBloccata);
        Services.AddSingleton(ctx);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new LocalizzatoreDiLingua(new KeyLocalizer(), ctx));
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(lettore);
        return ctx;
    }

    private IRenderedFragment Badge(string? linguaDelMembro) =>
        linguaDelMembro is null
            ? RenderComponent<AudienceBadge>(p => p.Add(b => b.Audience, SectionAudience.Pilots))
            : Render(b =>
            {
                b.OpenComponent<CascadingValue<string>>(0);
                b.AddComponentParameter(1, "Name", ComponenteDelDocumento.Cascata);
                b.AddComponentParameter(2, "Value", linguaDelMembro);
                b.AddComponentParameter(3, "IsFixed", true);
                b.AddComponentParameter(4, "ChildContent", (RenderFragment)(c =>
                {
                    c.OpenComponent<AudienceBadge>(0);
                    c.AddComponentParameter(1, nameof(AudienceBadge.Audience), SectionAudience.Pilots);
                    c.CloseComponent();
                }));
                b.CloseComponent();
            });

    /// <summary>🔴 LIRP entrando dalla vIPI: porta bilingue letta in italiano, membro vSOP bloccato in inglese.
    /// Prima l'etichetta del membro seguiva la porta.</summary>
    [Fact]
    public void Membro_bloccato_in_inglese_sotto_una_porta_bilingue_ha_le_etichette_INGLESI()
    {
        var originale = CultureInfo.CurrentUICulture;
        try
        {
            Pagina(lettore: "it", portaBloccata: null);

            var cut = Badge(linguaDelMembro: "en");

            Assert.Equal("pilots only", cut.Find(".aud-badge").TextContent.Trim());
        }
        finally { CultureInfo.CurrentUICulture = originale; }
    }

    /// <summary>🔴 LIRP entrando dal vSOP: porta bloccata in inglese, lettore italiano, membro vIPI bilingue.
    /// Prima il membro aveva il testo tradotto in italiano e le etichette in inglese.</summary>
    [Fact]
    public void Membro_bilingue_sotto_una_porta_bloccata_ha_le_etichette_di_CHI_LEGGE()
    {
        var originale = CultureInfo.CurrentUICulture;
        try
        {
            Pagina(lettore: "it", portaBloccata: "en");

            var cut = Badge(linguaDelMembro: "it");

            Assert.Equal("solo piloti", cut.Find(".aud-badge").TextContent.Trim());
        }
        finally { CultureInfo.CurrentUICulture = originale; }
    }

    /// <summary>⚠️ E senza cascata non cambia niente: il corpo della PORTA segue la regola di sempre.</summary>
    [Fact]
    public void Senza_cascata_vale_la_lingua_imposta_dalla_pagina()
    {
        var originale = CultureInfo.CurrentUICulture;
        try
        {
            Pagina(lettore: "it", portaBloccata: "en");

            var cut = Badge(linguaDelMembro: null);

            Assert.Equal("pilots only", cut.Find(".aud-badge").TextContent.Trim());
        }
        finally { CultureInfo.CurrentUICulture = originale; }
    }

    // ---- La rete: nessun componente del corpo torna al localizzatore della pagina -------------------------

    /// <summary>
    /// 🔴 Un componente nuovo dentro il corpo di un documento che scrive <c>@inject IStringLocalizer L</c>
    /// compila, funziona da solo e su una pagina unita torna a prendere le etichette della porta — senza
    /// errore. Si parte dai tre corpi e si scende per i tag: ogni componente raggiunto che usa <c>L</c> deve
    /// ereditare <see cref="ComponenteDelDocumento"/>, o prenderne la regola (<c>Scegli</c>). Le isole
    /// interattive no: la cascata non attraversa il circuito, e la lingua la ricevono per parametro.
    /// </summary>
    [Fact]
    public void Ogni_componente_dentro_un_corpo_legge_la_lingua_del_SUO_documento()
    {
        var radice = Path.Combine(Radice(), "src", "Vipi.Ui");
        var componenti = Directory.EnumerateFiles(radice, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .GroupBy(f => Path.GetFileNameWithoutExtension(f))
            .ToDictionary(g => g.Key, g => File.ReadAllText(g.First()));

        var visti = new HashSet<string>();
        var daVedere = new Stack<string>(new[] { "AppDocumentBody", "AirportDocumentBody", "MilDocumentBody" });
        var fuori = new List<string>();
        while (daVedere.Count > 0)
        {
            var nome = daVedere.Pop();
            if (!visti.Add(nome) || !componenti.TryGetValue(nome, out var sorgente)) continue;
            if (Regex.IsMatch(sorgente, @"^@rendermode\b", RegexOptions.Multiline)) continue;   // isola

            if (Regex.IsMatch(sorgente, @"^@inject\s+IStringLocalizer<SharedResource>\s+L\s*$", RegexOptions.Multiline))
                fuori.Add(nome);

            foreach (Match m in Regex.Matches(sorgente, @"<([A-Z][A-Za-z0-9]+)[\s/>]"))
                if (componenti.ContainsKey(m.Groups[1].Value)) daVedere.Push(m.Groups[1].Value);
        }

        Assert.True(visti.Count > 20, $"La discesa dai corpi ha trovato solo {visti.Count} componenti: la rete è rotta.");
        Assert.True(fuori.Count == 0,
            "Questi componenti stanno dentro il corpo di un documento ma leggono le etichette dalla pagina: "
            + string.Join(", ", fuori) + ". Usare `@inherits ComponenteDelDocumento` al posto di `@inject … L`.");
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Vipi.Ui")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Radice del repo non trovata.");
    }
}
