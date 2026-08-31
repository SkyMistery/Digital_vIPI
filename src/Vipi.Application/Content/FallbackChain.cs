using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una riga di ripiego <b>dichiarata</b>: chi riceve il traffico di un settore chiuso, e in quale fascia di
/// quota. Quote nulle = riga sempre valida.
/// </summary>
/// <param name="TargetCallsign">Il settore che raccoglie.</param>
/// <param name="BaseFeet">Piede della fascia in piedi, <b>incluso</b>. Null = nessun limite in basso.</param>
/// <param name="TopFeet">Tetto della fascia in piedi, <b>escluso</b>. Null = nessun limite in alto.</param>
public readonly record struct FallbackRow(string TargetCallsign, int? BaseFeet, int? TopFeet)
{
    /// <summary>
    /// Se la riga vale a quella quota. Il piede è incluso e il tetto escluso: due fasce scritte
    /// <c>SFC–FL305</c> e <c>FL305–UNL</c> non si contendono FL305, che va all'alta — che è come le legge
    /// chi le ha scritte.
    ///
    /// <para>⚠️ Con <paramref name="levelFeet"/> <c>null</c> (un punto che la quota non la dichiara: «as
    /// coordinated», «per aerovia») una riga con fascia <b>non si può valutare</b> e viene saltata. Non è un
    /// ripiego mancato, è l'unica risposta onesta — e va detto a schermo, o l'admin lo legge come un guasto.</para>
    /// </summary>
    public bool AppliesAt(int? levelFeet)
    {
        if (BaseFeet is null && TopFeet is null) return true;
        if (levelFeet is not int q) return false;
        return (BaseFeet is not int b || q >= b) && (TopFeet is not int t || q < t);
    }
}

/// <summary>
/// Una voce della catena come si <b>mostra</b>: chi raccoglie, in quale fascia, e se è una riga scritta da
/// qualcuno o il padre di copertura — che nessuno scrive e che non si può togliere.
/// </summary>
/// <param name="TargetCallsign">Chi raccoglie.</param>
/// <param name="BaseFeet">Piede della fascia in piedi (incluso). Null = nessun limite.</param>
/// <param name="TopFeet">Tetto della fascia in piedi (escluso). Null = nessun limite.</param>
/// <param name="FromParent">Vero se è il padre di copertura, cioè la coda implicita della catena.</param>
public readonly record struct FallbackStep(string TargetCallsign, int? BaseFeet, int? TopFeet, bool FromParent);

/// <summary>
/// La catena di ripiego di un settore <b>a una data quota</b>: i candidati a ricevere il suo traffico, in
/// ordine di priorità, che <c>TransferOnlineResolver</c> poi confronta con chi è online.
///
/// <para><b>Perché esiste.</b> Fino al 31 agosto 2026 la ricaduta era la sola catena dei padri, che è un
/// albero <b>senza dimensione verticale</b>. Su Milano, con WS2/ES2 divisi fino a FL305 e WS5 aperto sopra,
/// un trasferimento diretto a ES5 a FL350 finiva su ES2 — il padre — mentre quel cielo è di WS5, che non
/// vedeva niente. Nessun avviso: la ricaduta <b>riusciva</b>, solo verso il settore sbagliato.</para>
///
/// <para><b>Il padre è la coda della lista, non un meccanismo accanto.</b> Le righe dichiarate si consultano
/// per prime; esaurite, si continua sul padre come si è sempre fatto. A tabella vuota il risultato è
/// identico a quello di prima, riga per riga — ed è il motivo per cui questa non è una migrazione di dati.</para>
///
/// <para>Puro e deterministico, nessun I/O. Carta
/// <c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §2.</para>
/// </summary>
public static class FallbackChain
{
    /// <summary>
    /// I candidati di <paramref name="sector"/> alla quota <paramref name="levelFeet"/>, sé compreso e per
    /// primo, senza ripetizioni.
    ///
    /// <para>⚠️ <b>La visita è in ampiezza, non in profondità</b>, e la differenza si vede in un caso vero.
    /// Da ES5, con la riga «sopra FL305 → WS5» e il padre ES2, in profondità si esplorerebbe tutto il ramo di
    /// WS5 — WS5, poi il <i>suo</i> padre WS2 — prima ancora di guardare ES2. Con WS2 ed ES2 entrambi online
    /// il traffico dell'est finirebbe a ovest, saltando il proprio padre. In ampiezza l'ordine è
    /// <c>ES5, WS5, ES2, WS2</c>: prima tutti i ripieghi a un passo, poi quelli a due.</para>
    /// </summary>
    /// <param name="declared">callsign → sue righe dichiarate, <b>già in ordine</b>. Assente = nessuna riga.</param>
    /// <param name="parentOf">Il padre effettivo di un callsign, o null. È la coda della catena.</param>
    public static IReadOnlyList<string> Candidates(
        string sector,
        int? levelFeet,
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> declared,
        Func<string, string?> parentOf)
    {
        var risultato = new List<string>();
        if (string.IsNullOrWhiteSpace(sector)) return risultato;

        risultato.Add(sector.Trim());
        foreach (var passo in Cammina(sector, declared, parentOf, r => r.AppliesAt(levelFeet)))
            foreach (var e in passo)
                risultato.Add(e.TargetCallsign);

        return risultato;
    }

    /// <summary>
    /// La stessa catena, ma <b>per mostrarla</b>: raggruppata per passo e con la fascia di ogni voce, senza
    /// filtrare su una quota — perché a schermo si vogliono vedere <b>tutte</b> le fasce insieme, ed è
    /// esattamente la domanda «chi si dividerebbe il traffico, e a quali quote».
    ///
    /// <para>⚠️ <b>Passa dalla stessa camminata di <see cref="Candidates"/></b>, con l'unica differenza del
    /// filtro sulla fascia. Ricalcolarla a parte per il disegno vorrebbe dire avere due catene che possono
    /// divergere — cioè un pannello che mostra una cosa e una ricaduta che ne fa un'altra. È il difetto che
    /// questa carta esiste per chiudere, e sarebbe ridicolo riaprirlo nella schermata che lo racconta.</para>
    ///
    /// <para>Il settore di partenza <b>non</b> è nel risultato: è l'intestazione, non un ripiego.</para>
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<FallbackStep>> Sequence(
        string sector,
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> declared,
        Func<string, string?> parentOf) =>
        Cammina(sector, declared, parentOf, _ => true);

    /// <summary>
    /// La camminata in ampiezza, sola e condivisa. Ogni giro produce <b>un passo</b>: tutte le voci a quella
    /// distanza dal settore di partenza, nell'ordine in cui la risoluzione le guarderebbe.
    /// </summary>
    /// <param name="accetta">Quali righe dichiarate contano. Con la quota, la ricaduta; sempre vero, il disegno.</param>
    private static List<IReadOnlyList<FallbackStep>> Cammina(
        string sector,
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> declared,
        Func<string, string?> parentOf,
        Func<FallbackRow, bool> accetta)
    {
        var passi = new List<IReadOnlyList<FallbackStep>>();
        if (string.IsNullOrWhiteSpace(sector)) return passi;

        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sector.Trim() };
        var fronte = new List<string> { sector.Trim() };

        // Il tetto non è una difesa dai cicli — quelli li chiude `visti` — ma dalla catena lunghissima che
        // un dato sporco potrebbe produrre in una pagina che deve restare leggibile.
        for (var giro = 0; giro < 24 && fronte.Count > 0; giro++)
        {
            var passo = new List<FallbackStep>();
            var prossimo = new List<string>();

            foreach (var x in fronte)
            {
                if (declared.TryGetValue(x, out var righe))
                    foreach (var r in righe)
                    {
                        if (!accetta(r) || string.IsNullOrWhiteSpace(r.TargetCallsign)) continue;
                        if (!visti.Add(r.TargetCallsign)) continue;
                        passo.Add(new FallbackStep(r.TargetCallsign, r.BaseFeet, r.TopFeet, FromParent: false));
                        prossimo.Add(r.TargetCallsign);
                    }

                if (parentOf(x) is { Length: > 0 } padre && visti.Add(padre))
                {
                    passo.Add(new FallbackStep(padre, null, null, FromParent: true));
                    prossimo.Add(padre);
                }
            }

            if (passo.Count == 0) break;
            passi.Add(passo);
            fronte = prossimo;
        }

        return passi;
    }

    /// <summary>Quota di un punto di trasferimento in piedi. <c>FL350</c> → 35000; <c>null</c> resta null.</summary>
    public static int? FeetOf(int? levelValue, LevelUnit unit) =>
        levelValue is not int v ? null : unit == LevelUnit.Fl ? v * 100 : v;
}
