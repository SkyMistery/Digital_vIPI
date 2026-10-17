namespace Vipi.SectorLab.Core.Git;

/// <summary>
/// «Registra» (carta F4, slice 4): il commit dei file salvati, quando lo chiede l'AOD (D5), col messaggio proposto
/// dalle modifiche e corretto da lui, e coi confini della carta madre controllati prima.
/// <para>🔴 Confini: solo <c>SectorFiles/Include/IT/**</c> e <c>SectorFiles/delete.upd</c> (C4); niente binari, niente
/// <c>Aurora.exe</c>, DLL, suoni, l'updater (§2.1). Un file fuori dai confini <b>non si registra mai</b> e si mostra
/// («resta fuori»): non ferma il resto, perché un clone che è anche la cartella di Aurora ne ha sempre (scelta della
/// slice 4, scritta nella traccia). Un binario dentro i confini ferma tutto. E mai su <c>master</c> né su un
/// <c>airac/</c>: ci si arriva con una PR (§3).</para>
/// </summary>
public static class Registrazione
{
    /// <summary>La cartella del sector italiano, dalla radice del repo.</summary>
    public const string CartellaDelSector = "SectorFiles/Include/IT/";

    /// <summary>Il file dei file da cancellare nei client (C4), fuori da <c>IT</c>.</summary>
    public const string FileDaCancellare = "SectorFiles/delete.upd";

    /// <summary>
    /// Vero se il Lab può registrare quel file (percorso dalla radice del repo, con le barre in avanti): la cartella del
    /// sector, <c>delete.upd</c> e, dalla slice 7 (C4: togliere un file toglie la sua riga dagli <c>.isc</c>), gli <c>.isc</c>
    /// di <c>SectorFiles/</c>.
    /// </summary>
    public static bool DentroIConfini(string percorso)
    {
        ArgumentNullException.ThrowIfNull(percorso);
        return percorso.StartsWith(CartellaDelSector, StringComparison.Ordinal) || percorso == FileDaCancellare
               || (percorso.StartsWith("SectorFiles/", StringComparison.Ordinal) && percorso.Count(c => c == '/') == 1
                   && percorso.EndsWith(".isc", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>I file cambiati che il Lab registrerebbe: quelli dentro i confini.</summary>
    public static IReadOnlyList<FileCambiato> DaRegistrare(StatoDelRamo stato)
    {
        ArgumentNullException.ThrowIfNull(stato);
        return [.. stato.Cambiati.Where(c => DentroIConfini(c.Percorso))];
    }

    /// <summary>I file cambiati fuori dai confini: restano come sono, il Lab non li registra.</summary>
    public static IReadOnlyList<FileCambiato> Fuori(StatoDelRamo stato)
    {
        ArgumentNullException.ThrowIfNull(stato);
        return [.. stato.Cambiati.Where(c => !DentroIConfini(c.Percorso))];
    }

    /// <summary>Perché adesso non si registra, o null.</summary>
    public static string? PercheNonSiRegistra(StatoDelRamo stato, string predefinito)
    {
        ArgumentNullException.ThrowIfNull(stato);
        if (stato.Ramo is null)
            return "git è su un commit staccato: passa a un ramo di lavoro prima di registrare.";
        if (stato.Ramo == predefinito || stato.Ramo.StartsWith(RamiDelSector.PrefissoDelCiclo, StringComparison.Ordinal))
            return $"Il Lab non registra su {stato.Ramo}: ci si arriva con una PR. Fai un lavoro nuovo (il riquadro del ramo) e registra lì.";
        if (stato.Cambiati.FirstOrDefault(c => c.Come == "in conflitto") is { } conflitto)
            return $"{conflitto.Percorso} è in conflitto: va risolto prima.";
        var dentro = DaRegistrare(stato);
        if (dentro.Count == 0)
            return "Non c'è niente da registrare: i file del sector salvati sono già tutti registrati.";
        var binari = dentro.Where(c => c.Come != "tolto" && Binario(Path.Combine(stato.Radice, c.Percorso))).Select(c => c.Percorso).ToList();
        if (binari.Count > 0)
            return $"Non sono testo, il Lab non li registra: {string.Join(", ", binari.Take(5))}.";
        return null;
    }

    /// <summary>
    /// Il messaggio proposto: in testa il gesto (o quanti gesti, e in quali file), sotto un gesto per riga e i file.
    /// <paramref name="gesti"/> sono le voci della storia del Lab salvate e non ancora registrate (vuote se il Lab è stato
    /// riaperto nel frattempo: resta l'elenco dei file).
    /// </summary>
    public static string MessaggioProposto(IReadOnlyList<string> gesti, IReadOnlyList<FileCambiato> cambiati)
    {
        ArgumentNullException.ThrowIfNull(gesti);
        ArgumentNullException.ThrowIfNull(cambiati);
        var nomi = cambiati.Select(c => c.Percorso[(c.Percorso.LastIndexOf('/') + 1)..]).Distinct(StringComparer.Ordinal).ToList();
        string file = nomi.Count <= 3 ? string.Join(", ", nomi) : $"{string.Join(", ", nomi.Take(3))} e altri {nomi.Count - 3}";
        string testa = gesti.Count == 1 ? gesti[0] : gesti.Count > 1 ? $"{gesti.Count} modifiche in {file}" : $"Modifiche in {file}";
        var righe = new List<string> { testa };
        if (gesti.Count > 1)
        {
            righe.Add("");
            righe.AddRange(gesti.Select(g => "- " + g));
        }

        righe.Add("");
        righe.Add("File: " + string.Join(", ", nomi));
        return string.Join("\n", righe);
    }

    /// <summary>
    /// Registra i file cambiati (e solo quelli) col messaggio dato. Torna null se è andata, o il perché. Chi chiama ha
    /// già chiesto <see cref="PercheNonSiRegistra"/>: qui si ricontrolla, perché il disco può essere cambiato.
    /// </summary>
    public static async Task<string?> RegistraAsync(Git git, StatoDelRamo stato, string predefinito, string? messaggio, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(stato);
        if (PercheNonSiRegistra(stato, predefinito) is { } perche)
            return perche;
        if (string.IsNullOrWhiteSpace(messaggio))
            return "Scrivi il messaggio: dice agli altri AOD che cosa è cambiato e perché.";

        string[] percorsi = [.. DaRegistrare(stato).Select(c => c.Percorso)];
        var aggiungi = await git.EseguiAsync(stato.Radice, ["add", "--all", "--", .. percorsi], annulla).ConfigureAwait(false);
        if (!aggiungi.Riuscito)
            return $"git non ha preso i file: {aggiungi.Errore}";

        var registra = await git.EseguiAsync(stato.Radice, ["commit", "--quiet", "-m", messaggio.Trim(), "--", .. percorsi], annulla).ConfigureAwait(false);
        if (registra.Riuscito)
            return null;
        return registra.Errore.Contains("user.email", StringComparison.Ordinal) || registra.Errore.Contains("user.name", StringComparison.Ordinal)
            ? "git non sa chi sei: una volta sola, in un terminale, git config --global user.name \"Nome Cognome\" e git config --global user.email \"la tua mail di GitHub\"."
            : $"git non ha registrato: {registra.Errore}";
    }

    /// <summary>Vero se il file ha un byte zero nei primi 8 000: non è testo.</summary>
    private static bool Binario(string percorso)
    {
        try
        {
            using var flusso = File.OpenRead(percorso);
            var testa = new byte[8000];
            int letti = flusso.Read(testa, 0, testa.Length);
            return Array.IndexOf(testa, (byte)0, 0, letti) >= 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
