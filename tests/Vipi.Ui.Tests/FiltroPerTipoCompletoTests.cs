using Vipi.Application.Routing;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il menu «Tipo» di <c>/services/vsop/versions</c> deve offrire <b>tutte</b> le famiglie di documento che
/// esistono davvero — e <b>solo</b> quelle.
///
/// <para>⚠️ <b>Il difetto che presidia è già capitato.</b> Quando è nato il vSOP militare, l'elenco imparò a
/// mostrarlo — icona, etichetta, riga — ma il <b>filtro</b> restò ai quattro tipi civili: i documenti
/// militari si vedevano e non si potevano isolare, in una pagina che serve proprio a isolare. Nessun
/// compilatore lega un elenco di filtri all'enum che filtra, e a schermo la mancanza non salta all'occhio:
/// un menu con cinque voci sembra completo quanto uno con sei.</para>
///
/// <para>⚠️ <b>Chi «esiste davvero» lo dice il descrittore di rotta, non un elenco scritto qui.</b> Una
/// famiglia con un <c>PublicUrl</c> ha una pagina pubblica, quindi può avere documenti da filtrare; una che
/// torna <c>null</c> — oggi l'APP militare — <b>non ha nemmeno una porta che li crea</b>, e una voce di
/// filtro che non può che dare zero righe è una promessa falsa. È lo stesso criterio, e la stessa lezione,
/// di <see cref="RotteDeiDocumentiEsistonoTests"/>.</para>
/// </summary>
public class FiltroPerTipoCompletoTests
{
    private static readonly IDocKindRoutes[] Descrittori =
    {
        new VloaDocRoutes(), new AppDocRoutes(), new AccVipiDocRoutes(),
        new AirportDocRoutes(), new AirportMilDocRoutes(), new AppMilDocRoutes(),
    };

    /// <summary>Il solo corpo dell'inizializzatore di <c>KindFilters</c>: il resto della pagina nomina i tipi
    /// per mille altre ragioni, e cercarli ovunque farebbe passare un filtro che non c'è.</summary>
    private static string CorpoDelFiltro()
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), "Pages", "VersioniPage.razor"));
        var inizio = sorgente.IndexOf("KindFilters", StringComparison.Ordinal);
        Assert.True(inizio > 0, "KindFilters non trovato in VersioniPage.razor");

        var fine = sorgente.IndexOf("};", inizio, StringComparison.Ordinal);
        Assert.True(fine > inizio, "l'inizializzatore di KindFilters non si chiude");
        return sorgente[inizio..fine];
    }

    public static TheoryData<string, bool> Famiglie()
    {
        var dati = new TheoryData<string, bool>();
        foreach (var d in Descrittori)
            dati.Add(d.Target.ToString(), d.PublicUrl("lirr", "LIRF", "LIMM") is not null);
        return dati;
    }

    [Theory]
    [MemberData(nameof(Famiglie))]
    public void Il_filtro_per_tipo_offre_le_famiglie_che_esistono_e_solo_quelle(string tipo, bool haPagine)
    {
        var corpo = CorpoDelFiltro();
        var citato = corpo.Contains($"ReleaseTargetType.{tipo}", StringComparison.Ordinal);

        if (haPagine)
            Assert.True(citato, $"«{tipo}» ha pagine pubbliche ma non compare fra i filtri di VersioniPage.");
        else
            Assert.False(citato, $"«{tipo}» non ha pagine: un filtro per lui non puo' che dare zero righe.");
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
