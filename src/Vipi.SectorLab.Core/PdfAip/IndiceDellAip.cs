namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Una voce nell'indice: il suo posto nello strato dell'AIP (per sceglierla), il titolo e il genere.</summary>
public sealed record VoceDellIndice(int Indice, string Titolo, string Genere);

/// <summary>Un gruppo dell'indice: una sezione («ENR 5.1»), uno scalo («LIRF»), o un genere dentro di loro («SID»).</summary>
public sealed record GruppoDellIndice(string Codice, string Nome, IReadOnlyList<GruppoDellIndice> Gruppi, IReadOnlyList<VoceDellIndice> Voci)
{
    /// <summary>Quante voci ci sono qui dentro, a ogni livello.</summary>
    public int Quante => Voci.Count + Gruppi.Sum(g => g.Quante);
}

/// <summary>
/// L'indice dell'AIP espanso (riordino delle pagine, D9): le voci dello strato messe come nell'AIP — la parte (ENR 2,
/// ENR 3, ENR 4, ENR 5, AD 2), la sezione («ENR 5.1 Aree vietate, regolamentate e pericolose»), e dentro una sezione con
/// più generi un gruppo per genere; AD 2 scalo per scalo, con lo scalo e le sue procedure. Si costruisce dallo strato
/// (<see cref="StratoDellAip"/>) e non dal modello, così l'indice di una voce è quello che sceglie la mappa.
/// </summary>
public static class IndiceDellAip
{
    /// <summary>
    /// I nomi delle sezioni, quelli dell'ICAO (Annesso 15) che l'AIP Italia segue. Le sottosezioni che l'Italia organizza a
    /// modo suo (ENR 3.x, ENR 5.3.x) non sono qui: hanno il solo codice, piuttosto che un nome inventato.
    /// </summary>
    private static readonly Dictionary<string, string> NomiDelleSezioni = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENR 2"] = "Spazi aerei ATS",
        ["ENR 2.1"] = "FIR, UIR, TMA e CTA",
        ["ENR 2.2"] = "Altri spazi aerei regolamentati",
        ["ENR 3"] = "Rotte ATS",
        ["ENR 4"] = "Radioassistenze e sistemi",
        ["ENR 4.1"] = "Radioassistenze in rotta",
        ["ENR 4.4"] = "Punti significativi",
        ["ENR 4.5"] = "Luci aeronautiche al suolo in rotta",
        ["ENR 5"] = "Avvisi alla navigazione",
        ["ENR 5.1"] = "Aree vietate, regolamentate e pericolose",
        ["ENR 5.2"] = "Aree di esercitazione militare e ADIZ",
        ["ENR 5.3"] = "Altre attività di natura pericolosa",
        ["AD 2"] = "Aeroporti",
    };

    /// <summary>L'ordine delle parti, come nell'AIP; quello che non è in una di queste va in fondo, «Altro».</summary>
    private static readonly string[] Parti = ["ENR 2", "ENR 3", "ENR 4", "ENR 5", "AD 2"];

    public static IReadOnlyList<GruppoDellIndice> Di(IReadOnlyList<VoceDellAip> voci, string? filtro = null)
    {
        ArgumentNullException.ThrowIfNull(voci);
        var nomiDegliScali = voci
            .Where(v => v.Genere == AipSullaMappa.Scali)
            .GroupBy(v => v.Titolo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Campo(g.First(), "Nome"), StringComparer.OrdinalIgnoreCase);

        // Si cerca nel titolo della voce e, in AD 2, anche nello scalo: «Fiumicino» trova LIRF con le sue procedure.
        bool Trovata(VoceDellAip voce)
        {
            if (string.IsNullOrWhiteSpace(filtro))
                return true;
            var cosa = filtro.Trim();
            if (voce.Titolo.Contains(cosa, StringComparison.OrdinalIgnoreCase))
                return true;
            if (Parte(voce) != "AD 2")
                return false;
            return (Scalo(voce) ?? ScaloDalNome(voce, nomiDegliScali)) is { } scalo
                   && nomiDegliScali.GetValueOrDefault(scalo) is { } nome
                   && nome.Contains(cosa, StringComparison.OrdinalIgnoreCase);
        }

        var scelte = voci
            .Select((v, i) => (Voce: v, Indice: i))
            .Where(x => Trovata(x.Voce))
            .ToList();

        var parti = new List<GruppoDellIndice>();
        foreach (var parte in scelte.GroupBy(x => Parte(x.Voce)).OrderBy(g => OrdineDellaParte(g.Key)))
        {
            List<GruppoDellIndice> gruppi;
            if (parte.Key == "AD 2")
            {
                // Una voce di AD 2 senza scalo (le ATZ: l'area non lo dice) va nel suo scalo se il nome lo trova; se no, in
                // un gruppo con la sua sezione.
                gruppi = [.. parte
                    .GroupBy(x => Scalo(x.Voce) ?? ScaloDalNome(x.Voce, nomiDegliScali) ?? Sezione(x.Voce) ?? "AD 2", StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => nomiDegliScali.ContainsKey(g.Key) ? 0 : 1)
                    .ThenBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => PerGenere(g.Key.ToUpperInvariant(), nomiDegliScali.GetValueOrDefault(g.Key) ?? "", g))];
            }
            else
            {
                gruppi = [.. parte
                    .GroupBy(x => Sezione(x.Voce) ?? parte.Key, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key, ComparatoreDelleSezioni.Uno)
                    .Select(g => PerGenere(g.Key, NomiDelleSezioni.GetValueOrDefault(g.Key) ?? "", g))];
            }
            parti.Add(new GruppoDellIndice(parte.Key, NomiDelleSezioni.GetValueOrDefault(parte.Key) ?? "", gruppi, []));
        }
        return parti;
    }

    /// <summary>Un gruppo con le sue voci; se dentro ci sono più generi (P, R, D; lo scalo e le SID), un sottogruppo per genere.</summary>
    private static GruppoDellIndice PerGenere(string codice, string nome, IEnumerable<(VoceDellAip Voce, int Indice)> voci)
    {
        var tutte = voci.ToList();
        var generi = tutte.GroupBy(x => x.Voce.Genere).ToList();
        if (generi.Count == 1)
            return new GruppoDellIndice(codice, nome, [], Ordinate(tutte));
        // Lo scalo stesso (la sua voce) sta in testa, fuori dai sottogruppi.
        var dirette = tutte.Where(x => x.Voce.Genere == AipSullaMappa.Scali).ToList();
        var sotto = generi
            .Where(g => g.Key != AipSullaMappa.Scali)
            .OrderBy(g => g.Min(x => x.Indice))
            .Select(g => new GruppoDellIndice($"{codice}/{g.Key}", g.Key, [], Ordinate(g)))
            .ToList();
        return new GruppoDellIndice(codice, nome, sotto, Ordinate(dirette));
    }

    private static List<VoceDellIndice> Ordinate(IEnumerable<(VoceDellAip Voce, int Indice)> voci) =>
        [.. voci.OrderBy(x => x.Voce.Titolo, ComparatoreDelleSezioni.Uno).Select(x => new VoceDellIndice(x.Indice, x.Voce.Titolo, x.Voce.Genere))];

    /// <summary>
    /// Lo scalo di un'area di AD 2 dal suo nome: «Roma Fiumicino ATZ» è di LIRF («ROMA Fiumicino»); «Napoli ATZ» di LIRN se
    /// un solo scalo ha un nome che comincia con «Napoli». Altrimenti null: meglio un gruppo a parte che uno scalo sbagliato.
    /// </summary>
    private static string? ScaloDalNome(VoceDellAip voce, IReadOnlyDictionary<string, string?> nomiDegliScali)
    {
        var nome = voce.Titolo.Trim();
        var spazio = nome.LastIndexOf(' ');
        if (spazio > 0 && nome[(spazio + 1)..] is "ATZ" or "CTR" or "RMZ" or "TMZ")
            nome = nome[..spazio].TrimEnd();
        var uguali = nomiDegliScali.Where(s => string.Equals(s.Value, nome, StringComparison.OrdinalIgnoreCase)).Select(s => s.Key).ToList();
        if (uguali.Count == 1)
            return uguali[0];
        if (nome.Contains(' ', StringComparison.Ordinal))
            return null;
        var perPrimaParola = nomiDegliScali
            .Where(s => s.Value is { } n && (n.Equals(nome, StringComparison.OrdinalIgnoreCase) || n.StartsWith(nome + " ", StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Key).ToList();
        return perPrimaParola.Count == 1 ? perPrimaParola[0] : null;
    }

    /// <summary>La parte dell'AIP: «ENR 5.1» → «ENR 5»; lo scalo e le procedure → «AD 2».</summary>
    public static string Parte(VoceDellAip voce)
    {
        ArgumentNullException.ThrowIfNull(voce);
        if (Scalo(voce) is not null)
            return "AD 2";
        if (Sezione(voce) is not { } sezione)
            return "Altro";
        var spazio = sezione.IndexOf(' ', StringComparison.Ordinal);
        if (spazio < 0)
            return "Altro";
        var numero = sezione[(spazio + 1)..];
        var punto = numero.IndexOf('.', StringComparison.Ordinal);
        var parte = $"{sezione[..spazio].ToUpperInvariant()} {(punto < 0 ? numero : numero[..punto])}";
        return Array.IndexOf(Parti, parte) >= 0 ? parte : "Altro";
    }

    /// <summary>La sezione stampata nella voce («ENR 5.1»), se c'è e se è una sezione (non il titolo di una procedura).</summary>
    private static string? Sezione(VoceDellAip voce) =>
        Campo(voce, "Sezione") is { } s && (s.StartsWith("ENR ", StringComparison.OrdinalIgnoreCase) || s.StartsWith("AD ", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("GEN ", StringComparison.OrdinalIgnoreCase)) && s.Length <= 12 ? s.Trim() : null;

    /// <summary>Lo scalo di una voce di AD 2: la voce dello scalo (il titolo è l'ICAO) o una procedura (il campo «Scalo»).</summary>
    private static string? Scalo(VoceDellAip voce) =>
        voce.Genere == AipSullaMappa.Scali ? voce.Titolo : Campo(voce, "Scalo");

    private static string? Campo(VoceDellAip voce, string nome) =>
        voce.Campi.FirstOrDefault(c => c.Nome == nome) is { Valore: { Length: > 0 } valore } ? valore : null;

    private static int OrdineDellaParte(string parte) => Array.IndexOf(Parti, parte) is var i and >= 0 ? i : Parti.Length;

    /// <summary>
    /// «ENR 3.4.1» dopo «ENR 3.4», «ENR 5.10» dopo «ENR 5.2», «M726» dopo «M72», «LI R12» dopo «LI R2»: i numeri si confrontano
    /// come numeri, il resto come testo.
    /// </summary>
    private sealed class ComparatoreDelleSezioni : IComparer<string>
    {
        public static readonly ComparatoreDelleSezioni Uno = new();

        public int Compare(string? a, string? b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
                {
                    int fa = i, fb = j;
                    while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                    while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                    var na = a[fa..i].TrimStart('0');
                    var nb = b[fb..j].TrimStart('0');
                    var c = na.Length != nb.Length ? na.Length.CompareTo(nb.Length) : string.CompareOrdinal(na, nb);
                    if (c != 0) return c;
                }
                else
                {
                    var c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                    if (c != 0) return c;
                    i++;
                    j++;
                }
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }
    }
}
