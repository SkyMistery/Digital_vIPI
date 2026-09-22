using System.Text.RegularExpressions;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// I tag <c>//@</c> dei <c>.sid</c> e dei <c>.str</c> (carta madre §8.2, decisi il 21 settembre 2026; carta F2 slice 7):
/// lettura e scrittura in un posto solo, perché il catalogo delle chiavi è un contratto fra il Lab (scrive) e vIPI
/// (legge).
/// </summary>
/// <remarks>
/// <para>La forma di un blocco, per un record:</para>
/// <code>
/// //@source=AIRAC2610                       (del file, nelle prime righe)
/// //@BANA6W fix=BANAV initialclimb=5000     (la dichiarazione: il NOME del record, poi le chiavi)
/// //@START
/// LIRF;25;BANA6W;…                          (il record: una riga di .sid, o intestazione e corpo di .str)
/// //@END BANA6W
/// </code>
/// <para>La dichiarazione si aggancia col NOME: se il record sotto ne ha un altro è un errore, e le chiavi non gli
/// si attaccano (un AOD che cancella una riga a mano non sposta i metadati sul vicino). <c>//@START</c>/<c>//@END</c>
/// sono facoltativi per la lettura (una dichiarazione subito sopra il record basta); la scrittura li mette sempre.
/// Il nome può avere spazi (<c>LIRF CTR</c> dei MAPS): arriva fino alla prima parola con <c>=</c>.</para>
/// <para>I tag sono commenti per Aurora e per i lettori: stanno fra le righe grezze o nei commenti di testa dei
/// record, e un <c>//@</c> chiude sempre il record aperto (<c>StrParser</c>, <c>SidParser</c>). Un file senza tag
/// si legge come prima: sul master del 22 settembre 2026 le righe <c>//@</c> sono zero.</para>
/// </remarks>
public static partial class Metadati
{
    /// <summary>Le chiavi di un record: il nome intero del fix (<c>BANA6W</c> è BANAV) e l'initial climb.</summary>
    public static IReadOnlyList<string> ChiaviDelRecord { get; } = new[] { "fix", "initialclimb" };

    /// <summary>Le chiavi del file: il ciclo AIRAC da cui vengono i dati.</summary>
    public static IReadOnlyList<string> ChiaviDelFile { get; } = new[] { "source" };

    /// <summary>Il nome col quale si aggancia una SID: il terzo campo (<c>OST1E</c>, <c>SOS5A-ESI8H</c>).</summary>
    public static string NomeSid(SidProcedure sid) => (sid ?? throw new ArgumentNullException(nameof(sid))).Name.Trim();

    /// <summary>Il nome col quale si aggancia un record di <c>.str</c>: il terzo campo (<c>BULL1A</c>, <c>LIRF CTR</c>).</summary>
    public static string NomeStr(StrRecord str) => (str ?? throw new ArgumentNullException(nameof(str))).ProcedureId.Trim();

    /// <summary>Vero se la riga (già senza spazi in testa) è un tag <c>//@</c>.</summary>
    public static bool EUnTag(string rigaSenzaSpaziInTesta)
        => rigaSenzaSpaziInTesta.StartsWith("//@", StringComparison.Ordinal);

    /// <summary>Legge i tag di un file già letto. Non tocca niente.</summary>
    public static MetadatiDelFile<T> Leggi<T>(ParseResult<T> letto, Func<T, string> nomeDi)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(letto);
        ArgumentNullException.ThrowIfNull(nomeDi);

        var delFile = new Dictionary<string, string>(StringComparer.Ordinal);
        PosizioneDelTag? sorgente = null;
        var perRecord = new List<MetadatiDelRecord<T>>();
        var problemi = new List<ProblemaDeiMetadati>();

        // La dichiarazione in attesa del suo record, e il blocco aperto da //@START.
        (string Nome, Dictionary<string, string> Chiavi, int Riga, string Testo, PosizioneDelTag Dove)? inAttesa = null;
        (string Nome, int Riga, string Testo, bool ConRecord)? blocco = null;
        MetadatiDelRecord<T>? delBlocco = null;
        bool vistoUnRecord = false;
        int numero = 0;

        void Orfana()
        {
            if (inAttesa is { } d)
            {
                problemi.Add(new(TipoDiProblemaDeiMetadati.DichiarazioneOrfana, d.Riga, d.Testo));
                inAttesa = null;
            }
        }

        void Riga(string riga, PosizioneDelTag dove)
        {
            numero++;
            string t = riga.Trim();
            if (t.Length == 0)
            {
                // «Subito sopra la riga dati»: una riga vuota fra la dichiarazione e il record la lascia orfana.
                Orfana();
                return;
            }

            if (!EUnTag(t))
            {
                return;
            }

            var tag = Analizza(t[3..].Trim());
            switch (tag.Tipo)
            {
                case TipoDiTag.Illegibile:
                    problemi.Add(new(TipoDiProblemaDeiMetadati.RigaIllegibile, numero, riga));
                    break;

                case TipoDiTag.Start:
                    if (blocco is { } aperto)
                    {
                        problemi.Add(new(TipoDiProblemaDeiMetadati.StartSenzaEnd, aperto.Riga, aperto.Testo));
                        blocco = null;
                    }

                    if (inAttesa is { } d)
                    {
                        blocco = (d.Nome, numero, riga, false);
                    }
                    else
                    {
                        problemi.Add(new(TipoDiProblemaDeiMetadati.StartSenzaDichiarazione, numero, riga));
                    }

                    break;

                case TipoDiTag.End:
                    if (blocco is not { } chiuso)
                    {
                        problemi.Add(new(TipoDiProblemaDeiMetadati.EndSenzaStart, numero, riga));
                        break;
                    }

                    if (tag.Nome is { } nomeEnd && nomeEnd != chiuso.Nome)
                    {
                        problemi.Add(new(TipoDiProblemaDeiMetadati.EndConAltroNome, numero, riga));
                    }
                    else if (delBlocco is not null && chiuso.ConRecord)
                    {
                        delBlocco.Delimitato = true;
                    }

                    Orfana();
                    blocco = null;
                    delBlocco = null;
                    break;

                case TipoDiTag.ChiaviDelFile:
                    if (vistoUnRecord || inAttesa is not null || blocco is not null)
                    {
                        problemi.Add(new(TipoDiProblemaDeiMetadati.ChiaveDiFileFuoriPosto, numero, riga));
                        break;
                    }

                    foreach (var (chiave, valore) in tag.Chiavi)
                    {
                        if (!ChiaviDelFile.Contains(chiave))
                        {
                            problemi.Add(new(TipoDiProblemaDeiMetadati.ChiaveSconosciuta, numero, riga));
                        }

                        delFile[chiave] = valore;
                        if (chiave == "source")
                        {
                            sorgente = dove;
                        }
                    }

                    break;

                case TipoDiTag.Dichiarazione:
                    Orfana();
                    if (tag.Chiavi.Keys.Any(k => !ChiaviDelRecord.Contains(k)))
                    {
                        problemi.Add(new(TipoDiProblemaDeiMetadati.ChiaveSconosciuta, numero, riga));
                    }

                    inAttesa = (tag.Nome!, tag.Chiavi, numero, riga, dove);
                    break;
            }
        }

        void Record(RecordChunk<T> chunk)
        {
            numero += chunk.RawLines.Length + (chunk.HasMarkers ? 2 : 0);
            vistoUnRecord = true;
            string nome = nomeDi(chunk.Record);

            if (inAttesa is { } d)
            {
                inAttesa = null;
                if (d.Nome != nome)
                {
                    problemi.Add(new(TipoDiProblemaDeiMetadati.NomeNonCombacia, d.Riga, d.Testo));
                    return;
                }

                var metadati = new MetadatiDelRecord<T>(chunk.Record, nome, d.Chiavi, d.Riga, d.Dove);
                perRecord.Add(metadati);
                if (blocco is { } aperto && aperto.Nome == d.Nome && !aperto.ConRecord)
                {
                    blocco = aperto with { ConRecord = true };
                    delBlocco = metadati;
                }

                return;
            }

            // Un secondo record dentro il blocco di un altro nome: il blocco non è il suo.
            if (blocco is { } dentro && dentro.Nome != nome)
            {
                problemi.Add(new(TipoDiProblemaDeiMetadati.NomeNonCombacia, dentro.Riga, dentro.Testo));
            }
        }

        for (int p = 0; p < letto.Chunks.Count; p++)
        {
            switch (letto.Chunks[p])
            {
                case RawChunk<T> raw:
                    for (int i = 0; i < raw.Lines.Length; i++)
                    {
                        Riga(raw.Lines[i], new PosizioneDelTag(p, InTesta: false, i));
                    }

                    break;

                case RecordChunk<T> record:
                    for (int i = 0; i < record.LeadingComments.Length; i++)
                    {
                        Riga(record.LeadingComments[i], new PosizioneDelTag(p, InTesta: true, i));
                    }

                    Record(record);
                    break;
            }
        }

        Orfana();
        if (blocco is { } maiChiuso)
        {
            problemi.Add(new(TipoDiProblemaDeiMetadati.StartSenzaEnd, maiChiuso.Riga, maiChiuso.Testo));
        }

        problemi.Sort((a, b) => a.Riga.CompareTo(b.Riga));
        return new MetadatiDelFile<T>(delFile, perRecord, problemi, sorgente);
    }

    /// <summary>
    /// Scrive i metadati di <paramref name="record"/>: la dichiarazione col suo nome e le <paramref name="chiavi"/>, dentro
    /// <c>//@START</c>/<c>//@END</c>. Se il record li ha già cambia solo la riga della dichiarazione (e aggiunge
    /// START/END se mancavano); le altre righe del file restano com'erano. Restituisce il file nuovo: quello passato
    /// non si tocca.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Il file ha tag che non valgono (<see cref="MetadatiDelFile{T}.Problemi"/> con un errore: scrivere sopra un blocco
    /// rotto lo romperebbe di più), o il nome del record non si può dichiarare.
    /// </exception>
    /// <exception cref="ArgumentException">Una chiave fuori dal catalogo, o un valore vuoto o con spazi.</exception>
    public static ParseResult<T> Scrivi<T>(ParseResult<T> letto, T record, Func<T, string> nomeDi, IReadOnlyDictionary<string, string> chiavi)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(chiavi);
        foreach (var (chiave, valore) in chiavi)
        {
            if (!ChiaviDelRecord.Contains(chiave))
            {
                throw new ArgumentException($"Chiave fuori dal catalogo dei record: '{chiave}'.", nameof(chiavi));
            }

            ControllaIlValore(valore, nameof(chiavi));
        }

        var metadati = Leggi(letto, nomeDi);
        RifiutaSeRotto(metadati);

        int pezzo = IndiceDel(letto, record);
        string nome = nomeDi(record);
        if (nome.Length == 0 || nome.Contains('=', StringComparison.Ordinal) || Analizza(nome).Tipo != TipoDiTag.Dichiarazione)
        {
            throw new InvalidOperationException($"Il nome '{nome}' non si può dichiarare in un tag //@.");
        }

        string dichiarazione = "//@" + nome + string.Concat(chiavi
            .OrderBy(c => ChiaviDelRecord.ToList().IndexOf(c.Key))
            .Select(c => " " + c.Key + "=" + c.Value));

        var pezzi = letto.Chunks.ToList();
        var esistenti = metadati.Di(record);
        if (esistenti is not null)
        {
            SostituisciLaRiga(pezzi, esistenti.Dichiarazione, dichiarazione, altre: esistenti.Delimitato ? null : "//@START");
            if (!esistenti.Delimitato)
            {
                MettiLaFine(pezzi, pezzo, nome);
            }
        }
        else
        {
            var rec = (RecordChunk<T>)pezzi[pezzo];
            pezzi[pezzo] = Copia(rec, testa: rec.LeadingComments.Append(dichiarazione).Append("//@START").ToArray());
            MettiLaFine(pezzi, pezzo, nome);
        }

        return letto with { Chunks = pezzi };
    }

    /// <summary>
    /// Scrive il ciclo AIRAC del file (<c>//@source=AIRAC2610</c>): cambia la riga se c'è, o la mette in cima.
    /// Restituisce il file nuovo.
    /// </summary>
    public static ParseResult<T> ScriviSorgente<T>(ParseResult<T> letto, Func<T, string> nomeDi, string valore)
        where T : class
    {
        ControllaIlValore(valore, nameof(valore));
        var metadati = Leggi(letto, nomeDi);
        RifiutaSeRotto(metadati);

        string riga = "//@source=" + valore;
        var pezzi = letto.Chunks.ToList();
        if (metadati.Sorgente is { } dove)
        {
            SostituisciLaRiga(pezzi, dove, riga, altre: null);
        }
        else if (pezzi.Count == 0)
        {
            pezzi.Add(new RawChunk<T>(new[] { riga }));
        }
        else if (pezzi[0] is RecordChunk<T> primo)
        {
            pezzi[0] = Copia(primo, testa: primo.LeadingComments.Prepend(riga).ToArray());
        }
        else
        {
            pezzi[0] = new RawChunk<T>(((RawChunk<T>)pezzi[0]).Lines.Prepend(riga));
        }

        return letto with { Chunks = pezzi };
    }

    private static void ControllaIlValore(string valore, string parametro)
    {
        if (string.IsNullOrEmpty(valore) || valore.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"Valore vuoto o con spazi: '{valore}'.", parametro);
        }
    }

    private static void RifiutaSeRotto<T>(MetadatiDelFile<T> metadati)
        where T : class
    {
        if (metadati.Problemi.FirstOrDefault(p => p.EUnErrore) is { } errore)
        {
            throw new InvalidOperationException(
                $"Il file ha tag //@ che non valgono ({errore.Tipo}, riga {errore.Riga}: «{errore.Testo}»): vanno sistemati prima.");
        }
    }

    private static int IndiceDel<T>(ParseResult<T> letto, T record)
        where T : class
    {
        for (int p = 0; p < letto.Chunks.Count; p++)
        {
            if (letto.Chunks[p] is RecordChunk<T> c && ReferenceEquals(c.Record, record))
            {
                return p;
            }
        }

        throw new ArgumentException("Il record non è di questo file.", nameof(record));
    }

    // Sostituisce la riga in quella posizione e, se `altre` c'è, ci mette sotto un'altra riga.
    private static void SostituisciLaRiga<T>(List<FileChunk<T>> pezzi, PosizioneDelTag dove, string riga, string? altre)
    {
        IEnumerable<string> Nuove(string[] vecchie)
        {
            for (int i = 0; i < vecchie.Length; i++)
            {
                yield return i == dove.Indice ? riga : vecchie[i];
                if (i == dove.Indice && altre is not null)
                {
                    yield return altre;
                }
            }
        }

        pezzi[dove.Pezzo] = pezzi[dove.Pezzo] switch
        {
            RecordChunk<T> rec when dove.InTesta => Copia(rec, testa: Nuove(rec.LeadingComments).ToArray()),
            RawChunk<T> raw => new RawChunk<T>(Nuove(raw.Lines)),
            _ => throw new InvalidOperationException("Posizione del tag non valida."),
        };
    }

    // `//@END NOME` subito dopo l'ultima riga del record. Le righe vuote in coda (un .str tiene nel record le
    // righe vuote fino all'intestazione dopo) escono dal record e vanno dopo la fine del blocco.
    private static void MettiLaFine<T>(List<FileChunk<T>> pezzi, int pezzo, string nome)
    {
        string fine = "//@END " + nome;
        var rec = (RecordChunk<T>)pezzi[pezzo];
        int vuoteInCoda = rec.RawLines.Reverse().TakeWhile(r => r.Trim().Length == 0).Count();

        if (vuoteInCoda > 0 && vuoteInCoda < rec.RawLines.Length)
        {
            pezzi[pezzo] = Copia(rec, righe: rec.RawLines[..^vuoteInCoda]);
            pezzi.Insert(pezzo + 1, new RawChunk<T>(rec.RawLines[^vuoteInCoda..].Prepend(fine)));
        }
        else if (pezzo + 1 < pezzi.Count && pezzi[pezzo + 1] is RecordChunk<T> dopo)
        {
            pezzi[pezzo + 1] = Copia(dopo, testa: dopo.LeadingComments.Prepend(fine).ToArray());
        }
        else if (pezzo + 1 < pezzi.Count && pezzi[pezzo + 1] is RawChunk<T> grezze)
        {
            pezzi[pezzo + 1] = new RawChunk<T>(grezze.Lines.Prepend(fine));
        }
        else
        {
            pezzi.Insert(pezzo + 1, new RawChunk<T>(new[] { fine }));
        }
    }

    // Un pezzo nuovo con le stesse righe e la stessa base: il file passato a Scrivi non cambia.
    private static RecordChunk<T> Copia<T>(RecordChunk<T> rec, string[]? testa = null, string[]? righe = null)
        => new(rec.Record, righe ?? rec.RawLines, rec.HasMarkers, testa ?? rec.LeadingComments)
        {
            Base = rec.Base,
        };

    private enum TipoDiTag { Illegibile, Start, End, ChiaviDelFile, Dichiarazione }

    private readonly record struct Tag(TipoDiTag Tipo, string? Nome, Dictionary<string, string> Chiavi);

    // Il testo dopo `//@`: START, END [NOME], chiave=valore … (del file), o NOME [chiave=valore …].
    private static Tag Analizza(string corpo)
    {
        var nessuna = new Dictionary<string, string>(StringComparer.Ordinal);
        if (corpo.Length == 0)
        {
            return new(TipoDiTag.Illegibile, null, nessuna);
        }

        if (corpo == "START")
        {
            return new(TipoDiTag.Start, null, nessuna);
        }

        if (corpo == "END" || corpo.StartsWith("END ", StringComparison.Ordinal))
        {
            string nomeEnd = corpo[3..].Trim();
            return new(TipoDiTag.End, nomeEnd.Length > 0 ? nomeEnd : null, nessuna);
        }

        var parole = Parole().Matches(corpo);
        int primaChiave = -1;
        for (int i = 0; i < parole.Count; i++)
        {
            if (parole[i].Value.Contains('=', StringComparison.Ordinal))
            {
                primaChiave = i;
                break;
            }
        }

        var chiavi = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = primaChiave < 0 ? parole.Count : primaChiave; i < parole.Count; i++)
        {
            string parola = parole[i].Value;
            int uguale = parola.IndexOf('=', StringComparison.Ordinal);
            if (uguale <= 0 || uguale == parola.Length - 1 || !chiavi.TryAdd(parola[..uguale], parola[(uguale + 1)..]))
            {
                return new(TipoDiTag.Illegibile, null, nessuna);
            }
        }

        if (primaChiave == 0)
        {
            return new(TipoDiTag.ChiaviDelFile, null, chiavi);
        }

        string nome = primaChiave < 0 ? corpo : corpo[..parole[primaChiave].Index].TrimEnd();
        return new(TipoDiTag.Dichiarazione, nome, chiavi);
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex Parole();
}
