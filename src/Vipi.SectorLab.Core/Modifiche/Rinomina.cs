using System.Text.RegularExpressions;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// La rinomina di un punto (lotto «Subito» slice 7b, «file per file» L2): come cambia UNA riga. Chi cambiare lo dice
/// «chi lo usa» (<see cref="Sessione.ChiLoUsa"/>); qui c'è solo il testo — i campi uguali al nome, il fix nella
/// descrizione di un'attesa, i tag che nominano il punto —, e il resto della riga resta com'era, spazi compresi.
/// </summary>
public static partial class Rinomina
{
    /// <summary>
    /// Le righe da cambiare, file per file (il file del punto per primo): la dichiarazione del punto e delle sue copie,
    /// ogni citazione che «chi lo usa» gli dà, i tag che lo nominano. Oppure il perché no (<see cref="ModificaRifiutata"/>),
    /// o la domanda sui VOR/NDB omonimi (<see cref="RinominaDaDecidere"/>).
    /// </summary>
    /// <param name="omonimi">Con un VOR e un NDB dello stesso nome, le righe che valgono per tutti e due: vero = si
    /// rinominano anche loro, falso = restano; null = non si è ancora deciso, e la rinomina lo chiede (committente, 28
    /// settembre: «deve chiedere»).</param>
    public static object Prepara(SessioneAperta sessione, ChiLoUsa indice, IReadOnlyDictionary<string, CatalogoDeiPunti> cataloghi,
                                 string file, int record, string vecchio, string? nuovo, bool? omonimi,
                                 Func<string, IEnumerable<object>> sporchiDi)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(indice);
        ArgumentNullException.ThrowIfNull(cataloghi);
        ArgumentNullException.ThrowIfNull(sporchiDi);
        if (indice.Di(sessione, cataloghi, file, record, sporchiDi) is not { } usi)
            return new ModificaRifiutata("Questo record non è un punto che si cita per nome.");
        string? suo = usi.Nomi.FirstOrDefault(n => string.Equals(n, vecchio?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (suo is null)
            return new ModificaRifiutata($"«{vecchio}» non è un nome di questo punto.");
        nuovo = nuovo?.Trim();
        if (PercheNonVa(nuovo) is { } perche)
            return new ModificaRifiutata(perche);
        if (string.Equals(nuovo, suo, StringComparison.Ordinal))
            return new ModificaRifiutata("Il nome è già questo.");

        // Un nome che c'è già farebbe due punti con lo stesso nome (NomeDuplicato): Aurora ne prenderebbe uno.
        if (!string.Equals(nuovo, suo, StringComparison.OrdinalIgnoreCase))
        {
            if (usi.Catalogo == "attesa")
            {
                if (sessione.File.Values.OfType<IFileConRecord>().SelectMany(f => f.RecordDelModello).OfType<Attesa>()
                        .Any(a => string.Equals(a.Nome.Trim(), nuovo, StringComparison.OrdinalIgnoreCase)))
                    return new ModificaRifiutata($"C'è già un'attesa {nuovo}.");
            }
            else if (cataloghi.Values.Select(c => c.Cerca(nuovo!)).FirstOrDefault(p => p is not null) is { } gia)
            {
                return new ModificaRifiutata($"C'è già un {gia.Catalogo} {gia.Nome} in {gia.File[(gia.File.LastIndexOf('/') + 1)..]}: "
                                             + "due punti con lo stesso nome, e Aurora ne prenderebbe uno.");
            }
        }

        var citazioni = usi.Citazioni.Where(c => usi.Nomi.Count == 1 || CitaIlNome(c.Testo, suo)).ToList();
        // Le righe comuni: valgono anche per un altro punto — un VOR/NDB omonimo, o lo stesso nome che in un altro
        // master è un altro punto (la citazione è sua «solo in» alcuni). Rinominarle toglie il nome a quell'altro.
        static bool Comune(Citazione c) => c.AncheA is not null || c.SoloIn.Count > 0;
        if (citazioni.FirstOrDefault(Comune) is { } comune)
        {
            if (omonimi is null)
            {
                return new RinominaDaDecidere(
                    comune.AncheA ?? $"il {suo} che i master fuori da {string.Join(", ", comune.SoloIn)} risolvono in un altro punto",
                    citazioni.Count(Comune));
            }

            if (omonimi == false)
                citazioni.RemoveAll(Comune);
        }

        var perFile = new Dictionary<string, Dictionary<int, IReadOnlyList<string>>>(StringComparer.Ordinal);
        var righeDi = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        IReadOnlyList<string> Righe(string relativo)
        {
            if (!righeDi.TryGetValue(relativo, out var righe))
                righeDi[relativo] = righe = ((IFileConRecord)sessione.File[relativo]).RigheDelFile(sporchiDi(relativo));
            return righe;
        }

        void Metti(string relativo, int riga, string? nuova)
        {
            if (nuova is null)
                return;
            if (!perFile.TryGetValue(relativo, out var suoi))
                perFile[relativo] = suoi = [];
            suoi[riga + 1] = [nuova];
        }

        // La dichiarazione: il punto e le sue copie, coi tag del record sopra.
        foreach (var (relativo, k) in indice.Copie(sessione, file, record, suo).Prepend((file, record)))
        {
            var conRecord = (IFileConRecord)sessione.File[relativo];
            var righe = Righe(relativo);
            var (da, quante) = conRecord.PostiDeiRecord(sporchiDi(relativo))[k];
            for (int i = da; i < da + quante && i < righe.Count; i++)
                Metti(relativo, i, NeiCampi(righe[i], suo, nuovo!));
            for (int i = da - 1; i >= 0 && righe[i].TrimStart().StartsWith("//", StringComparison.Ordinal); i--)
                Metti(relativo, i, NeiTag(righe[i], suo, nuovo!, dichiarazione: true));
        }

        // Chi lo usa: le righe delle citazioni, e i tag dei punti //@@ nei file che lo citano.
        foreach (var citazione in citazioni)
        {
            string riga = Righe(citazione.File)[citazione.Riga - 1];
            Metti(citazione.File, citazione.Riga - 1,
                NeiCampi(riga, suo, nuovo!, attesa: citazione.Come == "attesa") ?? NeiTag(riga, suo, nuovo!, dichiarazione: false));
        }

        foreach (string relativo in citazioni.Select(c => c.File).Distinct(StringComparer.Ordinal))
        {
            var righe = Righe(relativo);
            for (int i = 0; i < righe.Count; i++)
            {
                if (righe[i].TrimStart().StartsWith("//@@", StringComparison.Ordinal) && !(perFile.GetValueOrDefault(relativo)?.ContainsKey(i + 1) ?? false))
                    Metti(relativo, i, NeiTag(righe[i], suo, nuovo!, dichiarazione: false));
            }
        }

        if (!perFile.ContainsKey(file))
            return new ModificaRifiutata("Non trovo il nome nelle righe del punto: si cambia a mano, dalle righe del file.");

        return new RinominaPronta(suo, nuovo!,
            [.. perFile.OrderBy(f => f.Key == file ? 0 : 1).ThenBy(f => f.Key, StringComparer.Ordinal)
                .Select(f => (f.Key, (IReadOnlyDictionary<int, IReadOnlyList<string>>)f.Value))]);
    }

    // Un VRP ha due nomi (il nome e il codice): delle sue citazioni si prendono quelle che nominano QUESTO.
    private static bool CitaIlNome(string riga, string nome)
        => riga.Split(';').Any(c => string.Equals(c.Trim(), nome, StringComparison.OrdinalIgnoreCase)
                                    || c.Trim().StartsWith(nome + "/", StringComparison.OrdinalIgnoreCase))
           || riga.Contains("=" + nome, StringComparison.OrdinalIgnoreCase)
           || riga.Contains("=\"" + nome + "\"", StringComparison.OrdinalIgnoreCase);

    /// <summary>Perché il nome nuovo non va, o null. Le regole di un nome nuovo, più quelle di un nome citato.</summary>
    public static string? PercheNonVa(string? nuovo)
    {
        if (OrdineAlfabetico.PercheNonVa(nuovo) is { } perche)
            return perche;
        if (nuovo!.Contains('/', StringComparison.Ordinal))
            return "Il nome non può avere la «/»: nella descrizione di un'attesa separa il fix dalla rotta (ABBOZ/225R-9000).";
        if (nuovo.Contains('"', StringComparison.Ordinal) || nuovo.Contains('@', StringComparison.Ordinal))
            return "Il nome non può avere le virgolette o la «@»: sono dei tag //@ del Lab.";
        if (!Punto.TryLeggi(nuovo, nuovo, out var punto) || !punto.PerNome)
            return "Sembra una coordinata, non un nome: Aurora lo leggerebbe come un punto.";
        return null;
    }

    /// <summary>
    /// La riga coi campi uguali a <paramref name="vecchio"/> (maiuscole indifferenti, come Aurora) diventati
    /// <paramref name="nuovo"/>; per un'attesa anche il fix della descrizione (<c>ABBOZ/225R-9000</c>). Null se non cambia.
    /// </summary>
    public static string? NeiCampi(string riga, string vecchio, string nuovo, bool attesa = false)
    {
        ArgumentNullException.ThrowIfNull(riga);
        if (riga.TrimStart().StartsWith("//", StringComparison.Ordinal))
            return null;
        string[] campi = riga.Split(';');
        bool cambiata = false;
        for (int i = 0; i < campi.Length; i++)
        {
            string pulito = campi[i].Trim();
            if (string.Equals(pulito, vecchio, StringComparison.OrdinalIgnoreCase))
            {
                campi[i] = campi[i].Replace(pulito, nuovo, StringComparison.Ordinal);
                cambiata = true;
            }
            else if (attesa && pulito.StartsWith(vecchio + "/", StringComparison.OrdinalIgnoreCase))
            {
                int dove = campi[i].IndexOf(pulito, StringComparison.Ordinal);
                campi[i] = campi[i][..dove] + nuovo + campi[i][(dove + vecchio.Length)..];
                cambiata = true;
            }
        }

        return cambiata ? string.Join(';', campi) : null;
    }

    /// <summary>
    /// Un tag che nomina il punto: la dichiarazione del suo record (<c>//@"LUSIL" …</c>, nel file che lo dichiara), un
    /// punto di una procedura (<c>//@@"LUSIL" …</c>) o il valore di <c>fix=</c> e <c>trans=</c> (§M, P6). Null se non cambia.
    /// </summary>
    /// <param name="dichiarazione">Vero nel file del punto (e delle sue copie): si rinomina <c>//@"NOME"</c>; negli
    /// altri file <c>//@"NOME"</c> è il nome di un ALTRO record (una SID che si chiama come il fix) e resta.</param>
    public static string? NeiTag(string riga, string vecchio, string nuovo, bool dichiarazione)
    {
        ArgumentNullException.ThrowIfNull(riga);
        string testa = riga.TrimStart();
        if (!testa.StartsWith("//@", StringComparison.Ordinal))
            return null;
        string v = Regex.Escape(vecchio);
        string fatta = riga;
        if (testa.StartsWith("//@@", StringComparison.Ordinal))
            fatta = Regex.Replace(fatta, $@"^(\s*//@@""){v}("")", m => m.Groups[1].Value + nuovo + m.Groups[2].Value, RegexOptions.IgnoreCase);
        else if (dichiarazione)
            fatta = Regex.Replace(fatta, $@"^(\s*//@""){v}("")", m => m.Groups[1].Value + nuovo + m.Groups[2].Value, RegexOptions.IgnoreCase);

        // I valori: fix=LUSIL, trans="LUSIL". La chiave in minuscolo come la scrive il catalogo (§M).
        fatta = Regex.Replace(fatta, $@"(?<=\s)((?:fix|trans)=)(""?){v}\2(?=\s|$)", m => m.Groups[1].Value + m.Groups[2].Value + nuovo + m.Groups[2].Value,
            RegexOptions.IgnoreCase);
        return fatta == riga ? null : fatta;
    }
}

/// <summary>La rinomina pronta: le righe da cambiare, file per file (il file del punto per primo).</summary>
public sealed record RinominaPronta(string Vecchio, string Nuovo, IReadOnlyList<(string File, IReadOnlyDictionary<int, IReadOnlyList<string>> Righe)> PerFile)
{
    public int Righe => PerFile.Sum(f => f.Righe.Count);
}

/// <summary>
/// Una rinomina con delle righe che valgono anche per un altro punto — un VOR e un NDB omonimi, o lo stesso nome che un
/// altro master risolve altrove —: si rinominano o no? Lo decide l'AOD (committente, 28 settembre: «deve chiedere»).
/// </summary>
/// <param name="AncheA">L'altro punto: «NDB TRP, NAVAIDS/itndb.ndb».</param>
/// <param name="Righe">Quante righe valgono anche per lui.</param>
public sealed record RinominaDaDecidere(string AncheA, int Righe);
