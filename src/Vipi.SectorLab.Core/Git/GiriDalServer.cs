namespace Vipi.SectorLab.Core.Git;

/// <summary>
/// I giri di prova sul server del sector (carta «I giri di prova dal fork», 3 novembre 2026, P1-P2): i file
/// <c>giri/*.json</c> del ramo predefinito, letti da git e non dal web — vale anche per un repo privato, usa le credenziali
/// di sempre, e la cartella non scende sul disco (il clone del sector resta parziale, solo <c>SectorFiles</c>).
/// </summary>
public static class GiriDalServer
{
    /// <summary>La cartella dei giri, alla radice del repo del sector.</summary>
    public const string Cartella = "giri";

    /// <summary>Un file di un giro com'è sul server: il nome (<c>02-ground.json</c>) e il testo.</summary>
    public sealed record FileDelGiro(string Nome, string Testo);

    /// <summary>
    /// I file dei giri su <c>origin/&lt;ramo&gt;</c>, in ordine di nome. Con <paramref name="prendi"/> prima chiede le
    /// novità al server (<c>git fetch</c>). <c>Perche</c> dice che cosa non è andato (il server che non risponde, il ramo
    /// che non c'è, un file che non si legge); i file letti tornano lo stesso. Un ramo senza la cartella torna vuoto, senza
    /// perché: il server non ha giri.
    /// </summary>
    public static async Task<(IReadOnlyList<FileDelGiro> Giri, string? Perche)> LeggiAsync(Git git, string radice, string ramo,
        bool prendi, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentException.ThrowIfNullOrEmpty(radice);
        ArgumentException.ThrowIfNullOrEmpty(ramo);

        string? perche = null;
        if (prendi)
        {
            var fetch = await git.EseguiAsync(radice, RamiDelSector.ChiediAlServer, annulla).ConfigureAwait(false);
            if (!fetch.Riuscito)
                perche = $"Il server non ha risposto: {fetch.Errore}";
        }

        string riferimento = $"origin/{ramo}";
        var elenco = await git.EseguiAsync(radice, ["ls-tree", "--name-only", riferimento, "--", Cartella + "/"], annulla).ConfigureAwait(false);
        if (!elenco.Riuscito)
            return ([], perche ?? $"Il ramo {riferimento} non si legge: {elenco.Errore}");

        var giri = new List<FileDelGiro>();
        foreach (string percorso in elenco.Righe
                     .Where(r => r.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            var file = await git.EseguiAsync(radice, ["show", $"{riferimento}:{percorso}"], annulla).ConfigureAwait(false);
            if (file.Riuscito)
                giri.Add(new FileDelGiro(percorso[(percorso.LastIndexOf('/') + 1)..], file.Uscita));
            else
                perche ??= $"{percorso} non si legge: {file.Errore}";
        }

        return (giri, perche);
    }
}
