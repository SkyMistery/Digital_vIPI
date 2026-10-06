using Vipi.Sectorfile.IO;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// Gli avvisi dei modelli ATIS e D-ATIS (lotto «Subito» slice 18a; «file per file» §22, W3 e W4): segnaposto che
/// Aurora non conosce, parentesi che non tornano, campo di un <c>.fds</c> che nessun modello usa, modello a voce che
/// non ha gli stessi segnaposto del suo D-ATIS.
/// </summary>
/// <remarks>
/// <para>Misure sul fork del 6 ottobre 2026: 7 <c>.atis</c>, 4 <c>.datis</c> (uno vuoto: <c>datis.datis</c>, voluto),
/// <c>atisextra.fds</c> con un campo (<c>ARR_TYPE</c>). Una parentesi in più in <c>default.atis</c>
/// (<c>[Runway in use [ARR]]].</c>), il modello di 102 posizioni; nessun segnaposto sconosciuto; nessun campo mai
/// usato. Le coppie ATIS ↔ D-ATIS dei <c>.frq</c> sono sette, e differiscono solo per <c>[STATION_NAME]</c> (i modelli
/// degli scali dicono il nome com'è pronunciato: «mlpainsa», «lee,NAH,teh») e per <c>[CPDLC]</c>, che sta solo nel
/// D-ATIS: quelle due differenze sono volute e non si segnalano.</para>
/// <para>Le pronunce non si controllano: sono scritte per la voce, e si correggono a orecchio (W3).</para>
/// </remarks>
public static class ControlloDegliAtis
{
    // Un ATIS a voce può dire il nome invece del segnaposto; il CPDLC si legge, non si ascolta.
    private static readonly HashSet<string> DifferenzeVolute = new(StringComparer.Ordinal) { "STATION_NAME", "CPDLC" };

    private sealed record Modello(string Relativo, int Riga, string Testo, ModelloAtisLetto Letto);

    /// <summary>
    /// I problemi dei modelli. <paramref name="modelli"/>: i <c>.atis</c> e i <c>.datis</c>; <paramref name="fds"/>:
    /// i <c>.fds</c>; <paramref name="frq"/>: i <c>.frq</c>, che dicono quale ATIS va con quale D-ATIS. Di ognuno il
    /// percorso da mostrare e le righe.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> modelli,
                                                    IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> fds,
                                                    IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> frq)
    {
        ArgumentNullException.ThrowIfNull(modelli);
        ArgumentNullException.ThrowIfNull(fds);
        ArgumentNullException.ThrowIfNull(frq);

        var problemi = new List<ProblemaDelSector>();
        var campi = new List<(string Relativo, int Riga, string Testo, string Nome)>();
        foreach (var (relativo, righe) in fds)
        {
            for (int i = 0; i < righe.Count; i++)
            {
                if (Dati(righe[i]) && righe[i].Split(';') is { Length: >= 2 } parti && parti[1].Trim() is ['[', .. { Length: > 0 } nome, ']'])
                    campi.Add((relativo, i + 1, righe[i], nome.Trim()));
            }
        }

        var conosciuti = ModelloAtis.DiAurora.Select(s => s.Nome).Concat(campi.Select(c => c.Nome)).ToHashSet(StringComparer.Ordinal);
        var letti = new List<Modello>();
        foreach (var (relativo, righe) in modelli)
        {
            for (int i = 0; i < righe.Count; i++)
            {
                if (!Dati(righe[i]))
                    continue;
                var letto = ModelloAtis.Leggi(righe[i]);
                letti.Add(new Modello(relativo, i + 1, righe[i], letto));

                foreach (string nome in letto.Segnaposto.Distinct(StringComparer.Ordinal).Where(n => !conosciuti.Contains(n)))
                {
                    problemi.Add(new(Regola.SegnapostoSconosciuto, relativo, i + 1, righe[i],
                        $"[{nome}] non è un segnaposto di Aurora né un campo di un .fds: nell'ATIS resta scritto com'è"));
                }

                if (letto.ChiuseInPiu.Count > 0)
                {
                    // Tolte le chiuse che non chiudono niente, il modello torna quello che Aurora legge già.
                    string corretta = string.Concat(righe[i].Where((_, k) => !letto.ChiuseInPiu.Contains(k)));
                    problemi.Add(new(Regola.ParentesiNonBilanciate, relativo, i + 1, righe[i],
                        $"{Quante(letto.ChiuseInPiu.Count, "una «]»", "«]»")} in più (carattere {string.Join(", ", letto.ChiuseInPiu.Select(k => k + 1))}): non chiude nessuna parte, e nell'ATIS si legge",
                        corretta));
                }

                if (letto.MaiChiuse.Count > 0)
                {
                    problemi.Add(new(Regola.ParentesiNonBilanciate, relativo, i + 1, righe[i],
                        $"{Quante(letto.MaiChiuse.Count, "una «[»", "«[»")} non si chiude (carattere {string.Join(", ", letto.MaiChiuse.Select(k => k + 1))}): la parte facoltativa che apre non finisce"));
                }
            }
        }

        var usati = letti.SelectMany(m => m.Letto.Segnaposto).ToHashSet(StringComparer.Ordinal);
        foreach (var campo in campi.Where(c => !usati.Contains(c.Nome)))
        {
            problemi.Add(new(Regola.CampoDellAtisMaiUsato, campo.Relativo, campo.Riga, campo.Testo,
                $"nessun modello .atis o .datis usa [{campo.Nome}]: il controllore lo riempie, e nessuno lo sente"));
        }

        // Le coppie: l'ATIS (5° campo) e il D-ATIS (8°) della stessa posizione di un .frq, per nome di file.
        var coppie = new Dictionary<(string Atis, string Datis), int>();
        foreach (var (_, righe) in frq)
        {
            foreach (string riga in righe.Where(Dati))
            {
                string[] c = riga.Split(';');
                if (c.Length >= 8 && Nome(c[4]) is { Length: > 0 } atis && Nome(c[7]) is { Length: > 0 } datis)
                    coppie[(atis, datis)] = coppie.GetValueOrDefault((atis, datis)) + 1;
            }
        }

        foreach (var ((atis, datis), posizioni) in coppie.OrderBy(c => c.Key.Atis, StringComparer.Ordinal).ThenBy(c => c.Key.Datis, StringComparer.Ordinal))
        {
            if (Primo(letti, atis) is not { } aVoce || Primo(letti, datis) is not { } scritto)
                continue;
            if (Confrontabili(aVoce.Letto).SequenceEqual(Confrontabili(scritto.Letto), StringComparer.Ordinal))
                continue;
            var (soloQui, soloLa) = Differenze(aVoce.Letto, scritto.Letto);
            string differenza = soloQui.Count + soloLa.Count == 0
                ? "gli stessi segnaposto, in un altro ordine"
                : string.Join("; ", new[]
                {
                    soloQui.Count > 0 ? "solo qui " + string.Join(" ", soloQui.Select(n => $"[{n}]")) : null,
                    soloLa.Count > 0 ? "solo nel D-ATIS " + string.Join(" ", soloLa.Select(n => $"[{n}]")) : null,
                }.Where(p => p is not null));
            problemi.Add(new(Regola.AtisEDatisDiversi, aVoce.Relativo, aVoce.Riga, aVoce.Testo,
                $"{Path.GetFileName(aVoce.Relativo)} e {Path.GetFileName(scritto.Relativo)} ({Quante(posizioni, "una posizione li usa", "posizioni li usano")} insieme) non dicono le stesse cose: {differenza}"));
        }

        return problemi.OrderBy(p => p.File, StringComparer.Ordinal).ThenBy(p => p.Riga);
    }

    /// <summary>
    /// I segnaposto che ha solo l'uno o solo l'altro di due modelli che vanno insieme, senza <c>STATION_NAME</c> e
    /// <c>CPDLC</c> (che è giusto siano diversi: il nome si può dire a voce, il CPDLC si legge soltanto).
    /// </summary>
    public static (IReadOnlyList<string> SoloNelPrimo, IReadOnlyList<string> SoloNelSecondo) Differenze(ModelloAtisLetto primo, ModelloAtisLetto secondo)
    {
        ArgumentNullException.ThrowIfNull(primo);
        ArgumentNullException.ThrowIfNull(secondo);
        var suoi = Confrontabili(primo);
        var altri = Confrontabili(secondo);
        return ([.. suoi.Except(altri, StringComparer.Ordinal)], [.. altri.Except(suoi, StringComparer.Ordinal)]);
    }

    // I segnaposto da confrontare fra un ATIS e il suo D-ATIS, nell'ordine, una volta ciascuno.
    private static List<string> Confrontabili(ModelloAtisLetto modello)
        => [.. modello.Segnaposto.Where(n => !DifferenzeVolute.Contains(n)).Distinct(StringComparer.Ordinal)];

    private static Modello? Primo(List<Modello> letti, string nomeDelFile)
        => letti.FirstOrDefault(m => string.Equals(Path.GetFileName(m.Relativo.Replace('\\', '/')), nomeDelFile, StringComparison.OrdinalIgnoreCase));

    // Il nome del file citato in un .frq, senza cartelle né la barra di troppo (`\liml.atis`).
    private static string Nome(string campo) => Path.GetFileName(campo.Trim().Replace('\\', '/'));

    private static bool Dati(string riga) => riga.Trim().Length > 0 && !riga.TrimStart().StartsWith("//", StringComparison.Ordinal);

    private static string Quante(int quante, string una, string tante) => quante == 1 ? una : $"{quante} {tante}";
}
