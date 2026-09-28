using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// I gesti degli editor dello scalo (vSOP militare e vIPI d'aeroporto) che restavano fuori dal guardiano, o che
/// decidevano DENTRO la fila su che cosa agire (revisione totale 3, fetta D di L11).
///
/// <para>⚠️ <b>Perché un presidio sul testo.</b> Nessun test monta <c>MilSectionsEditor</c> né
/// <c>AirportSectionsEditor</c>: tirano una dozzina di servizi, il documento, il lock e il tornello. Il guardiano
/// (<c>DocumentEditorShell.GuardAsync/GuardedAsync</c>) ha i suoi test di comportamento in
/// <c>DocumentEditorShellTests</c>; qui si tiene fermo che questi gesti ci passino, e come.</para>
/// </summary>
public class GestiDegliEditorDelloScaloTests
{
    /// <summary>
    /// 🔴 U-066: la ✕ di una radioassistenza passa l'INDICE, e l'elenco si leggeva dentro la fila. Un doppio clic
    /// arrivava col secondo gesto in coda: quando toccava a lui l'elenco era già senza la prima riga, e lo stesso
    /// indice toglieva la riga DOPO. Lo stesso per «sposta». La riga si sceglie al clic, per chiave.
    /// </summary>
    [Theory]
    [InlineData("TogliRadioassistenza")]
    [InlineData("SpostaRadioassistenza")]
    public void La_riga_della_radioassistenza_si_sceglie_al_clic_per_chiave(string gestore)
    {
        var corpo = Corpo(Leggi("Components/Doc/MilSectionsEditor.razor"), gestore);
        var lettura = corpo.IndexOf("ChiaviCorrenti()", StringComparison.Ordinal);
        var fila = corpo.IndexOf("_shell.GuardAsync(", StringComparison.Ordinal);

        Assert.True(lettura >= 0 && fila > 0 && lettura < fila,
            $"{gestore}: la riga va scelta al clic, PRIMA di mettersi in fila");
        Assert.DoesNotContain("RemoveAt(indice)", corpo);
    }

    /// <summary>
    /// 🔴 U-163: «+ Alternato» chiamava la sorgente IVAO fuori dal guardiano: un timeout o una risposta illeggibile
    /// abbattevano il circuito. E l'avviso «ICAO sconosciuto», scritto prima del salvataggio, lo cancellava il
    /// guardiano stesso all'ingresso: non si vedeva mai.
    /// </summary>
    [Fact]
    public void Aggiungere_un_alternato_cerca_il_nome_dentro_il_guardiano_e_avvisa_dopo()
    {
        var corpo = Corpo(Leggi("Components/Doc/MilSectionsEditor.razor"), "AggiungiAlternato");
        var guardiano = corpo.IndexOf("_shell.GuardAsync(", StringComparison.Ordinal);
        var ricerca = corpo.IndexOf("Aeroporti.FindAsync(", StringComparison.Ordinal);
        var salvataggio = corpo.IndexOf("SaveDiversionsAsync(", StringComparison.Ordinal);
        var avviso = corpo.IndexOf("Div_UnknownIcao", StringComparison.Ordinal);

        Assert.True(guardiano >= 0 && ricerca > guardiano, "la ricerca del nome va DENTRO il guardiano");
        Assert.True(salvataggio > 0 && avviso > salvataggio, "l'avviso va scritto DOPO il salvataggio");
    }

    /// <summary>
    /// 🔴 U-164: la scrittura di un campo di radioassistenza era in fila ma fuori dal guardiano: un'eccezione del
    /// servizio (il database, un permesso scaduto) usciva dal gestore della cella.
    /// </summary>
    [Fact]
    public void Scrivere_un_campo_di_radioassistenza_passa_dal_guardiano()
    {
        var corpo = Corpo(Leggi("Components/Doc/MilSectionsEditor.razor"), "ScriviRadioassistenza");
        Assert.Contains("_shell.GuardedAsync(", corpo);
        Assert.DoesNotContain("_shell.InFilaAsync(", corpo);
    }

    /// <summary>
    /// 🔴 U-165: «Crea vSOP militare» dall'editor d'aeroporto non guardava <c>_busy</c>, stava fuori dal tornello e
    /// prendeva due soli tipi di eccezione.
    /// </summary>
    [Fact]
    public void Creare_il_vSOP_militare_passa_dal_guardiano_con_la_sentinella()
    {
        var corpo = Corpo(Leggi("Components/Doc/AirportSectionsEditor.razor"), "CreaVsopMilitare");
        var guardia = corpo.IndexOf("if (_busy) return;", StringComparison.Ordinal);
        var attesa = corpo.IndexOf("await ", StringComparison.Ordinal);

        Assert.True(guardia >= 0 && guardia < attesa, "la sentinella va PRIMA del primo await");
        Assert.Matches(@"Guarded\(\(\) => Militari\.CreaAsync\(_icao\)\)", corpo);
    }

    private static string Corpo(string sorgente, string metodo)
    {
        var m = Regex.Match(sorgente,
            $@"private (async )?Task(<[^>]+>)? {metodo}\([^)]*\)(?<c>.*?)\n    (\}}|\}}\);)\n",
            RegexOptions.Singleline);
        Assert.True(m.Success, $"{metodo} non trovato");
        return m.Groups["c"].Value;
    }

    private static string Leggi(string relativo) =>
        File.ReadAllText(Path.Combine(Radice(), relativo)).Replace("\r\n", "\n");

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(AppContext.BaseDirectory);
    }
}
