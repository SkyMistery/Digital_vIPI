using System.Reflection;

namespace Vipi.Host;

/// <summary>
/// Quale codice è quello che gira, in due stringhe: una corta per la barra, una intera per il passaggio
/// del mouse e per la diagnostica d'avvio.
///
/// <para><b>Perché esiste.</b> «Che versione del sito è online?» non aveva risposta:
/// <c>AssemblyVersion</c> è <c>1.0.0</c> in ogni pacchetto, e l'unica prova a disposizione era la data in
/// <c>diagnostica/avvio-diagnostica.txt</c> — che dice quando è <b>ripartito</b>, non <b>che cosa</b> è
/// ripartito. Con Passenger, che riavvia il processo per inattività, quella data si rinfresca da sola: una
/// prova che si smentisce da sé.</para>
///
/// <para>Il timbro lo mette la build (<c>VipiTimbroVersione</c> in <c>Vipi.Host.csproj</c>) e porta il
/// <b>commit</b>, non l'ora di compilazione: ricompilare lo stesso codice deve dare la stessa versione.</para>
///
/// <para>Accanto al commit c'è la <b>versione</b> (<c>VipiVersione</c> in <c>Directory.Build.props</c>, dove
/// stanno anche le tre regole che le danno un significato). Fino al 30 agosto 2026 era una <i>lettera</i>
/// («e», «f», … fino a «j») passata al publish. ⚠️ I due pezzi non sono intercambiabili e servono a due cose
/// diverse: <b>il numero è il nome che diamo noi, il commit è l'unica cosa che identifica il codice</b>. Un
/// numero da solo riporterebbe al problema che ha fatto nascere questa classe — <c>AssemblyVersion</c> è
/// <c>1.0.0</c> in ogni pacchetto, e non dice niente.</para>
/// </summary>
internal static class VersioneBuild
{
    /// <summary>Quando è partito questo processo. Fisso per tutta la sua vita, letto una volta sola.</summary>
    private static readonly DateTime AvvioUtc = DateTime.UtcNow;

    private static (string Etichetta, string Dettaglio)? _cache;

    /// <summary>Le due stringhe, calcolate una volta.</summary>
    internal static (string Etichetta, string Dettaglio) Leggi() =>
        _cache ??= Componi(Metadato("VipiVersione"), Metadato("VipiCommit"), Metadato("VipiDataCommit"), AvvioUtc);

    /// <summary>
    /// Il timbro da scrivere nei <b>registri persistenti</b> — oggi: il gate delle riconciliazioni d'avvio
    /// (<c>ReconcileVipiDocuments</c>) — oppure <b>null</b> se questa build non ne ha uno.
    ///
    /// <para><b>Null è un valore che conta, non un ripiego.</b> In sviluppo non c'è nessun timbro, quindi
    /// tutte le build di sviluppo sarebbero la «stessa versione»: una riconciliazione che si è dichiarata
    /// conclusa una volta non ripartirebbe più, e chi aggiunge una sezione al catalogo la vedrebbe non
    /// arrivare — senza un errore da nessuna parte. Con null il gate si spegne e le passate girano a ogni
    /// avvio, che è il comportamento di prima e quello giusto mentre si scrive codice.</para>
    ///
    /// <para>⚠️ Porta <b>il commit e non il numero di versione</b>: il numero è il nome che diamo noi e può
    /// restare fermo fra due build (una PATCH ricostruita), il commit no. È la stessa ragione per cui
    /// <c>Etichetta</c> li mostra tutti e due.</para>
    /// </summary>
    internal static string? TimbroPersistente()
    {
        var commit = Pulisci(Metadato("VipiCommit"));
        if (commit is null) return null;

        var versione = Pulisci(Metadato("VipiVersione"));
        return versione is null ? commit : $"{versione}+{commit}";
    }

    private static string? Metadato(string chiave) =>
        typeof(VersioneBuild).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == chiave)?.Value;

    /// <summary>
    /// Composizione pura, così si può provare senza una build timbrata.
    ///
    /// <para>⚠️ Senza timbro si scrive <b>«sviluppo»</b> e non si inventa un numero: una versione finta è
    /// peggio di nessuna versione, perché a una versione si crede.</para>
    /// </summary>
    internal static (string Etichetta, string Dettaglio) Componi(
        string? versione, string? commit, string? dataCommit, DateTime avvioUtc)
    {
        versione = Pulisci(versione);
        commit = Pulisci(commit);
        dataCommit = Pulisci(dataCommit);

        var etichetta = string.Join(" · ", new[] { versione, commit }.Where(x => x is not null));
        if (etichetta.Length == 0) etichetta = "sviluppo";

        var pezzi = new List<string>();
        if (versione is not null) pezzi.Add($"versione {versione}");
        if (commit is not null) pezzi.Add(dataCommit is null ? $"commit {commit}" : $"commit {commit} del {dataCommit}");
        if (pezzi.Count == 0) pezzi.Add("build di sviluppo, senza timbro");
        pezzi.Add($"in servizio dal {avvioUtc:yyyy-MM-dd HH:mm} UTC");

        // Maiuscola all'inizio: è una frase, e finisce in un `title` e in un file che qualcuno legge.
        var dettaglio = string.Join(" · ", pezzi);
        return (etichetta, char.ToUpperInvariant(dettaglio[0]) + dettaglio[1..]);
    }

    private static string? Pulisci(string? valore) =>
        string.IsNullOrWhiteSpace(valore) ? null : valore.Trim();
}
