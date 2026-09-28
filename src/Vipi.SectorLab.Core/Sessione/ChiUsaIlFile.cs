using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>Un nome di <c>colors.def</c> e chi lo usa: quante volte, file per file.</summary>
/// <param name="Riga">La riga (da 1) di <c>colors.def</c> che lo definisce.</param>
public sealed record UsoDelColore(string Nome, string Valore, int Riga, IReadOnlyList<(string File, int Volte)> PerFile)
{
    public int Volte => PerFile.Sum(f => f.Volte);
}

/// <summary>
/// «Chi lo usa» di un FILE e dei nomi di <c>colors.def</c> (lotto «Subito» slice 7e, R-1). Un file lo usano gli
/// <c>.isc</c> che lo caricano (<c>F;</c>, o per il codice di uno scalo, o da un <c>.frq</c>) e i <c>.frq</c> che lo
/// citano come profilo (<c>PREFS\CTR.cpr</c>), ATIS (<c>default.atis</c>) o D-ATIS (<c>datis-acc.datis</c>); un nome
/// di <c>colors.def</c> lo usano il riempimento e il bordo dei <c>.pol</c> e dei settori dinamici, e le linee dei
/// <c>.geo</c>. Solo da vedere: rinominare un file o un colore non è in questa slice.
/// </summary>
public static class ChiUsaIlFile
{
    /// <summary>Chi usa il file <paramref name="relativo"/> (relativo alla radice del clone), per file e riga.</summary>
    public static IReadOnlyList<Citazione> Di(SessioneAperta sessione, string relativo, Func<string, IEnumerable<object>> sporchiDi)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(sporchiDi);
        string assoluto = Path.GetFullPath(sessione.Cartella.Assoluto(relativo));
        string dagliIt = Normale(Path.GetRelativePath(sessione.Cartella.CartellaIt, assoluto));
        var citazioni = new List<Citazione>();
        var senzaNome = new List<string>();

        // Gli .isc: la riga F; che lo nomina, o «per ICAO o da un .frq» se lo carica senza nominarlo.
        IEnumerable<string> ScaliDi(string percorso)
            => sessione.File.GetValueOrDefault(sessione.Cartella.Relativo(percorso)) is FileLetto<AirportInfo> scali
                ? scali.Letto.Records.Select(a => a.IcaoCode)
                : [];
        foreach (var carico in CarichiDegliIsc.Leggi(sessione.Cartella.SectorFiles, ScaliDi))
        {
            if (!carico.Caricati.Any(c => string.Equals(Path.GetFullPath(c), assoluto, StringComparison.OrdinalIgnoreCase)))
                continue;
            string isc = sessione.Cartella.Relativo(carico.Isc);
            string[] righe = System.IO.File.ReadAllLines(carico.Isc);
            bool nominato = false;
            for (int i = 0; i < righe.Length; i++)
            {
                string riga = righe[i].Trim();
                if (riga.StartsWith("F;", StringComparison.OrdinalIgnoreCase) && Nomina(riga[2..], dagliIt))
                {
                    citazioni.Add(new Citazione(isc, -1, i + 1, righe[i], "caricato", []));
                    nominato = true;
                }
            }

            // Il F; che punta altrove, e Aurora lo trova per nome (M6: DYNAMIC_SEC\GCI.tfl sta in OTHER\).
            foreach (var (riga, testo, _, trovato) in carico.TrovatiPerNome)
            {
                if (string.Equals(Path.GetFullPath(trovato), assoluto, StringComparison.OrdinalIgnoreCase))
                {
                    citazioni.Add(new Citazione(isc, -1, riga, testo, "caricato, trovato per nome (il F; dice un'altra cartella)", []));
                    nominato = true;
                }
            }

            if (!nominato)
                senzaNome.Add(isc);
        }

        // I .frq: profilo, ATIS, D-ATIS.
        foreach (var (frq, file) in sessione.File.Where(f => f.Key.EndsWith(".frq", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (file is not IFileConRecord conRecord)
                continue;
            var sporchi = sporchiDi(frq).ToList();
            var righe = conRecord.RigheDelFile(sporchi);
            var posti = conRecord.PostiDeiRecord(sporchi);
            for (int k = 0; k < conRecord.RecordDelModello.Count; k++)
            {
                if (conRecord.RecordDelModello[k] is not AtcPosition posizione)
                    continue;
                string? come = Nomina(posizione.Profile, dagliIt) ? "profilo"
                    : Nomina(posizione.AtisFile, dagliIt) ? "ATIS"
                    : Nomina(posizione.DatisFile, dagliIt) ? "D-ATIS"
                    : null;
                if (come is not null && k < posti.Count)
                    citazioni.Add(new Citazione(frq, k, posti[k].Da + 1, righe[posti[k].Da], come, []));
            }
        }

        // Un .isc che lo carica senza nominarlo: per il codice di uno scalo, o perché un .frq lo cita.
        bool daUnFrq = citazioni.Any(c => c.File.EndsWith(".frq", StringComparison.OrdinalIgnoreCase));
        citazioni.InsertRange(0, senzaNome.Select(isc => new Citazione(isc, -1, 0, "",
            daUnFrq ? "caricato perché un .frq lo cita" : "caricato per il codice dello scalo", [])));
        return citazioni;
    }

    /// <summary>
    /// I nomi di <c>colors.def</c> (letti dal disco: il motore non lo interpreta come record) e chi li usa. Null se il
    /// file non è un <c>.def</c>.
    /// </summary>
    public static IReadOnlyList<UsoDelColore>? Colori(SessioneAperta sessione, string relativo)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        if (!relativo.EndsWith(".def", StringComparison.OrdinalIgnoreCase))
            return null;
        string percorso = sessione.Cartella.Assoluto(relativo);
        if (!System.IO.File.Exists(percorso))
            return null;

        // Le righe dei nomi, come le legge Aurora (NOME;colore;), nell'ordine del file.
        var nomi = new List<(string Nome, string Valore, int Riga)>();
        string[] righe = System.IO.File.ReadAllLines(percorso);
        for (int i = 0; i < righe.Length; i++)
        {
            string riga = righe[i].Trim();
            if (riga.Length == 0 || riga.StartsWith("//", StringComparison.Ordinal))
                continue;
            string[] campi = riga.Split(';');
            if (campi.Length >= 2 && campi[0].Trim().Length > 0 && ColoreDelSector.TryLeggi(campi[1], out _))
                nomi.Add((campi[0].Trim(), campi[1].Trim(), i + 1));
        }

        var usi = nomi.ToDictionary(n => n.Nome, _ => new Dictionary<string, int>(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
        void Conta(string? colore, string file)
        {
            if (colore is not null && usi.TryGetValue(colore.Trim(), out var perFile))
                perFile[file] = perFile.GetValueOrDefault(file) + 1;
        }

        foreach (var (file, aperto) in sessione.File)
        {
            if (aperto is not IFileConRecord conRecord)
                continue;
            foreach (object record in conRecord.RecordDelModello)
            {
                switch (record)
                {
                    case Polygon poligono:
                        Conta(poligono.FillColor, file);
                        Conta(poligono.LineColor, file);
                        break;
                    case TflSector settore:
                        Conta(settore.FillColor, file);
                        Conta(settore.StrokeColor, file);
                        break;
                    case Line linea:
                        Conta(linea.Color, file);
                        break;
                }
            }
        }

        return [.. nomi.Select(n => new UsoDelColore(n.Nome, n.Valore, n.Riga,
            [.. usi[n.Nome].OrderByDescending(f => f.Value).ThenBy(f => f.Key, StringComparer.Ordinal).Select(f => (f.Key, f.Value))]))];
    }

    // Un percorso citato (un F; dell'.isc, un campo di un .frq) che nomina il file: dalla cartella dei dati (IT), con le
    // barre di Windows e anche con la barra di troppo davanti (`\liml.atis`, «file per file» §13); l'.isc di una FIR
    // può scrivere `IT\colors\colors.def` (dalla cartella Include).
    private static bool Nomina(string? citato, string dagliIt)
    {
        if (string.IsNullOrWhiteSpace(citato))
            return false;
        string pulito = Normale(citato.Trim()).TrimStart('/');
        return string.Equals(pulito, dagliIt, StringComparison.OrdinalIgnoreCase)
               || pulito.EndsWith("/" + dagliIt, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normale(string percorso) => percorso.Replace('\\', '/');
}
