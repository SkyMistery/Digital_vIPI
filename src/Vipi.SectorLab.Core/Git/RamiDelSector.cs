using System.Globalization;
using System.Text;
using Vipi.Domain.Services;

namespace Vipi.SectorLab.Core.Git;

/// <summary>Da dove parte un lavoro nuovo (carta madre, decisione 6).</summary>
public enum BaseDelLavoro
{
    /// <summary>Dal ramo predefinito (<c>master</c>): un aggiornamento immediato.</summary>
    Immediato,

    /// <summary>Dal ramo del ciclo AIRAC dopo (<c>airac/AANN</c>): entra in vigore col ciclo.</summary>
    CicloDopo,
}

/// <summary>
/// I rami del sector (carta F4, slice 3): i nomi (<c>aod/&lt;VID&gt;/&lt;argomento&gt;</c>, <c>airac/AANN</c>), il
/// lavoro nuovo da un ramo del server, il passaggio da un ramo all'altro, il ramo del ciclo dopo quando manca.
/// <para>🔴 Il Lab non scrive mai su <c>master</c> né su un <c>airac/</c> che c'è già: ci arriva solo con una PR. Il
/// solo push verso un <c>airac/</c> è la sua <b>nascita</b>, uguale a <c>master</c>, quando sul server non c'è.</para>
/// </summary>
public static class RamiDelSector
{
    /// <summary>Il prefisso dei rami di lavoro degli AOD.</summary>
    public const string PrefissoDiLavoro = "aod/";

    /// <summary>Il prefisso dei rami dei cicli AIRAC.</summary>
    public const string PrefissoDelCiclo = "airac/";

    /// <summary>
    /// Il nome del ramo di lavoro: <c>aod/704798/correzioni-r10</c>. L'argomento diventa minuscolo, senza accenti,
    /// con i trattini al posto degli spazi e di tutto il resto; al più 40 caratteri. Null se non resta niente.
    /// </summary>
    public static string? NomeDiLavoro(string vid, string? argomento)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vid);
        if (string.IsNullOrWhiteSpace(argomento))
            return null;
        var pulito = new StringBuilder();
        foreach (char c in argomento.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            char minuscola = char.ToLowerInvariant(c);
            if (char.IsAsciiLetterOrDigit(minuscola))
                pulito.Append(minuscola);
            else if (pulito.Length > 0 && pulito[^1] != '-')
                pulito.Append('-');
        }

        string slug = pulito.ToString().Trim('-');
        if (slug.Length > 40)
            slug = slug[..40].TrimEnd('-');
        return slug.Length == 0 ? null : $"{PrefissoDiLavoro}{vid.Trim()}/{slug}";
    }

    /// <summary>Il ciclo AIRAC in vigore il giorno dato e quello dopo (<c>2610</c>, <c>2611</c>), dal calendario di vIPI.</summary>
    public static (string InVigore, string Dopo, DateOnly DalDopo) Cicli(DateTime utc)
    {
        var airac = new AiracService();
        var inizio = airac.EffectiveDateFor(DateOnly.FromDateTime(utc));
        var dopo = inizio.AddDays(28);
        return (airac.GetCycle(utc), airac.GetCycle(dopo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)), dopo);
    }

    /// <summary>Il ramo del ciclo AIRAC dopo quello in vigore il giorno dato: <c>airac/2611</c>.</summary>
    public static string RamoDelCicloDopo(DateTime utc) => PrefissoDelCiclo + Cicli(utc).Dopo;

    /// <summary>Il ramo predefinito del server (<c>master</c> sul repo vero), da <c>origin/HEAD</c>; <c>master</c> se non si sa.</summary>
    public static async Task<string> PredefinitoAsync(Git git, string radice, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var testa = await git.EseguiAsync(radice, ["symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD"], annulla).ConfigureAwait(false);
        return testa.Riuscito && testa.Uscita.Trim() is { Length: > 7 } nome && nome.StartsWith("origin/", StringComparison.Ordinal)
            ? nome["origin/".Length..]
            : "master";
    }

    /// <summary>I rami di qui (locali), in ordine.</summary>
    public static async Task<IReadOnlyList<string>> LocaliAsync(Git git, string radice, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var esito = await git.EseguiAsync(radice, ["for-each-ref", "--format=%(refname:short)", "refs/heads"], annulla).ConfigureAwait(false);
        return esito.Riuscito ? [.. esito.Righe.Order(StringComparer.Ordinal)] : [];
    }

    /// <summary>
    /// I rami di lavoro (<c>aod/…</c>) che il server ha e qui non ci sono, dall'ultimo <c>fetch</c> (carta F5, slice 5):
    /// il lavoro di un altro AOD, o fatto su un altro PC, da guardare in Aurora prima di fonderlo. Ci si passa come a un
    /// ramo di qui: <c>git switch</c> crea il ramo locale che segue quello del server. Quelli già fusi in
    /// <paramref name="predefinito"/> (le PR chiuse) non c'è più niente da guardare: non si mostrano.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DaGuardareAsync(Git git, string radice, string predefinito, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var esito = await git.EseguiAsync(radice, ["for-each-ref", "--format=%(refname:short)", "--no-merged", "refs/remotes/origin/" + predefinito,
            "refs/remotes/origin/" + PrefissoDiLavoro], annulla).ConfigureAwait(false);
        if (!esito.Riuscito)
            return [];
        var locali = await LocaliAsync(git, radice, annulla).ConfigureAwait(false);
        return [.. esito.Righe.Select(r => r["origin/".Length..]).Where(r => !locali.Contains(r, StringComparer.Ordinal)).Order(StringComparer.Ordinal)];
    }

    /// <summary>Vero se il server ha quel ramo (dall'ultimo <c>fetch</c>).</summary>
    public static async Task<bool> SulServerAsync(Git git, string radice, string ramo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var esito = await git.EseguiAsync(radice, ["rev-parse", "--verify", "--quiet", "refs/remotes/origin/" + ramo], annulla).ConfigureAwait(false);
        return esito.Riuscito;
    }

    /// <summary>
    /// Il ramo da cui è nato un ramo di lavoro (<c>master</c>, <c>airac/2611</c>): il Lab lo scrive nella
    /// configurazione del clone (<c>branch.&lt;nome&gt;.vipibase</c>) quando lo crea, e la PR va lì. Null se non si sa.
    /// </summary>
    public static async Task<string?> BaseDiAsync(Git git, string radice, string ramo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var esito = await git.EseguiAsync(radice, ["config", "--get", $"branch.{ramo}.vipibase"], annulla).ConfigureAwait(false);
        return esito.Riuscito && esito.Uscita.Trim() is { Length: > 0 } base_ ? base_ : null;
    }

    /// <summary>
    /// Un lavoro nuovo: chiede le novità al server, poi crea <paramref name="nome"/> dal ramo del server
    /// <paramref name="da"/> e ci passa. Torna null se è andata, o il perché. Chi chiama ha già controllato che non ci sia
    /// niente da registrare (<see cref="StatoDelRamo.Cambiati"/> vuoto).
    /// </summary>
    public static async Task<string?> NuovoLavoroAsync(Git git, string radice, string nome, string da, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (!nome.StartsWith(PrefissoDiLavoro, StringComparison.Ordinal))
            return "Un ramo di lavoro comincia con «aod/».";
        var formato = await git.EseguiAsync(radice, ["check-ref-format", "--branch", nome], annulla).ConfigureAwait(false);
        if (!formato.Riuscito)
            return $"«{nome}» non è un nome di ramo valido per git.";
        if ((await LocaliAsync(git, radice, annulla).ConfigureAwait(false)).Contains(nome, StringComparer.Ordinal))
            return $"Il ramo {nome} c'è già: passaci invece di rifarlo.";

        var fetch = await git.EseguiAsync(radice, ["fetch", "--quiet", "origin"], annulla).ConfigureAwait(false);
        if (!fetch.Riuscito)
            return $"Il server non ha risposto: {fetch.Errore}";
        if (!await SulServerAsync(git, radice, da, annulla).ConfigureAwait(false))
            return $"Sul server non c'è il ramo {da}.";

        var crea = await git.EseguiAsync(radice, ["switch", "--quiet", "--no-track", "-c", nome, "origin/" + da], annulla).ConfigureAwait(false);
        if (!crea.Riuscito)
            return $"git non ha creato il ramo: {crea.Errore}";
        await git.EseguiAsync(radice, ["config", $"branch.{nome}.vipibase", da], annulla).ConfigureAwait(false);
        return null;
    }

    /// <summary>Passa a un altro ramo di qui. Torna null se è andata, o il perché (git rifiuta se perderebbe lavoro).</summary>
    public static async Task<string?> PassaAsync(Git git, string radice, string ramo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var esito = await git.EseguiAsync(radice, ["switch", "--quiet", ramo], annulla).ConfigureAwait(false);
        return esito.Riuscito ? null : $"git non è passato a {ramo}: {esito.Errore}";
    }

    /// <summary>
    /// Fa nascere sul server il ramo del ciclo, uguale al ramo predefinito di adesso: il solo push del Lab verso un
    /// <c>airac/</c>, e solo se sul server non c'è. Torna null se è andata, o il perché.
    /// </summary>
    public static async Task<string?> FaiNascereIlCicloAsync(Git git, string radice, string ciclo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (!ciclo.StartsWith(PrefissoDelCiclo, StringComparison.Ordinal))
            return "Il ramo di un ciclo comincia con «airac/».";
        var fetch = await git.EseguiAsync(radice, ["fetch", "--quiet", "origin"], annulla).ConfigureAwait(false);
        if (!fetch.Riuscito)
            return $"Il server non ha risposto: {fetch.Errore}";
        if (await SulServerAsync(git, radice, ciclo, annulla).ConfigureAwait(false))
            return $"Il ramo {ciclo} c'è già sul server.";

        string predefinito = await PredefinitoAsync(git, radice, annulla).ConfigureAwait(false);
        var push = await git.EseguiAsync(radice, ["push", "--quiet", "origin", $"refs/remotes/origin/{predefinito}:refs/heads/{ciclo}"], annulla).ConfigureAwait(false);
        if (!push.Riuscito)
            return $"Il server non ha accettato il ramo {ciclo}: {push.Errore}";
        await git.EseguiAsync(radice, ["fetch", "--quiet", "origin"], annulla).ConfigureAwait(false);
        return null;
    }
}
