namespace Vipi.SectorLab.Core.Aurora;

/// <summary>Una registrazione (un commit) letta per il lotto: il commit, i gesti del messaggio, i file che ha cambiato.</summary>
public sealed record RegistrazioneNelLotto(string Commit, IReadOnlyList<string> Gesti, IReadOnlyList<string> File);

/// <summary>
/// Il lotto dalle registrazioni (carta F5, slice 5): quando il lavoro arriva da git — fatto su un altro PC, da un altro
/// AOD, o salvato prima di riaprire il Lab — il lotto non ha i gesti salvati qui. Li legge allora dai commit fra quello che
/// era in vetrina e quello di adesso: il messaggio di «Registra» ha un gesto per riga (<c>- …</c>), o uno solo nel titolo.
/// </summary>
public static class Registrazioni
{
    /// <summary>
    /// Le registrazioni da <paramref name="da"/> (escluso) a <c>HEAD</c>, dalla più vecchia, senza le fusioni (i commit
    /// che una fusione porta ci sono uno per uno). Null se <paramref name="da"/> non sta nella storia di <c>HEAD</c>: allora
    /// il Lab non sa dire che cosa è cambiato, e va rivisto tutto.
    /// </summary>
    public static async Task<IReadOnlyList<RegistrazioneNelLotto>?> FraAsync(Git.Git git, string radice, string da, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var antenato = await git.EseguiAsync(radice, ["merge-base", "--is-ancestor", da, "HEAD"], annulla).ConfigureAwait(false);
        if (!antenato.Riuscito)
            return null;
        var log = await git.EseguiAsync(radice, ["log", "--no-merges", "--reverse", "--format=%x1e%h%x1f%s%x1f%b%x1f", "--name-only", $"{da}..HEAD"], annulla).ConfigureAwait(false);
        return log.Riuscito ? Leggi(log.Uscita) : null;
    }

    /// <summary>L'uscita di <c>git log</c> nel formato di <see cref="FraAsync"/>.</summary>
    public static IReadOnlyList<RegistrazioneNelLotto> Leggi(string uscita)
    {
        ArgumentNullException.ThrowIfNull(uscita);
        var lette = new List<RegistrazioneNelLotto>();
        foreach (string pezzo in uscita.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
        {
            var campi = pezzo.Split('\x1f');
            if (campi.Length < 4)
                continue;
            var file = campi[3].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(f => f.StartsWith("SectorFiles/", StringComparison.Ordinal)).ToList();
            lette.Add(new RegistrazioneNelLotto(campi[0].Trim(), Gesti(campi[1].Trim(), campi[2]), file));
        }

        return lette;
    }

    /// <summary>I gesti di un messaggio: le righe <c>- …</c> del corpo (come le scrive «Registra»), o il titolo.</summary>
    public static IReadOnlyList<string> Gesti(string titolo, string corpo)
    {
        ArgumentNullException.ThrowIfNull(corpo);
        var righe = corpo.Split('\n').Select(r => r.Trim()).Where(r => r.StartsWith("- ", StringComparison.Ordinal)).Select(r => r[2..].Trim()).Where(r => r.Length > 0).ToList();
        return righe.Count > 0 ? righe : [titolo];
    }
}
