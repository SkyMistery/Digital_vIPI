using Vipi.Application.Content;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// §S2 del filone sito (23 settembre 2026): dopo «Hide» nella scheda delle sezioni comuni l'ospite ricaricava
/// solo sé stesso, e i membri restavano con le sezioni nascoste a schermo. Non si poteva ricaricarli perché
/// l'elenco dei loro editor si aggiungeva e basta: un membro tolto restava dentro, con lo scope già chiuso.
/// </summary>
public class RegistroMembriTests
{
    private sealed class Finto(string nome) : IMembroEditor
    {
        public string Titolo => nome;
        public IReadOnlyList<EditableSection> Sezioni => Array.Empty<EditableSection>();
        public DocumentEditorShell Guscio => throw new NotSupportedException("Il registro non tocca il guscio.");
        public Task<string?> PrendiLockAsync() => Task.FromResult<string?>(null);
        public Task RilasciaLockAsync() => Task.CompletedTask;
        public Task RicaricaAsync() => Task.CompletedTask;
        public override string ToString() => nome;
    }

    [Fact]
    public void Un_membro_TOLTO_esce_dall_elenco()
    {
        var r = new RegistroMembri();
        var a = new Finto("A");
        var b = new Finto("B");
        r.Registra(3, a);
        r.Registra(5, b);

        Assert.True(r.TieniSolo(new[] { 5 }));

        Assert.Equal(new IMembroEditor[] { b }, r.Editor);
    }

    [Fact]
    public void Si_ricaricano_solo_i_membri_di_ADESSO()
    {
        // L'ospite rilegge l'unione PRIMA che questo registro si aggiorni al ridisegno: il membro appena tolto
        // è ancora registrato, e non va toccato — sta per essere smontato.
        var r = new RegistroMembri();
        var a = new Finto("A");
        var b = new Finto("B");
        r.Registra(3, a);
        r.Registra(5, b);

        Assert.Equal(new IMembroEditor[] { b }, r.Di(new[] { 5 }));
        Assert.Empty(r.Di(Array.Empty<int>()));
    }

    [Fact]
    public void Un_editor_NUOVO_per_lo_stesso_documento_prende_il_posto_del_vecchio()
    {
        var r = new RegistroMembri();
        var vecchio = new Finto("vecchio");
        var nuovo = new Finto("nuovo");
        r.Registra(3, vecchio);
        r.Registra(5, new Finto("B"));

        Assert.True(r.Registra(3, nuovo));

        Assert.Equal(2, r.Editor.Count);
        Assert.Same(nuovo, r.Editor[0]);
    }

    [Fact]
    public void Registrarsi_DUE_volte_non_cambia_niente()
    {
        var r = new RegistroMembri();
        var a = new Finto("A");
        Assert.True(r.Registra(3, a));

        Assert.False(r.Registra(3, a));

        Assert.Single(r.Editor);
    }

    [Fact]
    public void L_elenco_segue_l_ORDINE_dell_unione()
    {
        // Dopo «Sposta» l'indice unito deve seguire l'unione, non l'ordine in cui i membri si sono presentati.
        var r = new RegistroMembri();
        var a = new Finto("A");
        var b = new Finto("B");
        r.Registra(3, a);
        r.Registra(5, b);

        Assert.True(r.TieniSolo(new[] { 5, 3 }));
        Assert.Equal(new IMembroEditor[] { b, a }, r.Editor);

        Assert.False(r.TieniSolo(new[] { 5, 3 }));
    }

    /// <summary>
    /// Le tre pagine ospite ricaricano i MEMBRI dopo un gesto sull'unione, con l'insieme dei membri appena
    /// riletto. ⚠️ Si guarda il sorgente: il ricarico passa da componenti con scope e database propri.
    /// </summary>
    [Theory]
    [InlineData("Pages/AppEditorPage.razor")]
    [InlineData("Pages/AeroportoEditorPage.razor")]
    [InlineData("Pages/MilEditorPage.razor")]
    public void Dopo_un_gesto_sull_unione_si_ricaricano_anche_i_membri(string relativo)
    {
        var sorgente = Leggi(relativo);
        var inizio = sorgente.IndexOf("private async Task UnioneCambiata()", StringComparison.Ordinal);
        Assert.True(inizio >= 0, $"{relativo}: UnioneCambiata non trovata.");
        var fine = sorgente.IndexOf("\n    }", inizio, StringComparison.Ordinal);
        var corpo = sorgente[inizio..fine];

        var rilegge = corpo.IndexOf("await LeggiUnioneAsync();", StringComparison.Ordinal);
        var membri = corpo.IndexOf("await _membri.RicaricaAsync(_altri.Select(a => a.DocumentId)", StringComparison.Ordinal);
        Assert.True(membri >= 0, $"{relativo}: dopo un gesto sull'unione i membri non si ricaricano.");
        Assert.True(rilegge >= 0 && rilegge < membri,
            $"{relativo}: i membri si ricaricano PRIMA di rileggere l'unione — l'insieme sarebbe quello vecchio.");
    }

    /// <summary>Senza <c>@key</c> sul documento, dopo «Sposta» un componente riceve i parametri dell'altro
    /// membro e resta registrato sotto il documento sbagliato.</summary>
    [Fact]
    public void Ogni_membro_e_legato_al_SUO_documento()
    {
        var sorgente = Leggi("Components/Doc/UnionMembersEditor.razor");
        Assert.Contains("<section @key=\"m.DocumentId\"", sorgente, StringComparison.Ordinal);
        Assert.DoesNotContain("Registrati=\"Registra\"", sorgente, StringComparison.Ordinal);
    }

    private static string Leggi(string relativo) =>
        File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

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
