namespace Vipi.DbSeed;

/// <summary>Motore di un capo del travaso.</summary>
public enum Motore
{
    /// <summary>Il <c>vipi.db</c> di sviluppo. Solo come sorgente.</summary>
    Sqlite,

    /// <summary>Neon, il deploy di prova. Sorgente del travaso verso la produzione, o destinazione.</summary>
    Postgres,

    /// <summary>MariaDB: <c>atc.it.ivao.aero</c>. Solo come destinazione.</summary>
    MySql,
}

/// <summary>
/// Riga di comando del travaso.
///
/// <para><b>Perché flag e non posizionali.</b> La forma storica era
/// <c>&lt;vipi.db&gt; &lt;connstring&gt;</c>: con una sola sorgente e una sola destinazione possibili non
/// c'era niente da confondere. Ora i capi sono due su tre e due su due, e in mezzo c'è un TRUNCATE: due
/// stringhe di connessione posizionali si possono invertire, e l'errore si scopre a database svuotato.
/// Un flag esplicito per capo rende l'inversione impossibile da scrivere.</para>
/// </summary>
public sealed class Opzioni
{
    public required Motore Sorgente { get; init; }
    public required string SorgenteConnessione { get; init; }

    /// <summary><c>null</c> = <c>--dry-run</c>: si legge e si conta, non si scrive niente.</summary>
    public Motore? Destinazione { get; init; }
    public string? DestinazioneConnessione { get; init; }

    /// <summary>Versione del server MariaDB, se diversa da quella di produzione. Vedi <c>MySqlSchema</c>.</summary>
    public string? VersioneServer { get; init; }
}

/// <summary>Parsing della riga di comando: nessun default implicito, ogni capo va nominato.</summary>
public static class Argomenti
{
    public const string Uso = """
        Uso:
          dotnet run --project tools/Vipi.DbSeed -- <sorgente> <destinazione> [--server-version <x.y.z>]

        Sorgente (una):
          --from-sqlite   <percorso vipi.db>
          --from-postgres <connstring o URL postgres://…>

        Destinazione (una):
          --to-postgres   <connstring o URL postgres://…>
          --to-mysql      <connstring MySQL/MariaDB>
          --dry-run       legge e conta, non scrive niente

        Esempi:
          # prova: cosa c'è su Neon, senza toccare niente
          dotnet run --project tools/Vipi.DbSeed -- --from-postgres "postgres://…" --dry-run

          # il travaso vero, verso la MariaDB locale da cui esce il .sql per Ivao.It
          dotnet run --project tools/Vipi.DbSeed -- --from-postgres "postgres://…" \
            --to-mysql "Server=127.0.0.1;Port=3399;Database=itivao_atc;User Id=…;Password=…"

        ATTENZIONE: la destinazione viene SVUOTATA (TRUNCATE di tutte le tabelle) prima del ricarico.
        """;

    public static bool Prova(string[] args, out Opzioni opzioni, out string? errore)
    {
        opzioni = null!;
        errore = null;

        Motore? sorgente = null, destinazione = null;
        string? sorgenteConn = null, destinazioneConn = null, versione = null;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--from-sqlite":
                case "--from-postgres":
                    if (sorgente is not null) { errore = "indicata più di una sorgente."; return false; }
                    if (!ProvaValore(args, ref i, arg, out sorgenteConn, out errore)) return false;
                    sorgente = arg == "--from-sqlite" ? Motore.Sqlite : Motore.Postgres;
                    break;

                case "--to-postgres":
                case "--to-mysql":
                    if (destinazione is not null || dryRun) { errore = "indicata più di una destinazione."; return false; }
                    if (!ProvaValore(args, ref i, arg, out destinazioneConn, out errore)) return false;
                    destinazione = arg == "--to-postgres" ? Motore.Postgres : Motore.MySql;
                    break;

                case "--dry-run":
                    if (destinazione is not null) { errore = "indicata più di una destinazione."; return false; }
                    dryRun = true;
                    break;

                case "--server-version":
                    if (!ProvaValore(args, ref i, arg, out versione, out errore)) return false;
                    break;

                case "-h":
                case "--help":
                    return false;   // nessun errore: si stampa l'uso e basta

                default:
                    errore = $"argomento non riconosciuto: '{arg}'.";
                    return false;
            }
        }

        if (sorgente is null) { errore = "manca la sorgente (--from-sqlite o --from-postgres)."; return false; }
        if (destinazione is null && !dryRun)
        {
            errore = "manca la destinazione (--to-postgres, --to-mysql o --dry-run).";
            return false;
        }
        if (versione is not null && destinazione != Motore.MySql)
        {
            errore = "--server-version vale solo con --to-mysql.";
            return false;
        }

        opzioni = new Opzioni
        {
            Sorgente = sorgente.Value,
            SorgenteConnessione = sorgenteConn!,
            Destinazione = destinazione,
            DestinazioneConnessione = destinazioneConn,
            VersioneServer = versione,
        };
        return true;
    }

    private static bool ProvaValore(string[] args, ref int i, string flag, out string? valore, out string? errore)
    {
        valore = null;
        errore = null;
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            errore = $"{flag} vuole un valore.";
            return false;
        }
        valore = args[++i];
        return true;
    }
}
