using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// U-010 (revisione totale 3): una riga incompleta ferma il salvataggio dell'INTERA tabella, e «Fine modifica»
/// mollava il lock lo stesso. Le correzioni valide alle altre righe restavano nel buffer, la vista le mostrava
/// come salvate, e il primo ricarico le buttava. Riprodotto su LIRA il 26-set-2026: FL80 → FL90 perso in
/// silenzio con una riga nuova col solo QNH.
///
/// <para>⚠️ Salvare le righe complete saltando le altre non si può: i service sostituiscono la tabella intera, e
/// saltare una riga ESISTENTE che si sta riscrivendo la cancellerebbe (con le piste, anche quel che la cita).
/// Quindi: «Fine modifica» non esce finché una tabella è ferma, e dice quale.</para>
///
/// <para>⚠️ Guardie sul sorgente per l'ordine fra due cose (prima si chiede, poi si molla il lock), come in
/// <see cref="PisteNonSiPerdonoTests"/>: montare l'editor da solo direbbe che fa quel che il suo codice dice.</para>
/// </summary>
public class RigaIncompletaNonSiPerdeTests
{
    [Fact]
    public void Le_tabelle_ferme_sono_quelle_con_una_riga_a_meta()
    {
        var tls = new List<TlEdit> { new() { Id = 1, From = 1013, Level = "FL90" }, new() { From = 1030 } };
        var rwys = new List<RwEdit> { new() { Id = 1, Ident = "16" } };

        var ferme = AirportSaveGate.Ferme(tls, rwys, new List<RuleEdit>(), new List<SidEdit>(),
            new List<SidEdit> { new() { Name = "ERIKA1A" } });

        Assert.Equal(new[] { AirportSaveGate.TabellaLivelli, AirportSaveGate.TabellaStar }, ferme);
        Assert.Empty(AirportSaveGate.Ferme(new List<TlEdit>(), rwys, new List<RuleEdit>(),
            new List<SidEdit>(), new List<SidEdit>()));
    }

    [Theory]
    [InlineData("Components/Doc/AirportSectionsEditor.razor")]
    [InlineData("Components/Doc/MilSectionsEditor.razor")]
    public void Fine_modifica_non_esce_con_una_tabella_ferma(string file)
    {
        var s = Sorgente(file);
        var fine = s[s.IndexOf("private Task FineModifica()", StringComparison.Ordinal)..];
        fine = fine[..fine.IndexOf('}')];

        var chiede = fine.IndexOf("PercheResta", StringComparison.Ordinal);
        Assert.True(chiede >= 0, $"{file}: «Fine modifica» deve chiedere PercheResta prima di uscire.");
        Assert.Contains("AirportSaveGate.Ferme(", s);
    }

    /// <summary>Nei documenti uniti «Fine modifica» lo gestisce l'ospite per tutti: la domanda va fatta lì,
    /// PRIMA di mollare il primo lock, o la riga a metà di un membro si perde lo stesso.</summary>
    [Theory]
    [InlineData("Pages/AeroportoEditorPage.razor")]
    [InlineData("Pages/MilEditorPage.razor")]
    [InlineData("Pages/AppEditorPage.razor")]
    public void L_ospite_chiede_ai_membri_prima_di_mollare_i_lock(string file)
    {
        var s = Sorgente(file);
        var fine = s[s.IndexOf("private async Task FineModificaTutti()", StringComparison.Ordinal)..];
        var chiede = fine.IndexOf("PercheResta", StringComparison.Ordinal);
        var molla = fine.IndexOf("RilasciaLockAsync", StringComparison.Ordinal);

        Assert.True(chiede >= 0 && chiede < molla, $"{file}: PercheResta va chiesto prima di RilasciaLockAsync.");
    }

    private static string Sorgente(string relativo) => File.ReadAllText(Path.Combine(Radice(), relativo));

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
