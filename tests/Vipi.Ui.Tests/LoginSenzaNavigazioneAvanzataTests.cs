using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Un clic su «Login» deve avviare UN login, non due.</b>
///
/// <para>Visto in produzione il 18 settembre 2026 (registro del giorno): <c>/services/vsop/auth/login</c> due volte a
/// 0,3 s, poi il ritorno da IVAO fallisce per «nonce» — la pagina «accesso non riuscito» — e al ricarico si è
/// dentro. La navigazione avanzata di Blazor intercetta il link, fa una fetch dell'endpoint, riceve il rimando a
/// IVAO (un altro dominio) e ripete la richiesta a pagina piena: due avvii del giro, ognuno col suo nonce e il suo
/// cookie. Dalla pagina d'errore, che è HTML semplice, il secondo tentativo partiva una volta sola e riusciva.</para>
///
/// <para><b>Perché sul SORGENTE e su tutti i file.</b> Il difetto non è di un link: è di OGNI link verso un endpoint
/// che rimanda fuori dal sito. Il test prende quello che nascerà domani in un altro layout.</para>
/// </summary>
public sealed class LoginSenzaNavigazioneAvanzataTests
{
    private static readonly Regex Ancora = new(@"<a\b[^>]*>", RegexOptions.Singleline);

    [Fact]
    public void Ogni_link_di_login_e_logout_esce_dalla_navigazione_avanzata()
    {
        var sep = Path.DirectorySeparatorChar;
        var trovati = 0;
        var senza = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Radice(), "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}")))
        {
            foreach (Match m in Ancora.Matches(File.ReadAllText(file)))
            {
                if (!m.Value.Contains("/auth/login", StringComparison.Ordinal)
                    && !m.Value.Contains("/auth/logout", StringComparison.Ordinal)) continue;

                trovati++;
                if (!m.Value.Contains("data-enhance-nav=\"false\"", StringComparison.Ordinal))
                    senza.Add($"{Path.GetFileName(file)}: {m.Value}");
            }
        }

        // Quattro oggi (login e logout, nel menù «☰» e nella barra): se il conto scende a zero il test non guarda
        // più niente, e passerebbe per assenza.
        Assert.True(trovati >= 4, $"trovati solo {trovati} link di login/logout: la ricerca non li vede più");
        Assert.True(senza.Count == 0,
            "link di login/logout SENZA data-enhance-nav=\"false\" — un clic avvierebbe due login:\n  "
            + string.Join("\n  ", senza));
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
