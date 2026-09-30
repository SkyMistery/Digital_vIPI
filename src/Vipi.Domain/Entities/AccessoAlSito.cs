namespace Vipi.Domain.Entities;

/// <summary>
/// Chi è entrato nel sito almeno una volta: una riga per VID. Committente, 30 settembre 2026: col login obbligatorio
/// si vuole sapere chi legge — nome, divisione, ACC, primo e ultimo accesso, in quanti giorni. Lo vedono solo gli
/// amministratori. Carta <c>docs/feature/2026-09-30-registro-accessi.md</c>.
///
/// <para>⚠️ Sono dati personali di chiunque entri, di qualunque divisione: si tengono <b>dodici mesi dall'ultimo
/// accesso</b> (<see cref="Conservazione"/>) e poi si cancellano. Una riga, non una riga per accesso: non è un
/// registro di chi ha letto che cosa.</para>
/// </summary>
public class AccessoAlSito
{
    /// <summary>Il VID: la chiave. Non generata, viene da IVAO.</summary>
    public int UserId { get; set; }

    /// <summary>Il nome come lo manda IVAO all'ultimo accesso.</summary>
    public string Nome { get; set; } = "";

    /// <summary>Il nome breve, «Mario R.»: quello che compare nella classifica della divisione (30 settembre 2026,
    /// committente: nome, iniziale del cognome e VID). Null finché la persona non rientra dopo il rilascio.</summary>
    public string? NomeBreve { get; set; }

    /// <summary>La divisione IVAO (IT, FR, …); null se il cookie è di prima che la leggessimo.</summary>
    public string? Divisione { get; set; }

    /// <summary>L'ACC/FIR di appartenenza (LIRR, …).</summary>
    public string? Acc { get; set; }

    public DateTime PrimoUtc { get; set; }
    public DateTime UltimoUtc { get; set; }

    /// <summary>In quanti giorni diversi (UTC) è entrato.</summary>
    public int Giorni { get; set; }

    /// <summary>Dopo quanto dall'ultimo accesso la riga si cancella.</summary>
    public static readonly TimeSpan Conservazione = TimeSpan.FromDays(365);

    /// <summary>
    /// Un accesso adesso. Il giorno si conta una volta sola: chi entra dieci volte oggi ha un giorno, non dieci.
    /// Nome, divisione e ACC si aggiornano, ma una divisione che manca non cancella quella nota (un cookie vecchio
    /// non la porta).
    /// </summary>
    public void Registra(string nome, string? divisione, string? acc, DateTime oraUtc, string? nomeBreve = null)
    {
        if (!string.IsNullOrWhiteSpace(nomeBreve)) NomeBreve = Taglia(nomeBreve.Trim(), AccessoAlSitoLimits.NomeBreve);
        if (Giorni == 0)
        {
            PrimoUtc = oraUtc;
            Giorni = 1;
        }
        else if (oraUtc.Date > UltimoUtc.Date)
        {
            Giorni++;
        }

        if (oraUtc > UltimoUtc) UltimoUtc = oraUtc;
        if (!string.IsNullOrWhiteSpace(nome)) Nome = Taglia(nome.Trim(), AccessoAlSitoLimits.Nome);
        if (!string.IsNullOrWhiteSpace(divisione)) Divisione = Taglia(divisione.Trim().ToUpperInvariant(), AccessoAlSitoLimits.Divisione);
        if (!string.IsNullOrWhiteSpace(acc)) Acc = Taglia(acc.Trim().ToUpperInvariant(), AccessoAlSitoLimits.Acc);
    }

    private static string Taglia(string s, int n) => s.Length <= n ? s : s[..n];

    /// <summary>
    /// «Mario R.» da nome e cognome come IVAO li manda, separati. Il nome resta intero («Gian Marco R.»); del cognome
    /// l'iniziale della prima parola che comincia con una lettera maiuscola o no («de Santis» → «D.»). Senza cognome, il
    /// solo nome; senza nome, null — meglio il VID che un'iniziale sola.
    /// </summary>
    public static string? ComponiNomeBreve(string? nome, string? cognome)
    {
        var n = string.Join(' ', (nome ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (n.Length == 0) return null;
        var c = (cognome ?? "").Trim();
        var iniziale = c.FirstOrDefault(char.IsLetter);
        return iniziale == default ? n : $"{n} {char.ToUpperInvariant(iniziale)}.";
    }
}

/// <summary>Le lunghezze delle colonne.</summary>
public static class AccessoAlSitoLimits
{
    public const int Nome = 120;
    public const int NomeBreve = 60;
    public const int Divisione = 8;
    public const int Acc = 8;
}
