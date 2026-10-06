using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>Come un file scrive un'interruzione della linea (lotto «Subito» slice 5b, «file per file» B6, R-3).</summary>
public enum FormaDellInterruzione
{
    /// <summary>Una riga vuota fra due punti: le SID (<c>lied.sid</c>, NORTH DEP16) e le MVA di scalo.</summary>
    RigaVuota,

    /// <summary><c>&lt;br&gt;</c> nel 3° campo del punto che comincia il tratto nuovo: gli <c>.str</c>.</summary>
    Br,

    /// <summary>Una riga <c>T;DUMMY;…</c> fra i due punti: i confini <c>.artcc</c>, <c>.hartcc</c>, <c>.lartcc</c>.</summary>
    Dummy,

    /// <summary>Una riga <c>T;BREAK;PUNTO;PUNTO;</c> dopo l'ultimo punto del pezzo: le aerovie.</summary>
    Break,
}

/// <summary>
/// Dopo l'ultimo punto di un elenco la linea continua, oltre un'interruzione, in un altro elenco dello stesso record (il
/// poligono dopo, il tratto dopo) o in un altro record (l'aerovia dopo il <c>BREAK</c>, la zona dopo la riga vuota).
/// </summary>
public sealed record Continuazione(int Record, string Campo, string Testo);

/// <summary>
/// Spezza e unisci (lotto «Subito» slice 5b): «spezza dopo questo punto» toglie il pezzo di linea fra un punto e il
/// prossimo, «unisci» lo rimette. Lo stesso gesto ha cinque scritture, una per famiglia di file
/// (<see cref="FormaDellInterruzione"/>), e si fa <b>sul testo</b> del file com'è adesso: si aggiunge o si toglie la riga
/// (o il <c>&lt;br&gt;</c>), e il motore rilegge il file. Così un'aerovia spezzata diventa davvero tre record (il pezzo, il
/// <c>BREAK</c>, l'altro pezzo) come le legge Aurora, e spezzare e riunire torna al file di prima byte per byte.
/// </summary>
public static class Interruzioni
{
    /// <summary>Come si interrompe quell'elenco, o null se lì non si spezza.</summary>
    public static FormaDellInterruzione? Forma(FileAperto file, int indice, string campo)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Record(file, indice) switch
        {
            SidProcedure when campo == "Track" => FormaDellInterruzione.RigaVuota,
            ProcedureStrRecord when campo == "Waypoints" => FormaDellInterruzione.Br,
            GeometricStrRecord when campo.StartsWith("Segments[", StringComparison.Ordinal) => FormaDellInterruzione.Br,
            StaticBoundaryGroup when campo.StartsWith("Polygons[", StringComparison.Ordinal) => FormaDellInterruzione.Dummy,
            Airway when campo == nameof(Airway.FixLabels) => FormaDellInterruzione.Break,
            MvaSector when campo == nameof(MvaSector.Vertices) && !DiAcc(file) => FormaDellInterruzione.RigaVuota,
            _ => null,
        };
    }

    /// <summary>
    /// Perché in quell'elenco non si spezza, dove il perché conta: nelle MVA di ACC il <c>T;DUMMY</c> chiude la zona e
    /// il motore non lo tiene in mezzo (sul fork 169 in coda, nessuno in mezzo). Slice 15: resta così — una zona è un
    /// poligono solo con la sua etichetta, e due zone sono due blocchi (si aggiunge una zona, non si spezza questa).
    /// </summary>
    public static string? PercheNo(FileAperto file, int indice, string campo)
        => Record(file, indice) is MvaSector && campo == nameof(MvaSector.Vertices) && DiAcc(file)
            ? "Nelle MVA di ACC una zona è un poligono solo, chiuso dal suo T;DUMMY: per farne due si aggiunge una zona, non si spezza questa."
            : null;

    /// <summary>Le posizioni k dell'elenco dopo le quali, DENTRO l'elenco, la linea è interrotta (fra k e k+1).</summary>
    public static IReadOnlyList<int> Dentro(ElencoDiVertici elenco)
    {
        ArgumentNullException.ThrowIfNull(elenco);
        var dove = new List<int>();
        for (int k = 0; k + 1 < elenco.Quanti; k++)
        {
            if (elenco.Elenco[k + 1] is PuntoDelTracciato { NuovoTratto: true } or ProcedureWaypoint { IniziaUnTratto: true })
                dove.Add(k);
        }

        return dove;
    }

    /// <summary>Dove continua la linea dopo l'ultimo punto dell'elenco, oltre un'interruzione; null se finisce lì.</summary>
    public static Continuazione? Dopo(FileAperto file, int indice, string campo)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (Forma(file, indice, campo) is not { } forma || file is not IFileConRecord conRecord)
            return null;
        var record = conRecord.RecordDelModello[indice];

        if (Numero(campo) is { } i)
        {
            string prossimo = campo.Replace($"[{i}]", $"[{i + 1}]", StringComparison.Ordinal);
            return ElenchiDiVertici.Uno(file, indice, prossimo) is { Quanti: > 0 } e ? new Continuazione(indice, prossimo, e.Nome) : null;
        }

        var dopo = indice + 1 < conRecord.RecordDelModello.Count ? conRecord.RecordDelModello[indice + 1] : null;
        switch (record)
        {
            case Airway aerovia when dopo is Airway { Name: var b } && EUnBreak(b)
                                     && indice + 2 < conRecord.RecordDelModello.Count
                                     && conRecord.RecordDelModello[indice + 2] is Airway { FixLabels.Count: > 0 } seguito
                                     && seguito.Name == aerovia.Name:
                return new Continuazione(indice + 2, campo, $"{seguito.Name}, dopo il BREAK (record {indice + 2})");

            case MvaSector zona when forma == FormaDellInterruzione.RigaVuota && dopo is MvaSector { Vertices.Count: > 0 } altra
                                     && altra.LabelAnchors.Count == 0 && altra.Nome == zona.Nome && zona.Nome.Length > 0:
                return new Continuazione(indice + 1, campo, $"{altra.Nome}, dopo la riga vuota (record {indice + 1})");

            default:
                return null;
        }
    }

    /// <summary>
    /// Le righe da cambiare per spezzare (o unire) la linea dopo il punto <paramref name="dopo"/> dell'elenco, nel file com'è
    /// adesso (<paramref name="righe"/>, <paramref name="posti"/>), e quanti record avrà il file dopo. Null col perché se
    /// il gesto lì non si fa.
    /// </summary>
    internal static (IReadOnlyDictionary<int, IReadOnlyList<string>> Sostituzioni, int Record)? Righe(
        FileAperto file, IReadOnlyList<string> righe, IReadOnlyList<(int Da, int Quante)> posti,
        int indice, string campo, int dopo, bool spezza, out string? perche)
    {
        perche = null;
        var conRecord = (IFileConRecord)file;
        if (Forma(file, indice, campo) is not { } forma)
        {
            perche = PercheNo(file, indice, campo) ?? "Questa linea non si spezza: le interruzioni stanno nelle SID, negli .str, nei confini, nelle aerovie e nelle MVA di scalo.";
            return null;
        }

        if (ElenchiDiVertici.Uno(file, indice, campo) is not { } elenco || dopo < 0 || dopo >= elenco.Quanti)
        {
            perche = "Quel punto non c'è.";
            return null;
        }

        var punti = RigheDeiPunti(conRecord.RecordDelModello[indice], righe, posti[indice]);
        int primo = Primo(file, indice, campo);
        if (punti is null || punti.Count != Quanti(conRecord.RecordDelModello[indice]))
        {
            perche = "Nel testo del file non trovo le righe dei punti di questo record: si spezza a mano, dalle righe del file.";
            return null;
        }

        int record = conRecord.RecordDelModello.Count;
        int a = punti[primo + dopo];
        int? b = null;
        int delta = 0;
        if (dopo + 1 < elenco.Quanti)
        {
            b = punti[primo + dopo + 1];
        }
        else if (!spezza && Dopo(file, indice, campo) is { } oltre)
        {
            if (oltre.Record == indice)
            {
                b = punti[primo + dopo + 1];
            }
            else
            {
                var suoi = RigheDeiPunti(conRecord.RecordDelModello[oltre.Record], righe, posti[oltre.Record]);
                if (suoi is not { Count: > 0 })
                {
                    perche = "Nel testo del file non trovo le righe del pezzo dopo.";
                    return null;
                }

                b = suoi[0];
                delta = oltre.Record - indice;
            }
        }

        if (b is not { } dopoB)
        {
            perche = spezza ? "Dopo l'ultimo punto non c'è niente da spezzare." : "Qui la linea non è interrotta.";
            return null;
        }

        var fra = Enumerable.Range(a + 1, dopoB - a - 1).ToList();
        // Il <br> si scrive come lo scrive il file: `…;<br>;` (liba.str) o `…;<br>` (lirf.str), quello che ha di più.
        bool chiuso = righe.Count(r => r.TrimEnd().EndsWith("<br>;", StringComparison.Ordinal))
                      > righe.Count(r => r.TrimEnd().EndsWith("<br>", StringComparison.Ordinal));
        var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
        bool giaSpezzata = forma switch
        {
            FormaDellInterruzione.RigaVuota => fra.Any(i => righe[i].Trim().Length == 0),
            FormaDellInterruzione.Br => Campo3(righe[dopoB]) == "<br>",
            FormaDellInterruzione.Dummy => fra.Any(i => EUnaRigaCon(righe[i], "DUMMY")),
            _ => fra.Any(i => EUnaRigaCon(righe[i], "BREAK")),
        };

        if (spezza == giaSpezzata)
        {
            perche = spezza ? "Qui la linea è già interrotta." : "Qui la linea non è interrotta.";
            return null;
        }

        // I numeri delle righe per CambiaRighe vanno da 1.
        if (spezza)
        {
            switch (forma)
            {
                case FormaDellInterruzione.RigaVuota:
                    sostituzioni[a + 1] = [righe[a], ""];
                    delta = Record(file, indice) is MvaSector ? 1 : 0;
                    break;
                case FormaDellInterruzione.Br when Campo3(righe[dopoB]) is { Length: > 0 } suffisso:
                    if (suffisso.StartsWith("//", StringComparison.Ordinal))
                    {
                        perche = "La riga del punto dopo ha un commento in coda: spostalo sopra, poi spezza.";
                        return null;
                    }

                    // Il suffisso (4E) e il <br> stanno nello stesso campo: come fanno i file (lime.str), il punto si ripete,
                    // una volta col <br> e una col suffisso.
                    sostituzioni[a + 1] = [righe[a], ConIlBr(righe[dopoB], chiuso: true)];
                    break;
                case FormaDellInterruzione.Br:
                    sostituzioni[dopoB + 1] = [ConIlBr(righe[dopoB], chiuso)];
                    break;
                case FormaDellInterruzione.Dummy:
                    sostituzioni[a + 1] = [righe[a], Separatore(righe[a], "DUMMY")];
                    break;
                default:
                    sostituzioni[a + 1] = [righe[a], Separatore(righe[a], "BREAK")];
                    delta = 2;
                    break;
            }
        }
        else
        {
            switch (forma)
            {
                case FormaDellInterruzione.RigaVuota:
                    foreach (int i in fra.Where(i => righe[i].Trim().Length == 0))
                        sostituzioni[i + 1] = [];
                    delta = -delta;
                    break;
                case FormaDellInterruzione.Br when dopoB + 1 < righe.Count && Campo3(righe[dopoB + 1]) is { Length: > 0 } s
                                                   && s != "<br>" && StessoPunto(righe[dopoB], righe[dopoB + 1]):
                    // Il punto ripetuto per tenere il suffisso: si toglie la sua copia col <br>.
                    sostituzioni[dopoB + 1] = [];
                    break;
                case FormaDellInterruzione.Br:
                    sostituzioni[dopoB + 1] = [SenzaIlBr(righe[dopoB])];
                    break;
                case FormaDellInterruzione.Dummy:
                    foreach (int i in fra.Where(i => EUnaRigaCon(righe[i], "DUMMY")))
                        sostituzioni[i + 1] = [];
                    break;
                default:
                    foreach (int i in fra.Where(i => EUnaRigaCon(righe[i], "BREAK")))
                        sostituzioni[i + 1] = [];
                    delta = -delta;
                    break;
            }
        }

        return (sostituzioni, record + delta);
    }

    /// <summary>Il nome del punto dopo il quale si spezza, per la voce delle modifiche.</summary>
    internal static string NomeDelPunto(ElencoDiVertici elenco, int dopo) => elenco.Scrivi(dopo);

    private static object? Record(FileAperto file, int indice)
        => file is IFileConRecord r && indice >= 0 && indice < r.RecordDelModello.Count ? r.RecordDelModello[indice] : null;

    private static bool DiAcc(FileAperto file) => file.Relativo.Contains("/ENRMVA/", StringComparison.OrdinalIgnoreCase);

    private static bool EUnBreak(string nome) => string.Equals(nome.Trim(), "BREAK", StringComparison.OrdinalIgnoreCase);

    /// <summary>Il numero dentro la chiave di un elenco di elenchi (<c>Polygons[2].Vertices</c> → 2), o null.</summary>
    private static int? Numero(string campo)
    {
        int apre = campo.IndexOf('[', StringComparison.Ordinal), chiude = campo.IndexOf(']', StringComparison.Ordinal);
        return apre >= 0 && chiude > apre && int.TryParse(campo.AsSpan(apre + 1, chiude - apre - 1), out int n) ? n : null;
    }

    /// <summary>Dove comincia quell'elenco fra i punti del record messi in fila (i poligoni e i tratti uno dopo l'altro).</summary>
    private static int Primo(FileAperto file, int indice, string campo)
    {
        if (Numero(campo) is not { } n)
            return 0;
        string prima = campo[..campo.IndexOf('[', StringComparison.Ordinal)];
        return ElenchiDiVertici.Di(file, indice)
            .Where(e => e.Chiave.StartsWith(prima + "[", StringComparison.Ordinal) && Numero(e.Chiave) < n)
            .Sum(e => e.Quanti);
    }

    /// <summary>Quanti punti ha il record, messi in fila: quante righe di punti deve avere nel testo.</summary>
    private static int Quanti(object record) => record switch
    {
        SidProcedure s => s.Track.Count,
        ProcedureStrRecord p => p.Waypoints.Count,
        GeometricStrRecord g => g.Segments.Sum(t => t.Points.Count),
        HoldingStrRecord h => h.Points.Count,
        Airway a => a.FixLabels.Count + a.Coordinates.Count,
        StaticBoundaryGroup c => c.Polygons.Sum(p => p.Vertices.Count),
        MvaSector m => m.Vertices.Count,
        _ => -1,
    };

    /// <summary>
    /// Gli indici, nelle righe del file, delle righe dei punti del record, in fila: le righe di dati, tolte la testa (SID
    /// e .str), i separatori DUMMY e BREAK, e nelle MVA le righe L.
    /// </summary>
    internal static List<int>? RigheDeiPunti(object record, IReadOnlyList<string> righe, (int Da, int Quante) posto)
    {
        var dati = Enumerable.Range(posto.Da, posto.Quante)
            .Where(i => i < righe.Count && righe[i].Trim() is { Length: > 0 } t && !t.StartsWith("//", StringComparison.Ordinal))
            .ToList();
        return record switch
        {
            SidProcedure or StrRecord => [.. dati.Skip(1)],
            Airway => dati,
            StaticBoundaryGroup => [.. dati.Where(i => !EUnaRigaCon(righe[i], "DUMMY"))],
            MvaSector => [.. dati.Where(i => righe[i].TrimStart().StartsWith("T;", StringComparison.OrdinalIgnoreCase) && !EUnaRigaCon(righe[i], "DUMMY"))],
            _ => null,
        };
    }

    /// <summary>Una riga <c>T;NOME;…</c> col 2° campo dato (DUMMY, BREAK), in qualunque maiuscola.</summary>
    private static bool EUnaRigaCon(string riga, string nome)
    {
        string t = riga.Trim();
        if (t.StartsWith("//", StringComparison.Ordinal))
            return false;
        var campi = t.Split(';');
        return campi.Length > 1 && campi[0].Trim() is "T" or "t" && string.Equals(campi[1].Trim(), nome, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>La riga che separa, sul punto della riga di prima: <c>T;BREAK;RIVAM;RIVAM;</c>, come nei file.</summary>
    private static string Separatore(string riga, string nome)
    {
        var campi = riga.Trim().Split(';');
        return $"T;{nome};{campi.ElementAtOrDefault(2)?.Trim()};{campi.ElementAtOrDefault(3)?.Trim()};";
    }

    /// <summary>Il 3° campo di una riga di punto di un <c>.str</c> (il suffisso o il &lt;br&gt;), tolti gli spazi; vuoto se non c'è.</summary>
    private static string Campo3(string riga)
    {
        var campi = riga.Split(';');
        return campi.Length > 2 ? campi[2].Trim() : "";
    }

    private static string ConIlBr(string riga, bool chiuso)
    {
        var campi = riga.TrimEnd().Split(';').ToList();
        while (campi.Count < 3)
            campi.Add("");
        campi[2] = "<br>";
        if (chiuso && campi.Count == 3)
            campi.Add("");
        return string.Join(';', campi);
    }

    private static string SenzaIlBr(string riga)
    {
        var campi = riga.TrimEnd().Split(';').ToList();
        campi.RemoveAt(2);
        string senza = string.Join(';', campi);
        return senza.EndsWith(';') ? senza : senza + ";";
    }

    private static bool StessoPunto(string una, string altra)
    {
        var x = una.Split(';');
        var y = altra.Split(';');
        return x.Length > 1 && y.Length > 1 && x[0].Trim() == y[0].Trim() && x[1].Trim() == y[1].Trim();
    }
}
