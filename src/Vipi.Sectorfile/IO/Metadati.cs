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
/// //@"BANA6W" fix=BANAV initialclimb=5000   (la dichiarazione: il NOME del record, poi le chiavi)
/// //@START
/// LIRF;25;BANA6W;…                          (il record: una riga di .sid, o intestazione e corpo di .str)
/// //@END "BANA6W"
/// </code>
/// <para>La dichiarazione si aggancia col NOME: se il record sotto ne ha un altro è un errore, e le chiavi non gli
/// si attaccano (un AOD che cancella una riga a mano non sposta i metadati sul vicino). <c>//@START</c>/<c>//@END</c>
/// sono facoltativi per la lettura (una dichiarazione subito sopra il record basta); la scrittura li mette sempre.
/// Il nome si scrive <b>fra virgolette</b> (F3-bis D4, 23 settembre 2026: il nome e le chiavi si distinguono a occhio,
/// e i nomi delle mappe hanno spazi, <c>//@"STAR RNAV(ALL)" composta=…</c>). Si legge anche senza, come lo scriveva
/// F2: allora il nome arriva fino alla prima parola con <c>=</c> (<c>//@LIRF CTR fix=X</c>).</para>
/// <para>I tag sono commenti per Aurora e per i lettori: stanno fra le righe grezze o nei commenti di testa dei
/// record, e un <c>//@</c> chiude sempre il record aperto (<c>StrParser</c>, <c>SidParser</c>). Un file senza tag
/// si legge come prima: sul master del 22 settembre 2026 le righe <c>//@</c> sono zero.</para>
/// <para>La sintassi è quella della carta «file per file» §M (27 settembre 2026): un valore con spazi o una voce
/// con spazi in un elenco vanno <b>fra virgolette</b> (<c>initialclimb="COO APP"</c>,
/// <c>compose=ODIN4E,25:"RNP10 UPETI"</c>); i valori si conservano come sono scritti (virgolette comprese) e si
/// leggono con <see cref="Testo"/> e <see cref="ElencoDellaComposta"/>. Le chiavi ammesse dipendono dal tipo di
/// file (<see cref="CatalogoDeiTag"/>). Le righe <c>//@@</c> sono i tag di un punto dentro il record: qui non si
/// leggono ancora, e non sono una dichiarazione.</para>
/// </remarks>
public static partial class Metadati
{
    /// <summary>
    /// Le procedure di una mappa composta (F3-bis §2.2: <c>compose=ODINA4E,25:NENI5A</c>, vedi
    /// <see cref="ElencoDellaComposta"/>). Era <c>composta</c> fino al 27 settembre 2026 (§M regola 9: chiavi in
    /// inglese; nel sector vero i tag erano zero, niente da migrare).
    /// </summary>
    public const string Compose = "compose";

    /// <summary>Come si disegnano le procedure di una composta: <c>whole=si</c>, ognuna intera (D8 rivista; era <c>intere</c>).</summary>
    public const string Whole = "whole";

    /// <summary>
    /// Vero se una procedura con quel nome può stare nell'elenco di <see cref="Compose"/>: un nome con spazi, virgole
    /// o due punti si scrive fra virgolette (§M regola 5); resta fuori solo un nome vuoto o con le virgolette dentro.
    /// Fino al 27 settembre 2026 ne restavano fuori 63 su 1169 (<c>RNP10 UPETI</c> di <c>lica.str</c>, le rotte
    /// <c>AAR …</c> di <c>lizz.str</c>).
    /// </summary>
    public static bool NomeElencabile(string nome)
        => !string.IsNullOrEmpty(nome) && nome.Trim() == nome && !nome.Contains('"', StringComparison.Ordinal);

    /// <summary>
    /// Le procedure di una mappa composta, dal valore di <see cref="Compose"/> (F3-bis D5): voci separate da virgola,
    /// nell'ordine in cui si disegnano; <c>25:NENI5A</c> sceglie la procedura della pista 25, il nome da solo le
    /// prende tutte; un nome fra virgolette può avere spazi, virgole e due punti (<c>25:"RNP10 UPETI"</c>).
    /// Null se il valore non si legge (una voce vuota, una pista vuota, virgolette che non chiudono).
    /// </summary>
    public static IReadOnlyList<ProceduraDellaComposta>? ElencoDellaComposta(string valore)
    {
        ArgumentNullException.ThrowIfNull(valore);
        if (Voci(valore) is not { } voci)
        {
            return null;
        }

        var elenco = new List<ProceduraDellaComposta>();
        foreach (string voce in voci)
        {
            int virgoletta = voce.IndexOf('"', StringComparison.Ordinal);
            int duePunti = voce.IndexOf(':', StringComparison.Ordinal);
            if (virgoletta >= 0 && duePunti > virgoletta)
            {
                duePunti = -1;  // i due punti stanno dentro il nome fra virgolette
            }

            string pista = duePunti < 0 ? string.Empty : voce[..duePunti];
            string nome = duePunti < 0 ? voce : voce[(duePunti + 1)..];
            if (nome.StartsWith('"'))
            {
                if (nome.Length < 3 || !nome.EndsWith('"') || nome[1..^1].Contains('"', StringComparison.Ordinal))
                {
                    return null;
                }

                nome = nome[1..^1];
            }
            else if (nome.Contains(':', StringComparison.Ordinal) || nome.Contains('"', StringComparison.Ordinal))
            {
                return null;
            }

            if (nome.Length == 0 || (duePunti >= 0 && pista.Length == 0) || pista.Contains('"', StringComparison.Ordinal))
            {
                return null;
            }

            elenco.Add(new ProceduraDellaComposta(pista.Length == 0 ? null : pista, nome));
        }

        return elenco;
    }

    /// <summary>
    /// Il valore di <see cref="Compose"/> per un elenco: le voci con la virgola, la pista davanti coi due punti, e fra
    /// virgolette i nomi che hanno spazi, virgole o due punti. È l'inverso di <see cref="ElencoDellaComposta"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Un nome che non può stare nell'elenco (<see cref="NomeElencabile"/>).</exception>
    public static string ScriviLElenco(IEnumerable<ProceduraDellaComposta> elenco)
    {
        ArgumentNullException.ThrowIfNull(elenco);
        return string.Join(",", elenco.Select(v =>
        {
            if (!NomeElencabile(v.Nome))
            {
                throw new ArgumentException($"«{v.Nome}» non può stare nell'elenco di una composta.", nameof(elenco));
            }

            string nome = v.Nome.Any(c => char.IsWhiteSpace(c) || c is ',' or ':' or '=') ? FraVirgolette(v.Nome) : v.Nome;
            return (v.Pista is null ? string.Empty : v.Pista + ":") + nome;
        }));
    }

    /// <summary>
    /// Un valore da scrivere in un tag: così com'è se non ha spazi, altrimenti fra virgolette (§M regola 5).
    /// </summary>
    /// <exception cref="ArgumentException">Vuoto, o con le virgolette dentro.</exception>
    public static string ValoreDaScrivere(string testo)
    {
        ArgumentNullException.ThrowIfNull(testo);
        if (testo.Length == 0 || testo.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException($"Un valore di tag non può essere vuoto né avere virgolette: «{testo}».", nameof(testo));
        }

        return testo.Any(char.IsWhiteSpace) ? FraVirgolette(testo) : testo;
    }

    /// <summary>Il testo di un valore letto: senza le virgolette, se è tutto fra virgolette (<c>"COO APP"</c> → COO APP).</summary>
    public static string Testo(string valore)
    {
        ArgumentNullException.ThrowIfNull(valore);
        return valore.Length >= 2 && valore[0] == '"' && valore[^1] == '"' && valore.Count(c => c == '"') == 2
            ? valore[1..^1]
            : valore;
    }

    /// <summary>Le chiavi del file: il ciclo AIRAC da cui vengono i dati.</summary>
    public static IReadOnlyList<string> ChiaviDelFile => CatalogoDeiTag.DelFile;

    /// <summary>Il nome col quale si aggancia una SID: il terzo campo (<c>OST1E</c>, <c>SOS5A-ESI8H</c>).</summary>
    public static string NomeSid(SidProcedure sid) => (sid ?? throw new ArgumentNullException(nameof(sid))).Name.Trim();

    /// <summary>Il nome col quale si aggancia un record di <c>.str</c>: il terzo campo (<c>BULL1A</c>, <c>LIRF CTR</c>).</summary>
    public static string NomeStr(StrRecord str) => (str ?? throw new ArgumentNullException(nameof(str))).ProcedureId.Trim();

    /// <summary>Vero se la riga (già senza spazi in testa) è un tag <c>//@</c>, anche di un punto (<c>//@@</c>).</summary>
    public static bool EUnTag(string rigaSenzaSpaziInTesta)
        => rigaSenzaSpaziInTesta.StartsWith("//@", StringComparison.Ordinal);

    /// <summary>
    /// Vero se la riga (già senza spazi in testa) è il tag di un punto, <c>//@@"PUNTO" …</c> (§M regola 4): sta dentro
    /// il record, sopra il suo punto, e — a differenza di un <c>//@</c> — non lo chiude.
    /// </summary>
    public static bool EUnTagDiPunto(string rigaSenzaSpaziInTesta)
        => rigaSenzaSpaziInTesta.StartsWith("//@@", StringComparison.Ordinal);

    /// <summary>Legge i tag di un file già letto, col catalogo del suo tipo di record. Non tocca niente.</summary>
    public static MetadatiDelFile<T> Leggi<T>(ParseResult<T> letto, Func<T, string> nomeDi)
        where T : class
        => Leggi(letto, nomeDi, CatalogoDelTipo<T>());

    /// <summary>Legge i tag di un file già letto, con le chiavi di <paramref name="catalogo"/>. Non tocca niente.</summary>
    public static MetadatiDelFile<T> Leggi<T>(ParseResult<T> letto, Func<T, string> nomeDi, CatalogoDeiTag catalogo)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(letto);
        ArgumentNullException.ThrowIfNull(nomeDi);
        ArgumentNullException.ThrowIfNull(catalogo);

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

            // Il tag di un punto non è una dichiarazione: sta nel record, e lo legge chi legge i punti.
            if (!EUnTag(t) || EUnTagDiPunto(t))
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
                    if (tag.Chiavi.Keys.Any(k => !catalogo.AmmetteDelRecord(k)))
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
    /// <exception cref="ArgumentException">
    /// Una chiave fuori dal catalogo del file, o un valore che non si scrive: vuoto, o con spazi fuori dalle virgolette
    /// (<see cref="ValoreDaScrivere"/> mette le virgolette dove servono).
    /// </exception>
    public static ParseResult<T> Scrivi<T>(ParseResult<T> letto, T record, Func<T, string> nomeDi, IReadOnlyDictionary<string, string> chiavi)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(chiavi);
        var catalogo = CatalogoDelTipo<T>();
        foreach (var (chiave, valore) in chiavi)
        {
            if (!catalogo.AmmetteDelRecord(chiave))
            {
                throw new ArgumentException($"Chiave fuori dal catalogo dei {catalogo.Formato}: '{chiave}'.", nameof(chiavi));
            }

            ControllaIlValore(valore, nameof(chiavi));
        }

        var metadati = Leggi(letto, nomeDi, catalogo);
        RifiutaSeRotto(metadati);

        int pezzo = IndiceDel(letto, record);
        string nome = nomeDi(record);
        if (nome.Length == 0 || nome.Contains('"', StringComparison.Ordinal) || nome.Trim() != nome)
        {
            throw new InvalidOperationException($"Il nome '{nome}' non si può dichiarare in un tag //@.");
        }

        // Le chiavi nell'ordine del catalogo, quelle per verso dopo (in ordine di scrittura): lo stesso tag esce
        // sempre uguale, chiunque lo scriva.
        string dichiarazione = "//@" + FraVirgolette(nome) + string.Concat(chiavi
            .OrderBy(c => catalogo.DelRecord.Contains(c.Key) ? catalogo.DelRecord.ToList().IndexOf(c.Key) : int.MaxValue)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
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
    /// Toglie i metadati di <paramref name="record"/>: la dichiarazione, <c>//@START</c> e <c>//@END</c> (F3-bis slice 5:
    /// una mappa che non è più composta). Le altre righe restano com'erano, e un file al quale si mette e poi si toglie
    /// un tag torna uguale byte per byte. Restituisce il file nuovo; se il record non ha tag, quello di prima.
    /// </summary>
    /// <exception cref="InvalidOperationException">Il file ha tag che non valgono: toglierne uno lo romperebbe di più.</exception>
    public static ParseResult<T> Togli<T>(ParseResult<T> letto, T record, Func<T, string> nomeDi)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(record);
        var metadati = Leggi(letto, nomeDi);
        RifiutaSeRotto(metadati);
        if (metadati.Di(record) is not { } esistenti)
        {
            return letto;
        }

        var pezzi = letto.Chunks.ToList();
        int pezzo = IndiceDel(letto, record);

        // Prima la fine (dopo il record), poi la dichiarazione (prima): togliere righe non sposta i pezzi.
        if (esistenti.Delimitato)
        {
            TogliLaFine(pezzi, pezzo);
        }

        TogliLaRiga(pezzi, esistenti.Dichiarazione, ancheLoStartSotto: esistenti.Delimitato);
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

    // Un valore si scrive se, riletto, è una parola sola: niente spazi fuori dalle virgolette, virgolette che chiudono.
    private static void ControllaIlValore(string valore, string parametro)
    {
        if (string.IsNullOrEmpty(valore) || Parole(valore) is not [var unaSola] || unaSola.Testo != valore)
        {
            throw new ArgumentException($"Valore vuoto, con spazi fuori dalle virgolette o virgolette che non chiudono: '{valore}'.", parametro);
        }
    }

    private static CatalogoDeiTag CatalogoDelTipo<T>()
        => CatalogoDeiTag.Di<T>()
           ?? throw new NotSupportedException($"I record di tipo {typeof(T).Name} non hanno ancora un catalogo di tag.");

    // Le voci di un elenco, separate dalle virgole fuori dalle virgolette. Null se una virgoletta non chiude.
    private static List<string>? Voci(string valore)
    {
        var voci = new List<string>();
        int inizio = 0;
        bool dentro = false;
        for (int i = 0; i <= valore.Length; i++)
        {
            if (i == valore.Length || (valore[i] == ',' && !dentro))
            {
                voci.Add(valore[inizio..i]);
                inizio = i + 1;
            }
            else if (valore[i] == '"')
            {
                dentro = !dentro;
            }
        }

        return dentro ? null : voci;
    }

    private readonly record struct Parola(int Indice, string Testo);

    // Le parole di un tag: separate dagli spazi FUORI dalle virgolette (§M regola 5: «RR NE» fra virgolette è una
    // parola sola). Null se una virgoletta resta aperta.
    private static List<Parola>? Parole(string testo)
    {
        var parole = new List<Parola>();
        int inizio = -1;
        bool dentro = false;
        for (int i = 0; i <= testo.Length; i++)
        {
            bool fine = i == testo.Length || (char.IsWhiteSpace(testo[i]) && !dentro);
            if (fine)
            {
                if (inizio >= 0)
                {
                    parole.Add(new Parola(inizio, testo[inizio..i]));
                    inizio = -1;
                }

                continue;
            }

            if (inizio < 0)
            {
                inizio = i;
            }

            if (testo[i] == '"')
            {
                dentro = !dentro;
            }
        }

        return dentro ? null : parole;
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

    // Toglie la riga in quella posizione e, se c'è subito sotto, il suo //@START.
    private static void TogliLaRiga<T>(List<FileChunk<T>> pezzi, PosizioneDelTag dove, bool ancheLoStartSotto)
    {
        string[] Senza(string[] vecchie)
        {
            int quante = ancheLoStartSotto && dove.Indice + 1 < vecchie.Length && vecchie[dove.Indice + 1].Trim() == "//@START" ? 2 : 1;
            return vecchie.Take(dove.Indice).Concat(vecchie.Skip(dove.Indice + quante)).ToArray();
        }

        pezzi[dove.Pezzo] = pezzi[dove.Pezzo] switch
        {
            RecordChunk<T> rec when dove.InTesta => Copia(rec, testa: Senza(rec.LeadingComments)),
            RawChunk<T> raw => new RawChunk<T>(Senza(raw.Lines)),
            _ => throw new InvalidOperationException("Posizione del tag non valida."),
        };
    }

    // Toglie il `//@END` che chiude il record: la prima riga del pezzo dopo, come lo mette MettiLaFine.
    private static void TogliLaFine<T>(List<FileChunk<T>> pezzi, int pezzo)
    {
        static bool EUnaFine(string riga) => riga.Trim().StartsWith("//@END", StringComparison.Ordinal);

        if (pezzo + 1 >= pezzi.Count)
        {
            return;
        }

        pezzi[pezzo + 1] = pezzi[pezzo + 1] switch
        {
            RawChunk<T> raw when raw.Lines.Length > 0 && EUnaFine(raw.Lines[0]) => new RawChunk<T>(raw.Lines.Skip(1)),
            RecordChunk<T> rec when rec.LeadingComments.Length > 0 && EUnaFine(rec.LeadingComments[0])
                => Copia(rec, testa: rec.LeadingComments[1..]),
            var altro => altro,
        };
    }

    // `//@END NOME` subito dopo l'ultima riga del record. Le righe vuote in coda (un .str tiene nel record le
    // righe vuote fino all'intestazione dopo) escono dal record e vanno dopo la fine del blocco.
    private static void MettiLaFine<T>(List<FileChunk<T>> pezzi, int pezzo, string nome)
    {
        string fine = "//@END " + FraVirgolette(nome);
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
            if (nomeEnd.StartsWith('"'))
            {
                if (nomeEnd.Length < 3 || !nomeEnd.EndsWith('"') || nomeEnd[1..^1].Contains('"', StringComparison.Ordinal))
                {
                    return new(TipoDiTag.Illegibile, null, nessuna);
                }

                nomeEnd = nomeEnd[1..^1];
            }

            return new(TipoDiTag.End, nomeEnd.Length > 0 ? nomeEnd : null, nessuna);
        }

        // Il nome fra virgolette (D4): tutto fino alla virgoletta che chiude, poi solo chiavi.
        if (corpo.StartsWith('"'))
        {
            int chiude = corpo.IndexOf('"', 1);
            if (chiude <= 1 || (chiude + 1 < corpo.Length && !char.IsWhiteSpace(corpo[chiude + 1]))
                || Parole(corpo[(chiude + 1)..]) is not { } dopo || LeggiLeChiavi(dopo) is not { } chiaviDopo)
            {
                return new(TipoDiTag.Illegibile, null, nessuna);
            }

            return new(TipoDiTag.Dichiarazione, corpo[1..chiude], chiaviDopo);
        }

        if (Parole(corpo) is not { } parole)
        {
            return new(TipoDiTag.Illegibile, null, nessuna);
        }

        int primaChiave = parole.FindIndex(p => p.Testo.Contains('=', StringComparison.Ordinal));
        if (LeggiLeChiavi(primaChiave < 0 ? [] : parole.GetRange(primaChiave, parole.Count - primaChiave)) is not { } chiavi)
        {
            return new(TipoDiTag.Illegibile, null, nessuna);
        }

        if (primaChiave == 0)
        {
            return new(TipoDiTag.ChiaviDelFile, null, chiavi);
        }

        string nome = primaChiave < 0 ? corpo : corpo[..parole[primaChiave].Indice].TrimEnd();
        return new(TipoDiTag.Dichiarazione, nome, chiavi);
    }

    // Le chiavi di un tag, ogni parola «chiave=valore»: null se una parola non lo è (niente «=», chiave o valore
    // vuoti, virgolette nella chiave) o se una chiave si ripete. Il valore resta com'è scritto, virgolette comprese.
    private static Dictionary<string, string>? LeggiLeChiavi(List<Parola> parole)
    {
        var chiavi = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parola in parole)
        {
            int uguale = parola.Testo.IndexOf('=', StringComparison.Ordinal);
            if (uguale <= 0 || uguale == parola.Testo.Length - 1 || parola.Testo[..uguale].Contains('"', StringComparison.Ordinal)
                || parola.Testo[(uguale + 1)..] == "\"\"" || !chiavi.TryAdd(parola.Testo[..uguale], parola.Testo[(uguale + 1)..]))
            {
                return null;
            }
        }

        return chiavi;
    }

    private static string FraVirgolette(string nome) => "\"" + nome + "\"";
}
