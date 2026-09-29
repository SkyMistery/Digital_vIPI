using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// I controlli dei NAVAIDS fra file, per ogni master (lotto «Subito» slice 10b, «file per file» L3, L4, U1): le attese in
/// rotta legate ai fix, VOR e NDB che le citano, nei due versi; il master che non carica <c>[HOLDENR]</c>; lo stesso nome
/// in due cataloghi, lontano.
/// </summary>
/// <remarks>
/// Misure sul fork del 29 settembre: 68 attese, tutte citate una volta, tranne <c>EKLAP</c> che cita <c>HLD-ELKAP</c>
/// mentre l'attesa si chiama <c>HLD-EKLAP</c> e la sua info dice <c>ELKAP</c> (un altro fix, a 143 NM). <c>HOLDENR.hold</c>
/// lo carica solo <c>ITALY.isc</c>: il committente vuole un avviso per ognuno degli altri master, e il controllo attesa
/// per attesa solo dove le attese ci sono. 17 VOR e NDB con lo stesso nome, 10 a più di 0,1 NM.
/// </remarks>
public static class ControlloDelleAttese
{
    // Sotto un decimo di miglio è lo stesso punto scritto due volte, come per NomeRipetuto.
    private const double StessoPunto = 185.2;

    private static readonly string[] Cataloghi = ["fix", "vor", "ndb"];

    private sealed record Navaid(string Nome, string Catalogo, Coordinate Posizione, string? Attesa, int IndiceDellAttesa,
                                 string Percorso, string Relativo, int Riga);

    /// <summary>
    /// I problemi dei NAVAIDS di un master. <paramref name="caricati"/>: i file che carica, col percorso sul disco, quello
    /// da mostrare e i record; <paramref name="testoDellaRiga"/>: la riga del disco (percorso, da 1).
    /// </summary>
    public static IEnumerable<ProblemaDelSector> DelMaster(string master,
        IReadOnlyList<(string Percorso, string Relativo, IReadOnlyList<object> Record)> caricati, Func<string, int, string> testoDellaRiga)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(caricati);
        ArgumentNullException.ThrowIfNull(testoDellaRiga);

        var navaid = new List<Navaid>();
        var attese = new List<(Attesa Attesa, string Percorso, string Relativo)>();
        foreach (var (percorso, relativo, record) in caricati)
        {
            foreach (object r in record)
            {
                switch (r)
                {
                    case Fix f:
                        navaid.Add(new(f.Name.Trim(), "fix", f.Position, Pulito(f.NomeDellAttesa), 5, percorso, relativo, f.Source.LineNumber));
                        break;
                    case Vor v:
                        navaid.Add(new(v.Ident.Trim(), "vor", v.Position, Pulito(v.NomeDellAttesa), 7, percorso, relativo, v.Source.LineNumber));
                        break;
                    case Ndb n:
                        navaid.Add(new(n.Ident.Trim(), "ndb", n.Position, Pulito(n.NomeDellAttesa), 7, percorso, relativo, n.Source.LineNumber));
                        break;
                    case Attesa a when a.Nome.Trim().Length > 0:
                        attese.Add((a, percorso, relativo));
                        break;
                }
            }
        }

        var problemi = new List<ProblemaDelSector>();
        problemi.AddRange(NomiInPiuCataloghi(navaid, testoDellaRiga));

        var citanti = navaid.Where(n => n.Attesa is not null).ToList();
        if (attese.Count == 0)
        {
            // Committente, 29 settembre: uno per master, non uno per attesa (sarebbero 68 per ognuno dei quattro di FIR).
            int quante = citanti.Select(c => c.Attesa).Distinct(StringComparer.Ordinal).Count();
            if (quante > 0)
            {
                problemi.Add(new(Regola.AtteseNonCaricate, master, 0, string.Empty,
                    $"{master} non carica [HOLDENR]: le {quante} attese citate dai suoi fix, VOR e NDB non si vedono (il tasto HOLD resta vuoto)"));
            }

            return problemi;
        }

        // Un'attesa può essere una sequenza di punti con lo stesso nome (manuale IVAO): conta il primo.
        var perNome = attese.GroupBy(a => a.Attesa.Nome.Trim(), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        // Un nome può stare in più file (ESTERNI.fix ripete secsi.fix, e SARKI due volte a 556 NM): vale il più vicino.
        var conNome = navaid.ToLookup(n => n.Nome, StringComparer.Ordinal);
        Navaid? IlPiuVicino(string nome, Coordinate a) => conNome[nome].MinBy(n => Validatore.Metri(a, n.Posizione));

        foreach (var c in citanti.Where(c => !perNome.ContainsKey(c.Attesa!)))
        {
            string col = "HLD-" + c.Nome;
            string testo = testoDellaRiga(c.Percorso, c.Riga);
            bool colNome = perNome.ContainsKey(col);
            problemi.Add(new(Regola.AttesaNonDefinita, c.Relativo, c.Riga, testo,
                $"«{c.Attesa}» non è fra le attese di [HOLDENR]" + (colNome ? $": c'è {col}, col nome del {Nome(c.Catalogo)} {c.Nome}" : string.Empty),
                colNome ? ConIlCampo(testo, c.IndiceDellAttesa, col) : null));
        }

        var citate = citanti.Select(c => c.Attesa!).ToHashSet(StringComparer.Ordinal);
        foreach (var (nome, (attesa, percorso, relativo)) in perNome)
        {
            int riga = attesa.Source.LineNumber;
            string testo = testoDellaRiga(percorso, riga);
            string? suo = nome.StartsWith("HLD-", StringComparison.Ordinal) ? nome[4..] : null;
            Coordinate? punto = attesa.Posizione.Posizione
                ?? (attesa.Posizione.Nome is { } n ? conNome[n.Trim()].FirstOrDefault()?.Posizione : null);
            var delNome = suo is null ? null : punto is { } p ? IlPiuVicino(suo, p) : conNome[suo].FirstOrDefault();

            if (!citate.Contains(nome))
            {
                string chi = delNome is null ? string.Empty
                    : delNome.Attesa is { } altra ? $"; il {Nome(delNome.Catalogo)} {delNome.Nome} cita «{altra}»"
                    : $"; il {Nome(delNome.Catalogo)} {delNome.Nome} non ne cita nessuna";
                problemi.Add(new(Regola.AttesaMaiCitata, relativo, riga, testo, "nessun fix, VOR o NDB la cita: non si vede" + chi));
            }

            if (punto is not { } qui)
                continue;

            foreach (var c in citanti.Where(c => c.Attesa == nome))
            {
                double metri = Validatore.Metri(qui, c.Posizione);
                if (metri >= StessoPunto)
                {
                    problemi.Add(new(Regola.AttesaFuoriPosto, relativo, riga, testo,
                        $"il punto dell'attesa è a {Miglia(metri)} NM dal {Nome(c.Catalogo)} {c.Nome} che la cita ({c.Relativo}:{c.Riga})"));
                }
            }

            if (attesa.Fix is { } fix && IlPiuVicino(fix.Trim(), qui) is { } dellInfo)
            {
                double metri = Validatore.Metri(qui, dellInfo.Posizione);
                if (metri >= StessoPunto)
                {
                    // La proposta: il fix col nome dell'attesa, se sta nel suo punto (HLD-EKLAP → EKLAP/090R-FL190).
                    bool proponi = delNome is not null && Validatore.Metri(qui, delNome.Posizione) < StessoPunto;
                    problemi.Add(new(Regola.AttesaFuoriPosto, relativo, riga, testo,
                        $"l'info nomina {fix.Trim()}, che sta a {Miglia(metri)} NM dal punto dell'attesa"
                            + (proponi ? $"; nel punto c'è il {Nome(delNome!.Catalogo)} {delNome.Nome}" : string.Empty),
                        proponi ? ConIlCampo(testo, 3, delNome!.Nome + attesa.Descrizione.Trim()[fix.Length..]) : null));
                }
            }
        }

        return problemi;
    }

    // Lo stesso nome in due cataloghi, a 0,1 NM o più: sul secondo (nell'ordine fix, VOR, NDB), col primo nel dettaglio.
    private static IEnumerable<ProblemaDelSector> NomiInPiuCataloghi(List<Navaid> navaid, Func<string, int, string> testoDellaRiga)
    {
        foreach (var gruppo in navaid.GroupBy(n => n.Nome, StringComparer.Ordinal))
        {
            var primi = gruppo.GroupBy(n => n.Catalogo).Select(g => g.First())
                .OrderBy(n => Array.IndexOf(Cataloghi, n.Catalogo)).ToList();
            for (int i = 1; i < primi.Count; i++)
            {
                double metri = Validatore.Metri(primi[0].Posizione, primi[i].Posizione);
                if (metri >= StessoPunto)
                {
                    var altro = primi[i];
                    yield return new(Regola.NomeInPiuCataloghi, altro.Relativo, altro.Riga, testoDellaRiga(altro.Percorso, altro.Riga),
                        $"{primi[0].Catalogo.ToUpperInvariant()} «{gruppo.Key}» in {primi[0].Relativo}:{primi[0].Riga}, a {Miglia(metri)} NM: " +
                        "un punto per nome non dice quale dei due (quale prende Aurora: da provare)");
                }
            }
        }
    }

    private static string? Pulito(string? nome) => nome?.Trim() is { Length: > 0 } n ? n : null;

    private static string Nome(string catalogo) => catalogo == "fix" ? "fix" : catalogo.ToUpperInvariant();

    private static string Miglia(double metri) => (metri / 1852).ToString("0.##", CultureInfo.InvariantCulture);

    // La riga con un campo (da 0) cambiato; i campi che mancano prima di lui restano vuoti al loro posto.
    private static string ConIlCampo(string riga, int indice, string valore)
    {
        var campi = riga.Split(';').ToList();
        bool chiusa = campi.Count > 1 && campi[^1].Length == 0;
        if (chiusa)
            campi.RemoveAt(campi.Count - 1);
        while (campi.Count <= indice)
            campi.Add(string.Empty);
        campi[indice] = valore;
        return string.Join(";", campi) + (chiusa || campi.Count == indice + 1 ? ";" : string.Empty);
    }
}
