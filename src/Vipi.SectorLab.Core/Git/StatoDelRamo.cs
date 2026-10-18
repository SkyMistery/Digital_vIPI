namespace Vipi.SectorLab.Core.Git;

/// <summary>Un file che git vede cambiato nella copia di lavoro: il percorso (dalla radice del clone) e come.</summary>
/// <param name="Percorso">Con le barre in avanti, come lo scrive git (<c>SectorFiles/Include/IT/symbols.sym</c>).</param>
/// <param name="Come">Cambiato, nuovo (non ancora in git), tolto, rinominato, in conflitto.</param>
public sealed record FileCambiato(string Percorso, string Come);

/// <summary>
/// Dove sta il sector nel suo repo git (carta F4, slice 1): il ramo, il remoto, quanto è avanti o indietro rispetto
/// al server, i file salvati e non ancora registrati. Si legge e basta: lo mostra la barra in alto.
/// </summary>
/// <param name="Radice">La cartella del clone (quella che contiene <c>.git</c>).</param>
/// <param name="Ramo">Il ramo di adesso; null se git è su un commit staccato.</param>
/// <param name="Remoto">L'indirizzo di <c>origin</c>, o null se il clone non ne ha.</param>
/// <param name="Monte">Il ramo del server che segue (<c>origin/master</c>), o null.</param>
/// <param name="Avanti">Commit qui che il server non ha.</param>
/// <param name="Indietro">Commit sul server che qui non ci sono (dall'ultimo <c>fetch</c>).</param>
/// <param name="Cambiati">I file cambiati e non registrati.</param>
public sealed record StatoDelRamo(
    string Radice,
    string? Ramo,
    string? Remoto,
    string? Monte,
    int Avanti,
    int Indietro,
    IReadOnlyList<FileCambiato> Cambiati)
{
    /// <summary>Niente da registrare e niente da mandare o da prendere.</summary>
    public bool Pulito => Cambiati.Count == 0 && Avanti == 0 && Indietro == 0;

    /// <summary>Lo stato in parole, per la barra: «pulito», «3 file da registrare · 1 da mandare»…</summary>
    public string InParole()
    {
        var pezzi = new List<string>();
        if (Cambiati.Count > 0)
            pezzi.Add(Cambiati.Count == 1 ? "1 file da registrare" : $"{Cambiati.Count} file da registrare");
        if (Avanti > 0)
            pezzi.Add(Avanti == 1 ? "1 registrazione da mandare" : $"{Avanti} registrazioni da mandare");
        if (Indietro > 0)
            pezzi.Add(Indietro == 1 ? "1 novità sul server" : $"{Indietro} novità sul server");
        if (Monte is null && Ramo is not null)
            pezzi.Add("mai mandato");
        return pezzi.Count == 0 ? "pulito" : string.Join(" · ", pezzi);
    }

    /// <summary>
    /// Legge <c>git status --porcelain=v2 --branch</c>: le righe <c># branch.*</c> e una riga per file (<c>1</c> cambiato,
    /// <c>2</c> rinominato, <c>u</c> in conflitto, <c>?</c> nuovo).
    /// </summary>
    public static StatoDelRamo DaStatus(string radice, string? remoto, IReadOnlyList<string> righe)
    {
        ArgumentNullException.ThrowIfNull(righe);
        string? ramo = null, monte = null;
        int avanti = 0, indietro = 0;
        var cambiati = new List<FileCambiato>();
        foreach (string riga in righe)
        {
            if (riga.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                string testa = riga["# branch.head ".Length..];
                ramo = testa == "(detached)" ? null : testa;
            }
            else if (riga.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                monte = riga["# branch.upstream ".Length..];
            }
            else if (riga.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                var ab = riga["# branch.ab ".Length..].Split(' ');
                avanti = int.Parse(ab[0].TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                indietro = int.Parse(ab[1].TrimStart('-'), System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (riga.StartsWith("1 ", StringComparison.Ordinal))
            {
                // 1 XY sub mH mI mW hH hI path
                var parti = riga.Split(' ', 9);
                cambiati.Add(new FileCambiato(parti[8], parti[1].Contains('D', StringComparison.Ordinal) ? "tolto" : "cambiato"));
            }
            else if (riga.StartsWith("2 ", StringComparison.Ordinal))
            {
                // 2 XY sub mH mI mW hH hI Xscore path<TAB>origPath
                var parti = riga.Split(' ', 10);
                cambiati.Add(new FileCambiato(parti[9].Split('\t')[0], "rinominato"));
            }
            else if (riga.StartsWith("u ", StringComparison.Ordinal))
            {
                var parti = riga.Split(' ', 11);
                cambiati.Add(new FileCambiato(parti[10], "in conflitto"));
            }
            else if (riga.StartsWith("? ", StringComparison.Ordinal))
            {
                cambiati.Add(new FileCambiato(riga[2..], "nuovo"));
            }
        }

        return new StatoDelRamo(radice, ramo, remoto, monte, avanti, indietro, cambiati);
    }
}

/// <summary>Legge lo stato del repo che contiene la cartella del sector (carta F4, slice 1).</summary>
public static class RepoDelSector
{
    /// <summary>
    /// Lo stato del repo, o null se la cartella non sta in un clone git (il Lab lavora lo stesso, senza i gesti di git).
    /// Con <paramref name="prendi"/> prima chiede al server le novità (<c>git fetch</c>): così «indietro» è vero.
    /// </summary>
    public static async Task<(StatoDelRamo? Stato, string? Perche)> LeggiAsync(Git git, string cartella, bool prendi = false,
                                                                             CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentException.ThrowIfNullOrEmpty(cartella);

        var radice = await git.EseguiAsync(cartella, ["rev-parse", "--show-toplevel"], annulla).ConfigureAwait(false);
        if (radice.Codice == -1)
            return (null, radice.Errore);
        if (!radice.Riuscito)
            return (null, "La cartella non sta in un clone git: i gesti di git non ci sono.");
        string cima = radice.Uscita.Trim();

        var remoto = await git.EseguiAsync(cima, ["remote", "get-url", "origin"], annulla).ConfigureAwait(false);
        string? indirizzo = remoto.Riuscito ? remoto.Uscita.Trim() : null;

        string? perche = null;
        if (prendi && indirizzo is not null)
        {
            var fetch = await git.EseguiAsync(cima, RamiDelSector.ChiediAlServer, annulla).ConfigureAwait(false);
            if (!fetch.Riuscito)
                perche = $"Il server non ha risposto: {fetch.Errore}";
        }

        var stato = await git.EseguiAsync(cima, ["status", "--porcelain=v2", "--branch", "--untracked-files=all"], annulla).ConfigureAwait(false);
        if (!stato.Riuscito)
            return (null, $"git status non va: {stato.Errore}");
        return (StatoDelRamo.DaStatus(cima, indirizzo, stato.Righe), perche);
    }
}
