using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>Un posto dove un nome è citato: il file, il record, la riga (da 1, nel file com'è adesso) e il suo testo.</summary>
/// <param name="Come">Come lo cita: «punto» (una coppia di campi, un vertice, un'etichetta), «attesa» (il fix della
/// descrizione di un'attesa), «rimanda all'attesa» (il campo di un fix o di un VOR), o la chiave del tag (<c>fix=</c>).</param>
/// <param name="SoloIn">I master in cui la citazione va a QUESTO punto, se non in tutti quelli che caricano il file
/// (negli altri lo stesso nome è un altro punto); vuoto = in tutti.</param>
public sealed record Citazione(string File, int Record, int Riga, string Testo, string Come, IReadOnlyList<string> SoloIn)
{
    /// <summary>Per una citazione di un altro punto: dove Aurora la risolve («ndb, NAVAIDS/itndb.ndb»), o «nessun punto».</summary>
    public string? VaA { get; init; }

    /// <summary>
    /// Se un VOR e un NDB hanno lo stesso nome (TRP, PAN…: capita, e la riga non dice quale dei due), l'altro dei due:
    /// «NDB TRP, NAVAIDS/itndb.ndb». La citazione si mostra in tutti e due (committente, 28 settembre).
    /// </summary>
    public string? AncheA { get; init; }
}

/// <summary>Chi usa un punto: le citazioni che vanno a lui, e quelle dello stesso nome che vanno a un altro punto.</summary>
/// <param name="Catalogo">«fix», «vor», «ndb», «scalo», «vrp», «attesa», «posizione» o «pista».</param>
/// <param name="Nomi">I nomi con cui lo si cita (il VRP ne ha due: il nome e il codice).</param>
/// <param name="AltroPunto">Lo stesso nome citato in file dove Aurora lo risolve in un altro punto (o in nessuno):
/// non sono sue, e una rinomina non le tocca.</param>
public sealed record UsiDelPunto(
    string Catalogo, IReadOnlyList<string> Nomi, IReadOnlyList<Citazione> Citazioni, IReadOnlyList<Citazione> AltroPunto)
{
    /// <summary>I file che lo citano, in ordine.</summary>
    public IReadOnlyList<string> File => [.. Citazioni.Select(c => c.File).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}

/// <summary>
/// «Chi lo usa» (lotto «Subito» slice 7, «file per file» L2): dove un fix, un navaid, uno scalo, un punto VFR o
/// un'attesa sono citati per nome — aerovie, SID/STAR, confini, settori, MVA, rotte VFR, attese, tag. È la base della
/// rinomina (che aggiorna tutti e soli questi posti) e del «togli» impedito a un punto usato.
/// <para>🔴 Un nome va a un punto <b>per master</b>: Aurora lo cerca nei cataloghi di quel che il suo <c>.isc</c>
/// carica, e lo stesso nome in due FIR sono due punti (il validatore lo dice, <c>NomeDuplicato</c>). Una citazione è
/// di questo punto se, nei master che caricano il suo file, il nome si risolve nel file di questo punto.</para>
/// </summary>
public sealed class ChiLoUsa
{
    // Per nome (come Aurora, maiuscole indifferenti): i file e i record che lo citano.
    private readonly Dictionary<string, Dictionary<string, SortedSet<int>>> _perNome = new(StringComparer.OrdinalIgnoreCase);

    // Per nome: i punti che si chiamano così (catalogo e file) — i VOR e gli NDB omonimi, le copie di un punto.
    private readonly Dictionary<string, List<(string Catalogo, string File)>> _dichiarati = new(StringComparer.OrdinalIgnoreCase);

    // Per posizione (slice 7c, R-1): i file e i record che la citano — i trasferimenti dei .frq, le teste dei .tfl.
    private readonly Dictionary<string, Dictionary<string, SortedSet<int>>> _perPosizione = new(StringComparer.OrdinalIgnoreCase);

    // Per file: i nomi che ci sono, per rifare il file senza rifare l'albero.
    private readonly Dictionary<string, List<string>> _nomiDelFile = new(StringComparer.Ordinal);

    private ChiLoUsa()
    {
    }

    /// <summary>L'indice di tutta la sessione, dai record com'erano all'apertura.</summary>
    public static ChiLoUsa Di(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        var indice = new ChiLoUsa();
        foreach (var (relativo, file) in sessione.File)
            indice.Metti(relativo, file);
        return indice;
    }

    /// <summary>Un file è cambiato (una modifica, una rilettura): i suoi nomi si rifanno, gli altri restano.</summary>
    public void RifaiIlFile(FileAperto file)
    {
        ArgumentNullException.ThrowIfNull(file);
        Togli(file.Relativo);
        Metti(file.Relativo, file);
    }

    /// <summary>I file e i record che citano il nome, in ordine di file.</summary>
    public IReadOnlyList<(string File, int Record)> Citano(string nome)
        => _perNome.TryGetValue(nome.Trim(), out var perFile)
            ? [.. perFile.OrderBy(f => f.Key, StringComparer.Ordinal).SelectMany(f => f.Value.Select(r => (f.Key, r)))]
            : [];

    /// <summary>
    /// Chi usa il record <paramref name="record"/> del file: null se il record non è un punto che si cita per nome.
    /// </summary>
    /// <param name="cataloghi">I cataloghi di ogni master (<see cref="CatalogoDeiPunti.PerOgniIsc"/>).</param>
    /// <param name="sporchiDi">I record toccati di un file: le righe si leggono nel file com'è adesso.</param>
    public UsiDelPunto? Di(SessioneAperta sessione, IReadOnlyDictionary<string, CatalogoDeiPunti> cataloghi,
                           string file, int record, Func<string, IEnumerable<object>> sporchiDi)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(cataloghi);
        ArgumentNullException.ThrowIfNull(sporchiDi);
        if (sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord || record < 0 || record >= conRecord.RecordDelModello.Count)
            return null;

        object dichiarato = conRecord.RecordDelModello[record];
        var righe = new Dictionary<string, (IReadOnlyList<string> Righe, IReadOnlyList<(int Da, int Quante)> Posti)>(StringComparer.Ordinal);
        (IReadOnlyList<string> Righe, IReadOnlyList<(int Da, int Quante)> Posti) Testo(string relativo)
        {
            if (!righe.TryGetValue(relativo, out var testo))
            {
                var suo = (IFileConRecord)sessione.File[relativo];
                var sporchi = sporchiDi(relativo).ToList();
                testo = (suo.RigheDelFile(sporchi), suo.PostiDeiRecord(sporchi));
                righe[relativo] = testo;
            }

            return testo;
        }

        // Una pista (slice 7d, R-1): lo scalo e i due versi, citati nelle procedure, nelle mappe, nei tag, nei PAR.
        if (dichiarato is Runway)
            return Piste.Di(sessione, file, record, sporchiDi);

        // Una posizione (slice 7c, R-1) è un nome di rete: vale in tutto l'albero, non per master. La citano i
        // trasferimenti dei .frq (anche esclusa, «-LIRR_EW_CTR») e le teste dei settori dinamici (.tfl).
        if (dichiarato is AtcPosition posizione)
        {
            string codice = posizione.Code.Trim();
            if (codice.Length == 0)
                return null;
            var dellaPosizione = new List<Citazione>();
            foreach (var (relativo, k) in CitanoLaPosizione(codice))
            {
                var (testo, posti) = Testo(relativo);
                if (k >= posti.Count)
                    continue;
                bool settore = ((IFileConRecord)sessione.File[relativo]).RecordDelModello[k] is TflSector;
                foreach (int i in RigheCheNominano(testo, posti[k], codice, soloIlPrimoCampo: settore))
                {
                    string come = settore ? "settore dinamico"
                        : Parole(testo[i]).Any(p => p.StartsWith('-') && string.Equals(p[1..], codice, StringComparison.OrdinalIgnoreCase))
                            ? "trasferimento escluso"
                            : "trasferimento";
                    dellaPosizione.Add(new Citazione(relativo, k, i + 1, testo[i], come, []));
                }
            }

            return new UsiDelPunto("posizione", [codice], Ordina(dellaPosizione), []);
        }

        // Un'attesa non sta nei cataloghi dei master: la citano per nome i fix e i VOR (HLD-ABBOZ), in tutto l'albero.
        if (dichiarato is Attesa attesa)
        {
            string nome = attesa.Nome.Trim();
            if (nome.Length == 0)
                return null;
            var dellAttesa = new List<Citazione>();
            foreach (var (relativo, altro) in sessione.File.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                if (altro is not IFileConRecord navaid || !EUnNavaid(relativo))
                    continue;
                var (testo, posti) = Testo(relativo);
                for (int k = 0; k < posti.Count; k++)
                {
                    foreach (int i in RigheCheCitano(testo, posti[k], nome, attesa: false))
                        dellAttesa.Add(new Citazione(relativo, k, i + 1, testo[i], "rimanda all'attesa", []));
                }
            }

            return new UsiDelPunto("attesa", [nome], dellAttesa, []);
        }

        if (Cataloghi.Dichiarato(dichiarato) is not { } dichiarazione)
            return null;
        var nomi = dichiarazione.Nomi.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (nomi.Count == 0)
            return null;

        // I master che caricano ogni file: una citazione è di questo punto dove il nome si risolve nel suo file.
        var masterDi = new Dictionary<string, List<CatalogoDeiPunti>>(StringComparer.Ordinal);
        foreach (var catalogo in cataloghi.Values)
        {
            foreach (string caricato in catalogo.FileCaricati)
            {
                if (!masterDi.TryGetValue(caricato, out var suoi))
                    masterDi[caricato] = suoi = [];
                suoi.Add(catalogo);
            }
        }

        var sue = new List<Citazione>();
        var altrui = new List<Citazione>();
        foreach (string nome in nomi)
        {
            foreach (var (relativo, k) in Citano(nome))
            {
                // Un file che nessun master carica non risolve niente (il validatore lo dice, FileMaiCitato).
                if (!masterDi.TryGetValue(relativo, out var master) || sessione.File.GetValueOrDefault(relativo) is not IFileConRecord)
                    continue;
                // Un VOR e un NDB con lo stesso nome: la riga può voler dire l'uno o l'altro, e vale per tutti e due.
                // Solo quelli che un master carica: un NDB in un file che nessuno carica non è nel sector.
                var gemelli = Gemelli(nome, dichiarazione.Catalogo).Where(g => masterDi.ContainsKey(g.File)).ToList();
                string? ancheA = gemelli.Count == 0 ? null : string.Join("; ", gemelli.Select(g => $"{g.Catalogo.ToUpperInvariant()} {nome}, {Breve(g.File)}"));
                var qui = master.Where(c => EQuesto(c.Cerca(nome), file, dichiarazione)
                                            || c.Cerca(nome) is { } p && gemelli.Contains((p.Catalogo, p.File)))
                    .Select(c => c.Isc).Order(StringComparer.Ordinal).ToList();
                string? vaA = qui.Count > 0 ? null
                    : master.Select(c => c.Cerca(nome)).FirstOrDefault(p => p is not null) is { } altro
                        ? $"{altro.Catalogo}, {Breve(altro.File)}"
                        : "nessun punto";
                var (testo, posti) = Testo(relativo);
                if (k >= posti.Count)
                    continue;
                object citante = ((IFileConRecord)sessione.File[relativo]).RecordDelModello[k];
                bool dallAttesa = citante is Attesa;
                var dove = qui.Count > 0 ? sue : altrui;
                IReadOnlyList<string> soloIn = qui.Count > 0 && qui.Count < master.Count ? qui : [];
                foreach (int i in RigheCheCitano(testo, posti[k], nome, dallAttesa))
                    dove.Add(new Citazione(relativo, k, i + 1, testo[i], dallAttesa ? "attesa" : "punto", soloIn) { VaA = vaA, AncheA = ancheA });

                foreach (var (chiave, riga) in NeiTag(testo, posti[k], (IFileConRecord)sessione.File[relativo], k, nome))
                    dove.Add(new Citazione(relativo, k, riga + 1, testo[riga], chiave + "=", soloIn) { VaA = vaA, AncheA = ancheA });
            }
        }

        return new UsiDelPunto(dichiarazione.Catalogo, nomi, Ordina(sue), Ordina(altrui));
    }

    /// <summary>
    /// Vero se il nome, in quel master, va a questo punto: al suo file, o a una sua copia — lo stesso catalogo a meno di
    /// un decimo di miglio (lo scalo in <c>itap.ap</c> e in <c>limm.ap</c>, carta F3-bis §2.1; il fix scritto due volte,
    /// che il validatore chiama <c>NomeRipetuto</c> e non <c>NomeDuplicato</c>).
    /// </summary>
    private static bool EQuesto(PuntoDelCatalogo? trovato, string file, NomeDiCatalogo dichiarazione)
    {
        if (trovato is not { } punto)
            return false;
        if (string.Equals(punto.File, file, StringComparison.Ordinal))
            return true;
        return punto.Catalogo == dichiarazione.Catalogo && Vicini(punto.Posizione, dichiarazione.Posizione);
    }

    // Meno di un decimo di miglio: lo stesso punto scritto due volte (il validatore: NomeRipetuto, non NomeDuplicato).
    private static bool Vicini(Vipi.Sectorfile.Shared.Coordinate a, Vipi.Sectorfile.Shared.Coordinate b)
    {
        double dy = (a.LatitudeDeg - b.LatitudeDeg) * 111_320;
        double dx = (a.LongitudeDeg - b.LongitudeDeg) * 111_320 * Math.Cos(a.LatitudeDeg * Math.PI / 180);
        return Math.Sqrt((dx * dx) + (dy * dy)) < 185.2;
    }

    /// <summary>I file e i record che citano la posizione (trasferimenti, teste dei .tfl), in ordine di file.</summary>
    public IReadOnlyList<(string File, int Record)> CitanoLaPosizione(string codice)
        => _perPosizione.TryGetValue(codice.Trim(), out var perFile)
            ? [.. perFile.OrderBy(f => f.Key, StringComparer.Ordinal).SelectMany(f => f.Value.Select(r => (f.Key, r)))]
            : [];

    /// <summary>Vero se nell'albero c'è già una posizione (o un punto del catalogo) con quel nome.</summary>
    public bool CEGia(string nome, string catalogo)
        => _dichiarati.TryGetValue(nome.Trim(), out var dove) && dove.Any(d => d.Catalogo == catalogo);

    /// <summary>Le parole di una riga, campo per campo: i trasferimenti di un .frq, le posizioni della testa di un .tfl.</summary>
    internal static IEnumerable<string> Parole(string riga)
        => riga.Split(';').SelectMany(c => c.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Le righe (da 0) del record dove una PAROLA è il nome (con o senza il «-» dell'escluso): i trasferimenti sono parole
    /// separate da spazi dentro un campo. Nella testa di un .tfl conta solo il primo campo.
    /// </summary>
    internal static IEnumerable<int> RigheCheNominano(IReadOnlyList<string> righe, (int Da, int Quante) posto, string nome, bool soloIlPrimoCampo)
    {
        for (int i = posto.Da; i < posto.Da + posto.Quante && i < righe.Count; i++)
        {
            if (righe[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                continue;
            // Nella testa di un .tfl le posizioni si separano anche coi due punti (slice 13a: GCI.tfl, limmctr.tfl).
            var parole = soloIlPrimoCampo
                ? righe[i].Split(';')[0].Split([' ', ':'], StringSplitOptions.RemoveEmptyEntries)
                : Parole(righe[i]);
            if (parole.Any(p => string.Equals(p.TrimStart('-'), nome, StringComparison.OrdinalIgnoreCase)))
            {
                yield return i;
                if (soloIlPrimoCampo)
                    yield break;
            }
        }
    }

    /// <summary>
    /// Le copie del punto (lotto «Subito» slice 7b): gli altri record dello stesso catalogo con lo stesso nome a meno di
    /// un decimo di miglio — lo scalo in <c>itap.ap</c> e in <c>limm.ap</c>, il fix scritto in due file. Una rinomina le
    /// porta con sé: le citazioni che Aurora risolve in una copia sono anche sue.
    /// </summary>
    public IReadOnlyList<(string File, int Record)> Copie(SessioneAperta sessione, string file, int record, string nome)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        if (sessione.File.GetValueOrDefault(file) is not IFileConRecord conRecord || record < 0 || record >= conRecord.RecordDelModello.Count
            || Dichiarazione(conRecord.RecordDelModello[record]) is not { } questo
            || !_dichiarati.TryGetValue(nome.Trim(), out var dove))
            return [];

        var copie = new List<(string, int)>();
        foreach (string altro in dove.Where(d => d.Catalogo == questo.Catalogo).Select(d => d.File).Distinct().Order(StringComparer.Ordinal))
        {
            if (sessione.File.GetValueOrDefault(altro) is not IFileConRecord suo)
                continue;
            for (int k = 0; k < suo.RecordDelModello.Count; k++)
            {
                // Una posizione è la stessa in ogni .frq che la dichiara (itfreq.frq e quello della FIR): non ha un punto.
                if ((altro, k) != (file, record)
                    && Dichiarazione(suo.RecordDelModello[k]) is { } punto && punto.Catalogo == questo.Catalogo
                    && punto.Nomi.Any(n => string.Equals(n.Trim(), nome.Trim(), StringComparison.OrdinalIgnoreCase))
                    && (questo.Catalogo == "posizione" || Vicini(punto.Posizione, questo.Posizione)))
                    copie.Add((altro, k));
            }
        }

        return copie;
    }

    private static string Breve(string relativo)
        => relativo.IndexOf("/IT/", StringComparison.Ordinal) is var i and >= 0 ? relativo[(i + 4)..] : relativo;

    /// <summary>I navaid dell'altro tipo (VOR per un NDB, NDB per un VOR) con lo stesso nome: catalogo e file.</summary>
    private IReadOnlyList<(string Catalogo, string File)> Gemelli(string nome, string catalogo)
        => catalogo is "vor" or "ndb" && _dichiarati.TryGetValue(nome, out var tutti)
            ? [.. tutti.Where(n => n.Catalogo is "vor" or "ndb" && n.Catalogo != catalogo).Distinct()]
            : [];

    // Le chiavi dei tag che nominano un punto (§M, P6): il fix intero e la transizione di una SID o di una voce .str.
    private static readonly string[] ChiaviConUnPunto = ["fix", "trans"];

    private static IReadOnlyList<Citazione> Ordina(List<Citazione> citazioni)
        => [.. citazioni.DistinctBy(c => (c.File, c.Riga)).OrderBy(c => c.File, StringComparer.Ordinal).ThenBy(c => c.Riga)];

    private static bool EUnNavaid(string relativo)
        => relativo.EndsWith(".fix", StringComparison.OrdinalIgnoreCase) || relativo.EndsWith(".vor", StringComparison.OrdinalIgnoreCase)
           || relativo.EndsWith(".ndb", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Le righe (da 0) del record che hanno un campo uguale al nome: attive, non commentate (Aurora non le legge). Per
    /// un'attesa, il fix della descrizione (<c>ABBOZ/225R-9000</c>).
    /// </summary>
    internal static IEnumerable<int> RigheCheCitano(IReadOnlyList<string> righe, (int Da, int Quante) posto, string nome, bool attesa)
    {
        for (int i = posto.Da; i < posto.Da + posto.Quante && i < righe.Count; i++)
        {
            if (righe[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                continue;
            foreach (string campo in righe[i].Split(';'))
            {
                string pulito = campo.Trim();
                if (string.Equals(pulito, nome, StringComparison.OrdinalIgnoreCase)
                    || (attesa && pulito.StartsWith(nome + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    yield return i;
                    break;
                }
            }
        }
    }

    /// <summary>I tag del record con un punto per valore: la chiave e la riga (da 0) della dichiarazione sopra il record.</summary>
    private static IEnumerable<(string Chiave, int Riga)> NeiTag(IReadOnlyList<string> righe, (int Da, int Quante) posto,
                                                                  IFileConRecord file, int record, string nome)
    {
        if (file.ChiaviDi(record) is not { } chiavi)
            yield break;
        foreach (string chiave in ChiaviConUnPunto)
        {
            if (!chiavi.TryGetValue(chiave, out string? valore) || !string.Equals(Metadati.Testo(valore), nome, StringComparison.OrdinalIgnoreCase))
                continue;
            // La dichiarazione sta sopra il record, fra i suoi commenti: la prima riga //@ che ha la chiave.
            int riga = posto.Da;
            for (int i = posto.Da - 1; i >= 0 && righe[i].TrimStart().StartsWith("//", StringComparison.Ordinal); i--)
            {
                if (righe[i].TrimStart().StartsWith("//@", StringComparison.Ordinal) && righe[i].Contains(chiave + "=", StringComparison.Ordinal))
                {
                    riga = i;
                    break;
                }
            }

            yield return (chiave, riga);
        }
    }

    private void Metti(string relativo, FileAperto file)
    {
        if (file is not IFileConRecord conRecord)
            return;
        var nomi = new List<string>();
        var chiavi = conRecord.CatalogoDeiTag is null ? null : conRecord.ChiaviDeiRecord();
        for (int k = 0; k < conRecord.RecordDelModello.Count; k++)
        {
            if (Dichiarazione(conRecord.RecordDelModello[k]) is { } punto)
            {
                foreach (string suo in punto.Nomi.Select(n => n.Trim()).Where(n => n.Length > 0))
                {
                    if (!_dichiarati.TryGetValue(suo, out var omonimi))
                        _dichiarati[suo] = omonimi = [];
                    if (!omonimi.Contains((punto.Catalogo, relativo)))
                        omonimi.Add((punto.Catalogo, relativo));
                }
            }

            foreach (string posizione in PosizioniCitate(conRecord.RecordDelModello[k]))
            {
                if (!_perPosizione.TryGetValue(posizione, out var perFile))
                    _perPosizione[posizione] = perFile = new(StringComparer.Ordinal);
                if (!perFile.TryGetValue(relativo, out var suoi))
                    perFile[relativo] = suoi = [];
                suoi.Add(k);
            }

            foreach (string nome in NomiCitati(conRecord.RecordDelModello[k], chiavi?[k]))
            {
                if (!_perNome.TryGetValue(nome, out var perFile))
                    _perNome[nome] = perFile = new(StringComparer.Ordinal);
                if (!perFile.TryGetValue(relativo, out var record))
                    perFile[relativo] = record = [];
                if (record.Add(k) && record.Count == 1)
                    nomi.Add(nome);
            }
        }

        _nomiDelFile[relativo] = nomi;
    }

    private void Togli(string relativo)
    {
        foreach (var perFile in _perPosizione.Values)
            perFile.Remove(relativo);
        foreach (var omonimi in _dichiarati.Values)
            omonimi.RemoveAll(n => n.File == relativo);

        if (!_nomiDelFile.Remove(relativo, out var nomi))
            return;
        foreach (string nome in nomi)
        {
            if (_perNome.TryGetValue(nome, out var perFile) && perFile.Remove(relativo) && perFile.Count == 0)
                _perNome.Remove(nome);
        }
    }

    /// <summary>Il nome col quale un record si dichiara: un punto dei cataloghi, o una posizione di un .frq (slice 7c).</summary>
    internal static NomeDiCatalogo? Dichiarazione(object record)
        => record is AtcPosition posizione && posizione.Code.Trim().Length > 0
            ? new NomeDiCatalogo("posizione", default, [posizione.Code.Trim()])
            : Cataloghi.Dichiarato(record);

    // Le posizioni che un record cita: i trasferimenti di un .frq, le posizioni della testa di un settore dinamico.
    private static IEnumerable<string> PosizioniCitate(object record) => record switch
    {
        AtcPosition posizione => posizione.TransferList.Select(t => t.PositionCode.Trim()).Where(c => c.Length > 0),
        TflSector settore => settore.Posizioni(),
        _ => [],
    };

    // I nomi che un record cita: quelli che il motore risolve, il fix di un'attesa, i valori dei tag fix= e trans=.
    private static IEnumerable<string> NomiCitati(object oggetto, IReadOnlyDictionary<string, string>? chiavi)
    {
        var nomi = new HashSet<string>(Cataloghi.Usati(oggetto), StringComparer.OrdinalIgnoreCase);
        if (oggetto is Attesa { Fix: { } fix } && fix.Trim().Length > 0)
            nomi.Add(fix.Trim());
        if (chiavi is not null)
        {
            foreach (string chiave in ChiaviConUnPunto)
            {
                if (chiavi.TryGetValue(chiave, out string? valore) && Metadati.Testo(valore).Trim() is { Length: > 0 } punto)
                    nomi.Add(punto);
            }
        }

        return nomi;
    }
}
