using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>
/// Una parte di una voce: un record, o un poligono di un record (i confini, dove un gruppo T ha più poligoni separati
/// dal <c>T;DUMMY</c>). Il nome viene dal commento subito sopra (<c>//LSAG-LFMM</c>, <c>//X01-X02</c>).
/// </summary>
/// <param name="Chiave">Come si accende e spegne sulla mappa: <c>3</c> il record, <c>3.1</c> il suo secondo poligono.</param>
/// <param name="RigaDelNome">La riga (da 1, nel file di adesso) del commento che dà il nome; null se non ce l'ha.</param>
/// <param name="PrimaRiga">La prima riga della parte (da 0): un nome nuovo va nel commento sopra di lei.</param>
public sealed record ParteDellaVoce(string Chiave, int Record, int? Poligono, string? Nome, int? RigaDelNome, int PrimaRiga = 0);

/// <summary>
/// Una voce come nella finestra di selezione di Aurora (lotto «Subito» slice 6, «file per file» A3, J1, B1, E1, H3):
/// <i>ACC Selection</i> e <i>MVA Selection</i> hanno una voce per nome (anche in più pezzi); un'aerovia è una voce coi
/// suoi pezzi e le sue etichette; nei <c>.geo</c> e nei <c>.pol</c>, senza nome nelle righe, la voce è il gruppo sotto un
/// commento (H3).
/// </summary>
/// <param name="Record">Tutti i record della voce, in ordine.</param>
/// <param name="Parti">Le parti con un senso proprio (poligoni, zone, pezzi); vuoto dove la voce è un blocco solo.</param>
/// <param name="RigaDelNome">Dove il nome è un commento (i <c>.geo</c>): la sua riga, da 1.</param>
/// <param name="NomeMancante">Il nome non c'è, o è quello che mette Google Earth («Percorso senza titolo», H3).</param>
/// <param name="PrimaRiga">Dove il nome è un commento: la prima riga del gruppo (da 0), sotto il commento nuovo.</param>
public sealed record VoceDellaSelezione(string Nome, IReadOnlyList<int> Record, IReadOnlyList<ParteDellaVoce> Parti,
                                        int? RigaDelNome = null, bool NomeMancante = false, int? PrimaRiga = null)
{
    /// <summary>Vero se il nome della voce è un commento (i gruppi dei .geo e dei .pol): si cambia dalla scheda.</summary>
    public bool NomeDalCommento => PrimaRiga is not null;
}

/// <summary>Le voci della finestra di selezione di un file, calcolate sul testo com'è adesso.</summary>
public static class VociDellaSelezione
{
    /// <summary>Il nome della voce delle zone MVA senza il gruppo (2° campo vuoto): non è scritto nel file.</summary>
    public const string SenzaGruppo = "(senza gruppo)";

    /// <summary>Le voci del file, o null se il file non ha una finestra di selezione (fix, SID, settori dinamici…).</summary>
    public static IReadOnlyList<VoceDellaSelezione>? Di(FileAperto file, IReadOnlyList<string> righe, IReadOnlyList<(int Da, int Quante)> posti)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(posti);
        if (file is not IFileConRecord conRecord || conRecord.RecordDelModello.Count == 0)
            return null;
        var record = conRecord.RecordDelModello;

        return record[0] switch
        {
            StaticBoundaryGroup or LabelPoint => Confini(record, righe, posti),
            MvaSector => Mva(conRecord, record, righe, posti),
            Airway => Aerovie(record, righe, posti),
            Line { Nome: not null } => AreePerNome(record),
            Line or Polygon => PerCommento(record, righe, posti),
            _ => record.Any(r => r is StaticBoundaryGroup) ? Confini(record, righe, posti) : null,
        };
    }

    /// <summary>
    /// Il gruppo nel quale un record senza nome suo riceve i metadati (§M, «un pezzo che riceve il primo dato passa da
    /// commento a blocco»): la voce dei <c>.geo</c> e dei <c>.pol</c> col nome dal commento. Null col perché se non c'è,
    /// o se il suo nome è mancante (un blocco «Percorso senza titolo» non ritroverebbe niente).
    /// </summary>
    public static VoceDellaSelezione? GruppoDelRecord(FileAperto file, int indice, out string? perche)
    {
        ArgumentNullException.ThrowIfNull(file);
        perche = "Questo record non ha un nome suo, e non sta in un gruppo col nome dal commento: i suoi metadati non si attaccano a niente.";
        if (file is not IFileConRecord conRecord)
            return null;
        var voce = Di(file, conRecord.RigheDelFile([]), conRecord.PostiDeiRecord([]))?.FirstOrDefault(v => v.Record.Contains(indice));
        if (voce is null || !voce.NomeDalCommento)
            return null;
        if (voce.NomeMancante)
        {
            perche = $"Il gruppo si chiama «{voce.Nome}»: il nome del blocco è quello del gruppo, prima dagli un nome vero (sezione «Voce»).";
            return null;
        }

        perche = null;
        return voce;
    }

    /// <summary>Le voci che hanno un nome mancante («… senza titolo», o nessun commento): H3.</summary>
    public static bool SenzaTitolo(string? nome)
        => nome is null || nome.Contains("senza titolo", StringComparison.OrdinalIgnoreCase);

    // --- confini: una voce per nome del gruppo T, le parti sono i poligoni ---------------------------------------

    private static List<VoceDellaSelezione> Confini(IReadOnlyList<object> record, IReadOnlyList<string> righe, IReadOnlyList<(int Da, int Quante)> posti)
    {
        var voci = new List<(string Nome, List<int> Record, List<ParteDellaVoce> Parti)>();
        var etichette = new List<int>();
        for (int r = 0; r < record.Count; r++)
        {
            if (record[r] is LabelPoint)
            {
                etichette.Add(r);
                continue;
            }

            if (record[r] is not StaticBoundaryGroup gruppo)
                continue;
            string nome = gruppo.Name.Trim();
            var voce = voci.FirstOrDefault(v => v.Nome == nome);
            if (voce.Nome is null)
                voci.Add(voce = (nome, [], []));
            voce.Record.Add(r);

            // Il primo punto di ogni poligono: le righe di dati del record, tolti i DUMMY, messe in fila.
            var punti = Interruzioni.RigheDeiPunti(gruppo, righe, posti[r]) ?? [];
            int primo = 0;
            for (int p = 0; p < gruppo.Polygons.Count; p++)
            {
                int? riga = primo < punti.Count ? punti[primo] : null;
                var (testo, dove) = riga is { } i ? CommentoSopra(righe, i) : (null, null);
                voce.Parti.Add(new ParteDellaVoce($"{r}.{p}", r, p, testo, dove, riga ?? posti[r].Da));
                primo += gruppo.Polygons[p].Vertices.Count;
            }
        }

        var fatte = voci.Select(v => new VoceDellaSelezione(v.Nome, v.Record, v.Parti)).ToList();
        if (etichette.Count > 0)
            fatte.Add(new VoceDellaSelezione("Etichette (L)", etichette, []));
        return fatte;
    }

    // --- MVA: una voce per gruppo (il 2° campo), le parti sono le zone ------------------------------------------

    private static List<VoceDellaSelezione> Mva(IFileConRecord file, IReadOnlyList<object> record, IReadOnlyList<string> righe, IReadOnlyList<(int Da, int Quante)> posti)
    {
        var voci = new List<(string Nome, List<int> Record, List<ParteDellaVoce> Parti)>();
        for (int r = 0; r < record.Count; r++)
        {
            if (record[r] is not MvaSector zona)
                continue;
            string nome = zona.Nome.Length > 0 ? zona.Nome : SenzaGruppo;
            var voce = voci.FirstOrDefault(v => v.Nome == nome);
            if (voce.Nome is null)
                voci.Add(voce = (nome, [], []));
            voce.Record.Add(r);

            // Il nome della zona: il soprannome del blocco (E1, zone=), poi il commento sopra.
            var (testo, dove) = CommentoSopra(righe, posti[r].Da);
            string? soprannome = file.ChiaviDi(r)?.GetValueOrDefault("zone")?.Trim('"');
            voce.Parti.Add(new ParteDellaVoce($"{r}", r, null, soprannome ?? testo, soprannome is null ? dove : null, posti[r].Da));
        }

        return [.. voci.Select(v => new VoceDellaSelezione(v.Nome, v.Record, v.Parti))];
    }

    // --- aerovie: una voce per aerovia, le parti sono i pezzi (fra i BREAK) e le etichette ----------------------

    private static List<VoceDellaSelezione> Aerovie(IReadOnlyList<object> record, IReadOnlyList<string> righe, IReadOnlyList<(int Da, int Quante)> posti)
    {
        var voci = new List<(string Nome, List<int> Record, List<ParteDellaVoce> Parti)>();
        for (int r = 0; r < record.Count; r++)
        {
            if (record[r] is not Airway aerovia || string.Equals(aerovia.Name.Trim(), "BREAK", StringComparison.OrdinalIgnoreCase))
                continue;
            string nome = aerovia.Name.Trim();
            var voce = voci.FirstOrDefault(v => v.Nome == nome);
            if (voce.Nome is null)
                voci.Add(voce = (nome, [], []));
            voce.Record.Add(r);
            var (testo, dove) = CommentoSopra(righe, posti[r].Da);
            string cosa = aerovia.FixLabels.Count > 0 ? "tracciato" : "etichette";
            voce.Parti.Add(new ParteDellaVoce($"{r}", r, null, testo is null ? null : $"{cosa} · {testo}", dove, posti[r].Da));
        }

        return [.. voci.OrderBy(v => v.Nome, StringComparer.Ordinal).Select(v => new VoceDellaSelezione(v.Nome, v.Record, v.Parti))];
    }

    // --- aree P/R/D: una voce per area (il 6° campo, G2) ------------------------------------------------------

    private static List<VoceDellaSelezione> AreePerNome(IReadOnlyList<object> record)
        => [.. record.Select((r, i) => (Nome: (r as Line)?.Nome?.Trim() ?? "", Indice: i))
                     .GroupBy(v => v.Nome, StringComparer.Ordinal)
                     .Select(g => new VoceDellaSelezione(g.Key.Length > 0 ? g.Key : "(senza nome)", [.. g.Select(v => v.Indice)], [],
                                                         NomeMancante: g.Key.Length == 0))];

    // --- .geo e .pol: la voce è il gruppo sotto un commento (H3) -----------------------------------------------

    private static List<VoceDellaSelezione> PerCommento(IReadOnlyList<object> record, IReadOnlyList<string> righe, IReadOnlyList<(int Da, int Quante)> posti)
    {
        var voci = new List<VoceDellaSelezione>();
        var suoi = new List<int>();
        string? nome = null;
        int? riga = null;
        int fine = 0;

        void Chiudi()
        {
            if (suoi.Count > 0)
                voci.Add(new VoceDellaSelezione(nome ?? "(senza nome)", [.. suoi], [], riga, SenzaTitolo(nome), posti[suoi[0]].Da));
            suoi.Clear();
        }

        for (int r = 0; r < record.Count; r++)
        {
            // Un commento fra il record di prima e questo apre un gruppo nuovo, col suo nome.
            bool commento = Enumerable.Range(fine, Math.Max(0, posti[r].Da - fine)).Any(i => EUnCommento(righe[i]));
            if (commento || r == 0)
            {
                Chiudi();
                (nome, riga) = CommentoSopra(righe, posti[r].Da, finoA: fine);
            }

            suoi.Add(r);
            fine = posti[r].Da + posti[r].Quante;
        }

        Chiudi();
        return voci;
    }

    // --- i commenti -------------------------------------------------------------------------------------------

    /// <summary>
    /// Il nome dal commento subito sopra la riga <paramref name="riga"/> (da 0): fra le righe di commento di fila,
    /// l'ultima corta che non è un titolo di sole barre (<c>// LINPZ1 VEKEN</c> e non la descrizione sotto). Con
    /// <paramref name="finoA"/> si possono saltare le righe vuote fino a lì (i gruppi dei <c>.geo</c>).
    /// </summary>
    private static (string? Nome, int? Riga) CommentoSopra(IReadOnlyList<string> righe, int riga, int? finoA = null)
    {
        // Il separatore subito sopra col nome in coda (`T;DUMMY;…; //brindisi/tirana`, FRA.artcc): il nome è quello.
        if (riga > 0 && righe[riga - 1].IndexOf("//", StringComparison.Ordinal) is var coda and > 0
            && righe[riga - 1].TrimStart().StartsWith("T;", StringComparison.OrdinalIgnoreCase)
            && righe[riga - 1].Split(';') is { Length: > 1 } campi && string.Equals(campi[1].Trim(), "DUMMY", StringComparison.OrdinalIgnoreCase)
            && Pulito(righe[riga - 1][coda..]) is { Length: > 0 } inCoda)
            return (inCoda, null);

        var sopra = new List<int>();
        for (int i = riga - 1; i >= Math.Max(0, finoA ?? 0); i--)
        {
            string t = righe[i].Trim();
            if (EUnCommento(righe[i]))
                sopra.Add(i);
            else if (t.Length == 0 && finoA is not null && sopra.Count == 0)
                continue;
            else
                break;
        }

        var buone = sopra.Where(i => Pulito(righe[i]).Length is > 0 and <= 40).ToList();
        int? scelta = buone.Count > 0 ? buone[0] : sopra.Count > 0 ? sopra[0] : null;
        return scelta is { } s && Pulito(righe[s]) is { Length: > 0 } nome ? (nome, s + 1) : (null, null);
    }

    /// <summary>Un commento che dà un nome: non un tag <c>//@</c>, non una riga di dati commentata (quella è nascosta).</summary>
    private static bool EUnCommento(string riga)
    {
        string t = riga.TrimStart();
        return t.StartsWith("//", StringComparison.Ordinal) && !t.StartsWith("//@", StringComparison.Ordinal) && !Nascosti.Commentata(riga);
    }

    /// <summary>Il testo del commento senza le barre davanti (<c>////CONF 4</c> → <c>CONF 4</c>) e gli spazi.</summary>
    internal static string Pulito(string riga) => riga.Trim().TrimStart('/').Trim().TrimEnd('/').Trim();
}
