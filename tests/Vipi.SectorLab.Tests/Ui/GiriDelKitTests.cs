using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// I giri del kit (carta «I giri di prova dal fork», P9): gli originali in <c>tools/kit-aod/giri/</c>, prima di andare sul
/// fork di prova. Ognuno si legge senza perché, gli id dei giri sono diversi, ogni compito che non è una domanda porta a una
/// pagina che c'è, e l'ultimo compito chiede che cosa è mancato.
/// </summary>
public sealed class GiriDelKitTests
{
    private static string CartellaDeiGiri()
    {
        var radice = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(AlberoDiProva.Campioni)))!;
        string cartella = Path.Combine(radice, "tools", "kit-aod", "giri");
        Assert.True(Directory.Exists(cartella), cartella);
        return cartella;
    }

    [Fact]
    public void IGiriDelKitSiLeggonoTutti()
    {
        var file = Directory.EnumerateFiles(CartellaDeiGiri(), "*.json").Order(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(file);

        var giri = new List<Giro>();
        foreach (string percorso in file)
        {
            string nome = Path.GetFileName(percorso);
            var giro = GiroDiProva.Leggi(nome, File.ReadAllText(percorso), out string? perche);
            Assert.True(giro is not null, $"{nome}: {perche}");
            Assert.Matches(@"^\d\d-[a-z0-9-]+\.json$", nome);
            Assert.All(giro.Compiti.Where(c => !c.SoloNota), c => Assert.True(c.Pagina is not null, $"{nome} {c.Id}: senza una pagina che c'è"));
            Assert.All(giro.Compiti, c => Assert.False(string.IsNullOrWhiteSpace(c.DovrestiVedere), $"{nome} {c.Id}: senza «dovresti»"));
            Assert.True(giro.Compiti[^1].SoloNota, $"{nome}: l'ultimo compito è la domanda «che cosa ti è mancato?»");
            giri.Add(giro);
        }

        Assert.Equal(giri.Count, giri.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count());
    }
}
