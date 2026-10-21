using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Core.Git;

/// <summary>Com'è un passo del giro della consegna.</summary>
public enum StatoDelPasso
{
    /// <summary>Non c'è niente da fare: è a posto.</summary>
    Fatto,

    /// <summary>C'è da fare: il primo così è il prossimo passo.</summary>
    DaFare,

    /// <summary>C'è da guardare, ma non ferma il giro (le segnalazioni della lettura).</summary>
    DaGuardare,

    /// <summary>Qui non si fa (la vetrina non è impostata, il ramo non è di lavoro): il perché lo dice il passo.</summary>
    NonSiFa,
}

/// <summary>Un passo del giro: la chiave (quella del riquadro, <c>data-passo</c>), il nome come il tasto, lo stato, e in parole.</summary>
public sealed record PassoDelGiro(string Chiave, string Nome, StatoDelPasso Stato, string Detto);

/// <summary>
/// Le due consegne come giri (riordino R5, «le due consegne»): il sector — modifiche → Salva → Registra il sector → Porta
/// in vetrina → Manda il sector — e l'AIP espanso — i PDF → la lettura → le segnalazioni → Registra l'AIP → Manda l'AIP.
/// Ogni passo dice se è fatto o che cosa manca, dallo stato che il Lab ha già (le modifiche, git, il lotto, la lettura);
/// il prossimo è il primo da fare. Non fa niente: lo mostra soltanto, sopra i riquadri di sempre.
/// </summary>
public static class GiroDellaConsegna
{
    /// <summary>Il passo da fare adesso: il primo <see cref="StatoDelPasso.DaFare"/>, o null se il giro è a posto.</summary>
    public static PassoDelGiro? Prossimo(IReadOnlyList<PassoDelGiro> passi)
    {
        ArgumentNullException.ThrowIfNull(passi);
        return passi.FirstOrDefault(p => p.Stato == StatoDelPasso.DaFare);
    }

    /// <summary>
    /// Il giro del sector. <paramref name="inSospeso"/>: le modifiche non salvate; <paramref name="problemiNuovi"/>: quelli che
    /// porterebbero; <paramref name="ramo"/>: git (null = il sector non è un repo, o non si è ancora letto);
    /// <paramref name="vetrina"/>: la cartella della vetrina è impostata; <paramref name="daPortare"/> e
    /// <paramref name="daVedere"/>: le voci del lotto; <paramref name="predefinito"/>: il ramo predefinito del sector (lì
    /// non si registra).
    /// </summary>
    public static IReadOnlyList<PassoDelGiro> DelSector(int inSospeso, int problemiNuovi, StatoDelRamo? ramo, bool vetrina, int daPortare, int daVedere,
        string predefinito)
    {
        var passi = new List<PassoDelGiro>
        {
            inSospeso > 0
                ? new("modifiche", "Modifiche", StatoDelPasso.DaFare,
                    $"{Conta(inSospeso, "modifica", "modifiche")} in sospeso" + (problemiNuovi > 0 ? $" · {Conta(problemiNuovi, "problema nuovo", "problemi nuovi")}" : ""))
                : new("modifiche", "Modifiche", StatoDelPasso.Fatto, "niente in sospeso"),
            inSospeso > 0
                ? new("salva", "Salva", StatoDelPasso.DaFare, $"Salva ({inSospeso}): le modifiche nei file")
                : new("salva", "Salva", StatoDelPasso.Fatto, "i file sono come le modifiche"),
        };

        if (ramo is null)
            passi.Add(new("registra", "Registra il sector", StatoDelPasso.NonSiFa, "il sector non è in un repo git"));
        else if (Registrazione.DaRegistrare(ramo).Count is var daRegistrare and > 0)
            // Ci sono file da registrare, ma non sempre qui si può (il ramo predefinito, un conflitto): allora il perché.
            passi.Add(Registrazione.PercheNonSiRegistra(ramo, predefinito) is { } no
                ? new("registra", "Registra il sector", StatoDelPasso.NonSiFa, $"{Conta(daRegistrare, "file salvato", "file salvati")}: {no}")
                : new("registra", "Registra il sector", StatoDelPasso.DaFare, $"{Conta(daRegistrare, "file salvato", "file salvati")} da registrare"));
        else
            passi.Add(new("registra", "Registra il sector", StatoDelPasso.Fatto, "niente da registrare"));

        if (!vetrina)
            passi.Add(new("vetrina", "Porta in vetrina", StatoDelPasso.NonSiFa, "la vetrina non è impostata (Impostazioni)"));
        else if (daPortare > 0)
            passi.Add(new("vetrina", "Porta in vetrina", StatoDelPasso.DaFare, $"{Conta(daPortare, "gesto", "gesti")} da portare in vetrina"));
        else if (daVedere > 0)
            passi.Add(new("vetrina", "Porta in vetrina", StatoDelPasso.DaFare, $"{Conta(daVedere, "gesto", "gesti")} da guardare in Aurora, poi «Visto in Aurora»"));
        else
            passi.Add(new("vetrina", "Porta in vetrina", StatoDelPasso.Fatto, "visto in Aurora"));

        passi.Add(Manda("manda", "Manda il sector", ramo, Invio.PercheNonSiManda, "il sector non è in un repo git"));
        return passi;
    }

    /// <summary>
    /// Il giro dell'AIP espanso. <paramref name="pdf"/>: i PDF nella cartella; <paramref name="cicloLetto"/>: il ciclo che
    /// l'AIP espanso ha già letto (null = nessuno); <paramref name="segnalazioni"/>: quelle dell'ultima lettura;
    /// <paramref name="repo"/>: git dell'AIP espanso (null = non è un repo).
    /// </summary>
    public static IReadOnlyList<PassoDelGiro> DellAip(int pdf, string? cicloLetto, int segnalazioni, StatoDelRamo? repo)
    {
        var passi = new List<PassoDelGiro>
        {
            // Con un ciclo già letto la cartella vuota va bene: i PDF servono solo per leggerne uno nuovo.
            (pdf, cicloLetto) switch
            {
                ( > 0, _) => new("pdf", "I PDF", StatoDelPasso.Fatto, $"{Conta(pdf, "PDF", "PDF")} nella cartella"),
                (_, { } letto) => new("pdf", "I PDF", StatoDelPasso.Fatto, $"il ciclo {letto} è letto; per uno nuovo, i suoi PDF nella cartella pdf-aip"),
                _ => new("pdf", "I PDF", StatoDelPasso.DaFare, "metti i PDF dell'AIP nella cartella pdf-aip"),
            },
            cicloLetto is { } ciclo
                ? new("leggi", "Leggi nell'AIP espanso", StatoDelPasso.Fatto, $"ciclo {ciclo} letto")
                : new("leggi", "Leggi nell'AIP espanso", pdf > 0 ? StatoDelPasso.DaFare : StatoDelPasso.NonSiFa,
                    pdf > 0 ? "i PDF non sono ancora nell'AIP espanso" : "prima i PDF"),
            segnalazioni > 0
                ? new("segnalazioni", "Segnalazioni", StatoDelPasso.DaGuardare, $"{Conta(segnalazioni, "segnalazione", "segnalazioni")} della lettura da guardare")
                : new("segnalazioni", "Segnalazioni", StatoDelPasso.Fatto, cicloLetto is null ? "nessuna lettura ancora" : "nessuna"),
        };
        if (repo is null)
            passi.Add(new("registra", "Registra l'AIP", StatoDelPasso.NonSiFa, "l'AIP espanso non è in un repo git"));
        else if (repo.Cambiati.Count is var cambiati and > 0)
            passi.Add(new("registra", "Registra l'AIP", StatoDelPasso.DaFare, $"{Conta(cambiati, "file", "file")} da registrare"));
        else
            passi.Add(new("registra", "Registra l'AIP", StatoDelPasso.Fatto, "niente da registrare"));
        passi.Add(Manda("manda", "Manda l'AIP", repo, PercheNonSiMandaLAip, "l'AIP espanso non è in un repo git"));
        return passi;
    }

    // «Manda» di un repo: da fare se c'è qualcosa che il server non ha; a posto se le registrazioni sono tutte lassù; se no
    // il perché non si manda.
    private static PassoDelGiro Manda(string chiave, string nome, StatoDelRamo? ramo, Func<StatoDelRamo, string?> perche, string senzaRepo)
    {
        if (ramo is null)
            return new(chiave, nome, StatoDelPasso.NonSiFa, senzaRepo);
        if (ramo.Monte is not null && ramo.Avanti == 0)
            return new(chiave, nome, StatoDelPasso.Fatto, "tutto sul server");
        if (perche(ramo) is { } no)
            return new(chiave, nome, StatoDelPasso.NonSiFa, no);
        return new(chiave, nome, StatoDelPasso.DaFare,
            ramo.Monte is null ? "mai mandato" : $"{Conta(ramo.Avanti, "registrazione", "registrazioni")} da mandare");
    }

    // L'AIP espanso si manda dal ramo del ciclo (aip/…), mai dal ramo predefinito.
    private static string? PercheNonSiMandaLAip(StatoDelRamo ramo) =>
        ramo.Ramo?.StartsWith(RepoDellAip.PrefissoDelRamo, StringComparison.Ordinal) == true
            ? ramo.Remoto is null ? "il repo non ha un server (origin)" : null
            : $"l'AIP espanso si manda dal ramo del ciclo ({RepoDellAip.PrefissoDelRamo}…), non da {ramo.Ramo ?? "un commit staccato"}";

    private static string Conta(int n, string uno, string piu) => $"{n} {(n == 1 ? uno : piu)}";
}
