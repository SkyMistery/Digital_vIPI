using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <b>Chi scrive con <c>ExecuteUpdate</c> salta la rotazione del token di concorrenza.</b>
///
/// <para><c>VipiDbContext.RuotaTokenDiConcorrenza</c> assegna un token nuovo a ogni entità versionata che
/// passa dal change-tracker: è la garanzia centrale della concorrenza ottimistica. <c>ExecuteUpdate</c> e
/// <c>ExecuteDelete</c> non passano di lì — vanno dritti al database — quindi una scrittura convertita a
/// quella forma smette di ruotare il token, e due editori possono sovrascriversi senza che nessuno se ne
/// accorga.</para>
///
/// <para>⚠️ Le uniche scritture così su un'entità versionata sono le <b>quattro del lock</b> su
/// <c>Document</c> (acquisizione, rinnovo, rilascio, sblocco forzato), e lì è la scelta <b>giusta</b>: il
/// battito del lock passa ogni pochi secondi e ruotare il token farebbe fallire il salvataggio di chi ha
/// l'editor aperto. Il lock ha un'esclusione sua, dentro la <c>WHERE</c>.</para>
///
/// <para>Fino al 7 settembre 2026 il commento del <c>DbContext</c> diceva un'altra cosa — «l'unica entità
/// che li usa non ha token» — e quella frase non descriveva: <b>autorizzava</b>. Chi la leggeva concludeva
/// che convertire una scrittura su <c>Document</c> fosse innocuo (revisione del 6 settembre 2026, R-022).
/// Questo test è la frase corretta messa dove non può invecchiare.</para>
/// </summary>
public class ScrittureCheSaltanoIlTokenTests
{
    /// <summary>Le tre entità con <c>RowVersion</c> marcato <c>IsConcurrencyToken</c> nel modello.</summary>
    private static readonly string[] Versionate = { "_db.Documents", "_db.DocumentSections", "_db.ContentBlocks" };

    private static readonly Regex Scrittura = new(@"Execute(Update|Delete)(Async)?\s*\(", RegexOptions.Compiled);

    /// <summary>Una firma di metodo: serve solo il NOME, per dire se è uno del lock.</summary>
    private static readonly Regex Firma = new(
        @"^\s*(?:public|private|internal|protected).*\s(?<nome>\w+)\s*\(", RegexOptions.Compiled);

    [Fact]
    public void Su_un_entita_versionata_ExecuteUpdate_si_usa_solo_per_il_lock()
    {
        var fuori = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(Radice(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

            var righe = File.ReadAllLines(file);
            for (var i = 0; i < righe.Length; i++)
            {
                if (!Scrittura.IsMatch(righe[i])) continue;

                // La query comincia qualche riga sopra: `_db.Documents.Where(...).ExecuteUpdateAsync(...)`
                // si scrive spesso su più righe.
                var finestra = string.Join(' ', righe.Skip(Math.Max(0, i - 8)).Take(Math.Min(9, i + 1)));
                if (!Versionate.Any(v => finestra.Contains(v, StringComparison.Ordinal))) continue;

                var metodo = NomeDelMetodo(righe, i);
                if (metodo.Contains("Lock", StringComparison.OrdinalIgnoreCase)) continue;

                fuori.Add($"{Path.GetRelativePath(Radice(), file)}:{i + 1} — in {metodo}()");
            }
        }

        Assert.True(fuori.Count == 0,
            "Scritture con ExecuteUpdate/ExecuteDelete su un'entità VERSIONATA, fuori dai metodi del lock:\n  " +
            string.Join("\n  ", fuori) +
            "\n\nQuelle scritture non passano da `RuotaTokenDiConcorrenza`: il token non ruota, e la " +
            "concorrenza ottimistica smette di accorgersi delle modifiche parallele. Le quattro del lock " +
            "sono l'eccezione, e per una ragione precisa (vedi il commento in VipiDbContext).");
    }

    /// <summary>Il nome del metodo che contiene la riga: si risale finché non si trova una firma.</summary>
    private static string NomeDelMetodo(string[] righe, int riga)
    {
        for (var i = riga; i >= 0; i--)
            if (Firma.Match(righe[i]) is { Success: true } m && m.Groups["nome"].Value is not ("if" or "while" or "for" or "foreach" or "switch" or "catch" or "using" or "lock"))
                return m.Groups["nome"].Value;
        return "(fuori da un metodo)";
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"radice del repository non trovata da {AppContext.BaseDirectory}");
    }
}
