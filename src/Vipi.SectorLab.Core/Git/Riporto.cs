namespace Vipi.SectorLab.Core.Git;

/// <summary>Come si risolve un file in conflitto nel riporto.</summary>
public enum SceltaDelConflitto
{
    /// <summary>Resta la versione del ciclo (il ramo <c>airac/</c>).</summary>
    TieniIlCiclo,

    /// <summary>Vince la versione di <c>master</c>.</summary>
    PrendiMaster,

    /// <summary>L'AOD l'ha sistemato a mano nel file: niente segni di conflitto rimasti.</summary>
    SistematoAMano,
}

/// <summary>
/// Il riporto di <c>master</c> nel ramo del ciclo (carta F4, slice 7; carta madre §7 «riporto da main»): gli
/// aggiornamenti immediati entrano anche nel ciclo dopo. Non si scrive su <c>airac/</c>: si fa un ramo di lavoro
/// <c>aod/&lt;VID&gt;/riporto-&lt;ciclo&gt;</c> dal ciclo, ci si fonde <c>master</c>, e la PR porta il riporto nel ciclo.
/// <para>Un conflitto il Lab non lo risolve da solo: mostra i file, e per ognuno l'AOD sceglie la versione del ciclo, quella
/// di <c>master</c>, o dice che l'ha sistemato a mano. Un riporto a metà si annulla con <c>git merge --abort</c>, mai con un
/// reset (§5).</para>
/// </summary>
public static class Riporto
{
    /// <summary>Il nome del ramo del riporto: <c>aod/704798/riporto-2611</c>.</summary>
    public static string Ramo(string vid, string ciclo) => $"{RamiDelSector.PrefissoDiLavoro}{vid}/riporto-{ciclo[RamiDelSector.PrefissoDelCiclo.Length..]}";

    /// <summary>Quante registrazioni di <c>master</c> il ciclo non ha ancora (dall'ultimo <c>fetch</c>).</summary>
    public static async Task<int> DaRiportareAsync(Git git, string radice, string predefinito, string ciclo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var conta = await git.EseguiAsync(radice, ["rev-list", "--count", $"origin/{ciclo}..origin/{predefinito}"], annulla).ConfigureAwait(false);
        return conta.Riuscito && int.TryParse(conta.Uscita.Trim(), out int n) ? n : 0;
    }

    /// <summary>Vero se c'è una fusione a metà (un riporto coi conflitti da risolvere).</summary>
    public static async Task<bool> InCorsoAsync(Git git, string radice, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        return (await git.EseguiAsync(radice, ["rev-parse", "--quiet", "--verify", "MERGE_HEAD"], annulla).ConfigureAwait(false)).Riuscito;
    }

    /// <summary>
    /// Comincia il riporto: chiede le novità al server, crea il ramo del riporto dal ciclo e ci fonde <c>master</c>. Torna
    /// null se è andato liscio (registrato, da mandare), «conflitti» se ci sono file da risolvere, o il perché.
    /// </summary>
    public static async Task<string?> CominciaAsync(Git git, string radice, string vid, string predefinito, string ciclo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var fetch = await git.EseguiAsync(radice, ["fetch", "--quiet", "origin"], annulla).ConfigureAwait(false);
        if (!fetch.Riuscito)
            return $"Il server non ha risposto: {fetch.Errore}";
        if (!await RamiDelSector.SulServerAsync(git, radice, ciclo, annulla).ConfigureAwait(false))
            return $"Sul server non c'è il ramo {ciclo}.";
        if (await DaRiportareAsync(git, radice, predefinito, ciclo, annulla).ConfigureAwait(false) == 0)
            return $"Niente da riportare: {ciclo} ha già tutto quel che c'è su {predefinito}.";

        string ramo = Ramo(vid, ciclo);
        if (await RamiDelSector.NuovoLavoroAsync(git, radice, ramo, ciclo, annulla).ConfigureAwait(false) is { } noRamo)
            return noRamo;

        var fondi = await git.EseguiAsync(radice, ["merge", "--no-ff", "--no-edit", "-m", $"Riporto di {predefinito} in {ciclo}", "origin/" + predefinito], annulla).ConfigureAwait(false);
        if (fondi.Riuscito)
            return null;
        if (await InCorsoAsync(git, radice, annulla).ConfigureAwait(false))
            return "conflitti";
        return fondi.Errore.Contains("user.email", StringComparison.Ordinal) || fondi.Errore.Contains("user.name", StringComparison.Ordinal)
            ? "git non sa chi sei: una volta sola, git config --global user.name e user.email."
            : $"Il riporto non è riuscito: {fondi.Errore}";
    }

    /// <summary>
    /// Allinea il ramo di lavoro di adesso al ramo da cui è nato (<paramref name="base_"/>), dopo che altre PR ci sono
    /// state fuse (carta F4, slice 8: trovato coi cinque lavori R-10 dello stesso giorno, due righe di changelog nello
    /// stesso punto). Come il riporto: si fonde <c>origin/&lt;base&gt;</c> nel ramo, e i conflitti li risolve l'AOD; poi
    /// si manda di nuovo e la PR si aggiorna da sola. Torna null se è andata liscia, «conflitti», o il perché.
    /// </summary>
    public static async Task<string?> AllineaAsync(Git git, string radice, string base_, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var fetch = await git.EseguiAsync(radice, ["fetch", "--quiet", "origin"], annulla).ConfigureAwait(false);
        if (!fetch.Riuscito)
            return $"Il server non ha risposto: {fetch.Errore}";
        var conta = await git.EseguiAsync(radice, ["rev-list", "--count", $"HEAD..origin/{base_}"], annulla).ConfigureAwait(false);
        if (conta.Riuscito && conta.Uscita.Trim() == "0")
            return $"Niente da allineare: il ramo ha già tutto quel che c'è su {base_}.";

        var fondi = await git.EseguiAsync(radice, ["merge", "--no-ff", "--no-edit", "-m", $"Allineo il ramo a {base_}", "origin/" + base_], annulla).ConfigureAwait(false);
        if (fondi.Riuscito)
            return null;
        if (await InCorsoAsync(git, radice, annulla).ConfigureAwait(false))
            return "conflitti";
        return $"L'allineamento non è riuscito: {fondi.Errore}";
    }

    /// <summary>Quante registrazioni del ramo d'origine il ramo di lavoro non ha ancora (dall'ultimo <c>fetch</c>).</summary>
    public static async Task<int> IndietroRispettoAsync(Git git, string radice, string base_, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var conta = await git.EseguiAsync(radice, ["rev-list", "--count", $"HEAD..origin/{base_}"], annulla).ConfigureAwait(false);
        return conta.Riuscito && int.TryParse(conta.Uscita.Trim(), out int n) ? n : 0;
    }

    /// <summary>Risolve un file in conflitto come ha scelto l'AOD. Torna null se è andata, o il perché.</summary>
    public static async Task<string?> RisolviAsync(Git git, string radice, string percorso, SceltaDelConflitto scelta, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (scelta is SceltaDelConflitto.TieniIlCiclo or SceltaDelConflitto.PrendiMaster)
        {
            var prendi = await git.EseguiAsync(radice, ["checkout", scelta == SceltaDelConflitto.TieniIlCiclo ? "--ours" : "--theirs", "--", percorso], annulla).ConfigureAwait(false);
            if (!prendi.Riuscito)
                return $"git non ha preso quella versione di {percorso}: {prendi.Errore}";
        }
        else if (HaSegniDiConflitto(Path.Combine(radice, percorso.Replace('/', Path.DirectorySeparatorChar))))
        {
            return $"In {percorso} ci sono ancora i segni del conflitto (<<<<<<< e >>>>>>>): sistemalo, poi riprova.";
        }

        var aggiungi = await git.EseguiAsync(radice, ["add", "--", percorso], annulla).ConfigureAwait(false);
        return aggiungi.Riuscito ? null : $"git non ha preso {percorso}: {aggiungi.Errore}";
    }

    /// <summary>Conclude il riporto quando non resta niente in conflitto. Torna null se è andata, o il perché.</summary>
    public static async Task<string?> ConcludiAsync(Git git, StatoDelRamo stato, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(stato);
        if (stato.Cambiati.FirstOrDefault(c => c.Come == "in conflitto") is { } resta)
            return $"{resta.Percorso} è ancora in conflitto.";
        var registra = await git.EseguiAsync(stato.Radice, ["commit", "--quiet", "--no-edit"], annulla).ConfigureAwait(false);
        return registra.Riuscito ? null : $"git non ha concluso il riporto: {registra.Errore}";
    }

    /// <summary>Annulla il riporto a metà: i file tornano come prima della fusione (<c>git merge --abort</c>).</summary>
    public static async Task<string?> AnnullaAsync(Git git, string radice, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var esito = await git.EseguiAsync(radice, ["merge", "--abort"], annulla).ConfigureAwait(false);
        return esito.Riuscito ? null : $"git non ha annullato il riporto: {esito.Errore}";
    }

    private static bool HaSegniDiConflitto(string file)
        => File.Exists(file) && File.ReadLines(file).Any(r => r.StartsWith("<<<<<<<", StringComparison.Ordinal) || r.StartsWith(">>>>>>>", StringComparison.Ordinal));
}
