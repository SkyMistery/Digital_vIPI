using System.Text.RegularExpressions;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// I controlli del CPDLC (lotto «Subito» slice 11c, «file per file» M5): un gruppo col nome e senza messaggi, un messaggio
/// senza risposta, i valori dichiarati diversi da quelli del testo, risposta, gruppo e lunghezze fuori dal manuale.
/// </summary>
/// <remarks>
/// Misure sul fork del 29 settembre: 155 messaggi; il gruppo 15 si chiama «TWR» e non ne ha nessuno; tre messaggi senza
/// risposta sono pezzi del DCL (gruppo 20: <c>CLIMB [0]</c>, <c>SQK [0]</c>, <c>ATIS INFO [0]</c>) e non si dicono (scelta
/// dell'agente); due dichiarano più valori di quelli del testo (<c>WHEN CAN YOU ACCEPT [0]</c> 2, <c>REPORT PASSING [0]</c> 3).
/// </remarks>
public static partial class ControlloDelCpdlc
{
    private static readonly string[] Risposte = ["WU", "AN", "R", "NE"];

    /// <summary>
    /// I problemi del CPDLC dell'albero: <paramref name="file"/>, per ogni <c>.cpdlc</c> e <c>.cpdlcnames</c>, il percorso da
    /// mostrare e i record; <paramref name="testoDellaRiga"/> la riga del disco.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<object> Record)> file,
                                                    Func<string, int, string> testoDellaRiga)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(testoDellaRiga);

        var gruppiUsati = file.SelectMany(f => f.Record.OfType<MessaggioCpdlc>()).Select(m => m.Gruppo.Trim()).ToHashSet(StringComparer.Ordinal);
        foreach (var (relativo, record) in file)
        {
            foreach (object r in record)
            {
                switch (r)
                {
                    case MessaggioCpdlc m:
                    {
                        int riga = m.Source.LineNumber;
                        string testo = testoDellaRiga(relativo, riga);
                        string risposta = m.Risposta.Trim(), gruppo = m.Gruppo.Trim();
                        if (risposta.Length == 0 && gruppo != "20")
                            yield return new(Regola.MessaggioSenzaRisposta, relativo, riga, testo,
                                "nessuna risposta: il manuale vuole WU, AN, R o NE (nel gruppo 20, il DCL, i pezzi senza risposta sono normali)");
                        else if (risposta.Length > 0 && !Risposte.Contains(risposta))
                            yield return new(Regola.ValoreFuoriElenco, relativo, riga, testo, $"risposta «{risposta}»: il manuale vuole WU, AN, R o NE");
                        if (!EUnGruppo(gruppo, dcl: true))
                            yield return new(Regola.ValoreFuoriElenco, relativo, riga, testo, $"gruppo «{gruppo}»: il manuale vuole da 0 a 18, o 20 per il DCL");
                        if (m.Comando.Length > 128)
                            yield return new(Regola.ValoreFuoriElenco, relativo, riga, testo, $"il messaggio ha {m.Comando.Length} caratteri: il manuale ne vuole al più 128");
                        int nelTesto = Valori().Matches(m.Comando).Select(v => v.Value).Distinct(StringComparer.Ordinal).Count();
                        if (m.TotaleDeiValori is { } dichiarati && dichiarati != nelTesto)
                            yield return new(Regola.ValoriDelMessaggio, relativo, riga, testo,
                                $"dichiara {dichiarati} valori (TotVal), il testo ne ha {nelTesto}: si rifà con uLink");
                        break;
                    }

                    case NomeDelGruppoCpdlc g:
                    {
                        int riga = g.Source.LineNumber;
                        string testo = testoDellaRiga(relativo, riga);
                        string gruppo = g.Gruppo.Trim();
                        if (!EUnGruppo(gruppo, dcl: false))
                            yield return new(Regola.ValoreFuoriElenco, relativo, riga, testo, $"gruppo «{gruppo}»: i nomi valgono per i gruppi da 0 a 18");
                        else if (!gruppiUsati.Contains(gruppo))
                            yield return new(Regola.GruppoSenzaMessaggi, relativo, riga, testo,
                                $"il gruppo {gruppo} si chiama «{g.Nome.Trim()}», ma nessun messaggio sta nel gruppo {gruppo}: nella finestra CPDLC resta vuoto");
                        if (g.Nome.Trim().Length > 20)
                            yield return new(Regola.ValoreFuoriElenco, relativo, riga, testo, $"il nome ha {g.Nome.Trim().Length} caratteri: il manuale ne vuole al più 20");
                        break;
                    }
                }
            }
        }
    }

    private static bool EUnGruppo(string gruppo, bool dcl)
        => int.TryParse(gruppo, out int n) && gruppo == n.ToString(System.Globalization.CultureInfo.InvariantCulture)
           && (n is >= 0 and <= 18 || (dcl && n == 20));

    [GeneratedRegex(@"\[\d\]")]
    private static partial Regex Valori();
}
