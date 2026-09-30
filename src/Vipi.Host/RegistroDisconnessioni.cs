using System.Globalization;
using System.Text.Json;
using Vipi.Application.Abstractions;

namespace Vipi.Host;

/// <summary>
/// Una riga per ogni disconnessione vista da un browser, in <c>diagnostica/disconnessioni-AAAA-MM-GG.tsv</c>. La manda
/// <c>vipi-riconnessione.js</c> quando il riquadro «riconnessione» si chiude, in un modo o nell'altro.
///
/// <para><b>Perché esiste</b> (committente, 30 settembre 2026: le disconnessioni mentre si legge, «più spesso su
/// documenti lunghi come la vIPI di LIRF o LIMC»). I registri di prima dicevano quanto vive un circuito
/// (<see cref="RegistroRichieste"/>) e quando il processo si spegne (<see cref="RegistroAvvii"/>), ma non SE chi
/// guardava ha visto la pagina bloccarsi: una scheda chiusa e una connessione caduta finiscono con la stessa riga.
/// Misurato sui registri del 29–30 settembre: solo il 2% dei circuiti è morto insieme al processo, e del resto
/// non si sapeva niente.</para>
///
/// <para>⚠️ <b>Che cosa NON entra.</b> Il VID mai (basta «autenticato»), la query mai: la pagina è il solo percorso.
/// I valori arrivano dal browser, cioè da chiunque abbia fatto login: si tengono solo nella forma attesa, e al
/// più <see cref="MassimoAlMinuto"/> righe al minuto per processo — un ciclo impazzito non riempie il disco.</para>
/// </summary>
public sealed class RegistroDisconnessioni : IRiepilogoDisconnessioni
{
    public const string Prefisso = "disconnessioni";
    public const string Estensione = "tsv";
    public const string Rotta = "/vsop/diag/disconnessione";

    /// <summary>Un browser ne manda una per disconnessione: sessanta al minuto sono già un guasto, non traffico.</summary>
    internal const int MassimoAlMinuto = 60;

    internal const int MassimoCorpo = 2048;

    internal const string Colonne =
        "ora\tpid\tpid_pagina\tstesso_processo\tversione\tpagina\tesito\ts_sulla_pagina\ts_buco\ttentativi\tvisibile\ts_nascosta\tin_rete\tlettura\tautenticato";

    internal static readonly string[] Esiti = { "riagganciata", "rifiutata", "fallita", "abbandonata" };

    private readonly Action<DateTime, string> _scrivi;
    private readonly Func<string?> _cartella;
    private readonly string _versione;
    private readonly object _serratura = new();
    private DateTime _minuto;
    private int _nelMinuto;

    /// <summary>Quello vero: file del giorno in <c>diagnostica/</c>.</summary>
    public RegistroDisconnessioni() : this(Scrittore().Scrivi, RegistroGiornaliero.CartellaVera, VersioneBuild.Leggi().Etichetta) { }

    internal RegistroDisconnessioni(Action<DateTime, string> scrivi, Func<string?> cartella, string versione)
    {
        _scrivi = scrivi;
        _cartella = cartella;
        _versione = versione;
    }

    private static RegistroGiornaliero Scrittore() =>
        new(Prefisso, Estensione, Intestazione, RegistroGiornaliero.CartellaVera);

    /// <summary>L'endpoint: legge il beacon, lo scrive se ha la forma giusta, risponde 204 comunque.</summary>
    public async Task<IResult> RiceviAsync(HttpContext ctx)
    {
        try
        {
            var buffer = new byte[MassimoCorpo + 1];
            var letti = 0;
            int n;
            while (letti < buffer.Length && (n = await ctx.Request.Body.ReadAsync(buffer.AsMemory(letti), ctx.RequestAborted)) > 0)
                letti += n;
            if (letti > MassimoCorpo) return Results.NoContent();

            var ora = DateTime.UtcNow;
            if (!Concedi(ora)) return Results.NoContent();

            var riga = Riga(ora, Environment.ProcessId, _versione, buffer.AsSpan(0, letti).ToArray(),
                ctx.User.Identity?.IsAuthenticated == true);
            if (riga is not null) _scrivi(ora, riga);
        }
        catch { /* un beacon perso non è un guasto */ }
        return Results.NoContent();
    }

    private bool Concedi(DateTime ora)
    {
        lock (_serratura)
        {
            var minuto = new DateTime(ora.Year, ora.Month, ora.Day, ora.Hour, ora.Minute, 0, DateTimeKind.Utc);
            if (minuto != _minuto) { _minuto = minuto; _nelMinuto = 0; }
            return ++_nelMinuto <= MassimoAlMinuto;
        }
    }

    /// <summary>La riga dal corpo del beacon; <c>null</c> se non ha la forma attesa. Separata dall'I/O per i test.</summary>
    internal static string? Riga(DateTime ora, int pid, string versione, byte[] corpo, bool autenticato)
    {
        using var json = JsonDocument.Parse(corpo);
        var r = json.RootElement;
        if (r.ValueKind != JsonValueKind.Object) return null;

        var esito = Testo(r, "e");
        if (esito is null || Array.IndexOf(Esiti, esito) < 0) return null;

        // Solo un percorso del sito, e senza query: la stessa regola della pagina delle segnalazioni.
        var pagina = Vipi.Application.Content.FieldRequestRules.Pagina(Testo(r, "p")).Split('?', '#')[0];
        if (pagina.Length == 0) return null;
        if (pagina.Length > 160) pagina = pagina[..160];

        var pidPagina = Intero(r, "pid", 0, int.MaxValue);
        static string B(bool v) => v ? "1" : "0";
        return string.Join('\t',
            ora.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            pid.ToString(CultureInfo.InvariantCulture),
            pidPagina?.ToString(CultureInfo.InvariantCulture) ?? "-",
            pidPagina is null ? "-" : B(pidPagina == pid),
            versione.Replace('\t', ' '),
            pagina.Replace('\t', ' '),
            esito,
            (Intero(r, "sp", 0, 7 * 86400) ?? -1).ToString(CultureInfo.InvariantCulture),
            (Intero(r, "d", 0, 86400) ?? -1).ToString(CultureInfo.InvariantCulture),
            (Intero(r, "t", 0, 1000) ?? -1).ToString(CultureInfo.InvariantCulture),
            B(Intero(r, "v", 0, 1) == 1),
            (Intero(r, "n", -1, 7 * 86400) ?? -1).ToString(CultureInfo.InvariantCulture),
            B(Intero(r, "r", 0, 1) != 0),
            B(Intero(r, "s", 0, 1) == 1),
            B(autenticato));
    }

    private static string? Testo(JsonElement r, string nome) =>
        r.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Intero(JsonElement r, string nome, int min, int max) =>
        r.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)
            && !double.IsNaN(d) ? (int)Math.Clamp(Math.Round(d), min, max) : null;

    // ── Il riepilogo per la pagina Diagnostica ────────────────────────────────────────────────────────

    public RiepilogoDisconnessioni Leggi()
    {
        var giorni = new List<DisconnessioniDelGiorno>();
        var pagine = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (_cartella() is not { } cartella || !Directory.Exists(cartella))
            return new RiepilogoDisconnessioni(giorni, Array.Empty<(string, int)>());

        foreach (var file in Directory.GetFiles(cartella, $"{Prefisso}-*.{Estensione}").OrderByDescending(f => f))
        {
            var nome = Path.GetFileNameWithoutExtension(file)[(Prefisso.Length + 1)..];
            if (!DateOnly.TryParseExact(nome, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var giorno))
                continue;
            string[] righe;
            try { righe = File.ReadAllLines(file); } catch { continue; }
            giorni.Add(Giorno(giorno, righe, pagine));
        }
        return new RiepilogoDisconnessioni(giorni,
            pagine.OrderByDescending(p => p.Value).Take(10).Select(p => (p.Key, p.Value)).ToList());
    }

    internal static DisconnessioniDelGiorno Giorno(DateOnly giorno, IEnumerable<string> righe, Dictionary<string, int> pagine)
    {
        int tot = 0, ria = 0, rif = 0, fal = 0, abb = 0, cambiato = 0, nascosta = 0, senzaRete = 0;
        var buchi = new List<int>();
        foreach (var riga in righe)
        {
            if (riga.Length == 0 || !char.IsDigit(riga[0])) continue;
            var c = riga.Split('\t');
            if (c.Length < 15) continue;
            tot++;
            switch (c[6])
            {
                case "riagganciata": ria++; break;
                case "rifiutata": rif++; break;
                case "fallita": fal++; break;
                default: abb++; break;
            }
            if (c[3] == "0") cambiato++;
            if (c[10] == "0") nascosta++;
            if (c[12] == "0") senzaRete++;
            if (int.TryParse(c[8], out var d) && d >= 0) buchi.Add(d);
            pagine[c[5]] = pagine.GetValueOrDefault(c[5]) + 1;
        }
        buchi.Sort();
        var mediana = buchi.Count == 0 ? 0 : buchi.Count % 2 == 1
            ? buchi[buchi.Count / 2] : (buchi[buchi.Count / 2 - 1] + buchi[buchi.Count / 2]) / 2.0;
        return new DisconnessioniDelGiorno(giorno, tot, ria, rif, fal, abb, cambiato, nascosta, senzaRete, mediana);
    }

    private static string Intestazione() =>
        $"""
        # vIPI — disconnessioni viste dai browser: una riga ogni volta che il riquadro «riconnessione» si chiude.
        # Orari UTC; il giorno è nel nome del file. Chi la scrive: vipi-riconnessione.js, con un beacon a {Rotta}.
        #
        # pid = il processo che ha ricevuto la riga; pid_pagina = quello che aveva servito la pagina. stesso_processo 0 =
        #   il processo di prima non c'è più (o ce n'erano due): incrociare con avvii.txt.
        # esito: riagganciata (tornato da solo, niente perso) · rifiutata (il server non conosceva più il circuito:
        #   processo spento o ripartito) · fallita (tentativi finiti, server irraggiungibile) · abbandonata (pagina
        #   chiusa mentre si riprovava).
        # s_sulla_pagina = da quanto era aperta quando è caduta; s_buco = quanto è durato il buco.
        # visibile 0 = la scheda era in secondo piano (i browser strozzano i timer: il polso non parte);
        # s_nascosta = da quanti secondi lo era. in_rete 0 = il browser si diceva fuori rete.
        # lettura 1 = pagina di sola lettura (documento): lì il riquadro non si mostra più.
        # Il file si tiene {RegistroGiornaliero.GiorniTenuti} giorni. Il VID e la query non si scrivono.
        {Colonne}

        """.Replace("\r\n", "\n");
}
