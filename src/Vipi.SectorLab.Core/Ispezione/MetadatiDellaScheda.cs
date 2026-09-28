using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Una chiave dei metadati di §M come la mostra la scheda: il nome dell'AOD, il valore, se si scrive.</summary>
/// <param name="Chiave">La chiave del tag (<c>initialclimb</c>, <c>06.tora</c>): è quella che si scrive nel file.</param>
/// <param name="Valore">Il valore come si legge (senza virgolette), o null se il record non ce l'ha.</param>
/// <param name="SiNo">La chiave vale <c>si</c> o manca (<c>locked</c>, <c>whole</c>, <c>vfronly</c>): una casella.</param>
/// <param name="PercheNo">Perché non si scrive dalla scheda, o null se si scrive.</param>
/// <param name="Editor">Come si scrive (prova 68: fix e transizione dai punti del master, salita iniziale in ft/FL o COO
/// APP, categorie come tasti).</param>
public sealed record MetadatoDellaScheda(string Chiave, string Nome, string Significato, string? Valore, bool SiNo, string? PercheNo,
                                         EditorDelMetadato Editor = EditorDelMetadato.Testo)
{
    /// <summary>La chiave senza il numero di pista davanti (<c>tora</c> per <c>16L.tora</c>).</summary>
    public string Base => MetadatiDellaScheda.Senzaverso(Chiave);

    public bool SiScrive => PercheNo is null;
}

/// <summary>
/// I metadati di un record nella scheda (lotto «Subito», slice 3d): le chiavi del catalogo del suo tipo di file
/// (<see cref="CatalogoDeiTag"/>, il contratto con vIPI) appaiono da sole, col nome dell'AOD e il significato di §M;
/// quelle che il record ha già si vedono col loro valore, anche fuori catalogo. Le chiavi dei punti (<c>//@@</c>) sono
/// delle schede delle procedure e delle aerovie (slice 9, 14).
/// </summary>
public static class MetadatiDellaScheda
{
    /// <summary>Le chiavi che valgono <c>si</c> o mancano.</summary>
    private static readonly HashSet<string> ChiaviSiNo = new(StringComparer.Ordinal) { "locked", Metadati.Whole, "vfronly" };

    /// <summary>I metadati del record, nell'ordine del catalogo; vuoto se i record del file non portano tag.</summary>
    public static IReadOnlyList<MetadatoDellaScheda> Di(FileAperto file, int indice)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord || conRecord.CatalogoDeiTag is not { } catalogo
            || indice < 0 || indice >= conRecord.RecordDelModello.Count)
        {
            return [];
        }

        object record = conRecord.RecordDelModello[indice];
        var valori = conRecord.ChiaviDi(indice) ?? new Dictionary<string, string>();
        string? perTutte = conRecord.TagRotti() is { } rotto
            ? $"Il file ha tag //@ che non valgono ({rotto}): prima vanno sistemati."
            : conRecord.SiDichiara(indice)
                ? null
                : "Questo record non ha un nome suo: i suoi metadati si scrivono sul blocco che lo contiene (slice 6).";

        var chiavi = new List<string>(catalogo.DelRecord);
        // Le chiavi per verso di pista col numero davanti, per i due versi di QUESTA pista (§M regola 7).
        if (record is Runway pista)
        {
            foreach (string verso in new[] { pista.Designator1, pista.Designator2 }.Select(v => v.Trim()).Where(v => v.Length > 0))
                chiavi.AddRange(catalogo.PerVerso.Select(c => $"{verso}.{c}"));
        }

        chiavi.AddRange(valori.Keys.Where(k => !chiavi.Contains(k, StringComparer.Ordinal)));

        return [.. chiavi.Select(chiave =>
        {
            var (nome, significato) = Descrizione(chiave);
            string? percheNo = perTutte ?? PercheNoLaChiave(chiave, record, catalogo);
            bool siNo = ChiaviSiNo.Contains(Senzaverso(chiave));
            return new MetadatoDellaScheda(chiave, nome, significato,
                valori.TryGetValue(chiave, out string? scritto) ? Metadati.Testo(scritto) : null,
                siNo, percheNo, ValoriDeiMetadati.EditorDi(Senzaverso(chiave), siNo));
        })];
    }

    private static string? PercheNoLaChiave(string chiave, object record, CatalogoDeiTag catalogo)
    {
        if (!catalogo.AmmetteDelRecord(chiave))
            return $"Non è fra i metadati dei {catalogo.Formato}: si vede, ma non si scrive.";
        return chiave switch
        {
            "gen" => "La scrive il generatore che ha disegnato il record, coi suoi parametri (§M-G, F8).",
            Metadati.Compose or Metadati.Whole when record is StrRecord => "Si cambia da «Composta da», qui sotto.",
            Metadati.Compose or Metadati.Whole => "Le configurazioni composte arrivano con la saldatura dei bordi (J6, F8).",
            _ => null,
        };
    }

    /// <summary>La chiave senza il numero di pista davanti.</summary>
    public static string Senzaverso(string chiave) => chiave.Contains('.', StringComparison.Ordinal) && char.IsAsciiDigit(chiave[0])
        ? chiave[(chiave.IndexOf('.', StringComparison.Ordinal) + 1)..]
        : chiave;

    /// <summary>Nome e significato di una chiave, dal catalogo di §M (carta «file per file»).</summary>
    private static (string Nome, string Significato) Descrizione(string chiave)
    {
        string senza = Senzaverso(chiave);
        string verso = senza == chiave ? "" : " " + chiave[..chiave.IndexOf('.', StringComparison.Ordinal)];
        if (Nomi.TryGetValue(senza, out var descritta))
            return (descritta.Nome + verso, descritta.Significato);
        return (chiave, "Una chiave che il catalogo di §M non ha.");
    }

    private static readonly Dictionary<string, (string Nome, string Significato)> Nomi = new(StringComparer.Ordinal)
    {
        ["locked"] = ("Bloccato", "locked=si: l'import non lo tocca né lo toglie (l'AOD può sempre cancellarlo)."),
        ["gen"] = ("Generato da", "Il generatore e i suoi parametri (§M-G)."),
        ["note"] = ("Nota", "Per chi legge il file."),
        ["fix"] = ("Fix intero", "Il nome intero del fix (EKLOS per EKLO8R): un punto del sector."),
        ["trans"] = ("Transizione", "Il fix della transizione: un punto del sector."),
        ["initialclimb"] = ("Salita iniziale", "In piedi (6000ft) o in FL (FL80), oppure «COO APP» (coordinare con l'avvicinamento)."),
        ["wtc"] = ("Categorie di scia", "Le WTC ammesse: L M H S. Doppio clic: anche le precedenti come quella."),
        ["cat"] = ("Categorie Vref", "A B C D E. Doppio clic: anche le precedenti come quella."),
        ["nav"] = ("Specifica di navigazione", "Quale navigazione chiede la procedura: RNAV1, RNP1, RNP APCH (P11, Q2b; i valori arrivano con l'import dall'AIP, F7)."),
        ["type"] = ("Tipo di avvicinamento", "ILS, LOC, RNP, VOR, NDB."),
        ["mins"] = ("Minimi", "Per categoria, in piedi: A:450,B:450,C:500,D:500."),
        ["gp"] = ("Pendenza", "Del sentiero di discesa, in gradi (3.0)."),
        [Metadati.Compose] = ("Composta da", "L'elenco delle parti (mappe composte, configurazioni)."),
        [Metadati.Whole] = ("Parti intere", "whole=si: le parti si disegnano intere."),
        ["form"] = ("Famiglia di forme", "Lo stesso nome nelle copie della stessa forma (D5)."),
        ["lower"] = ("Limite inferiore", "La quota più bassa (FL o piedi)."),
        ["upper"] = ("Limite superiore", "La quota più alta (FL o piedi)."),
        ["class"] = ("Classe", "La classe dello spazio aereo, A-G."),
        ["zone"] = ("Soprannome della zona", "Anche ripetuto: due zone possono chiamarsi uguali (Torino)."),
        ["width"] = ("Larghezza", "Della pista, in metri."),
        ["length"] = ("Lunghezza", "Della pista, in metri."),
        ["vfronly"] = ("Solo VFR", "vfronly=si: la pista (o quel verso) è solo VFR."),
        ["thr"] = ("Soglia spostata", "In metri."),
        ["ils"] = ("ILS", "CAT1, CAT2, CAT3, no."),
        ["tora"] = ("TORA", "In metri; NU = non utilizzabile (AD 2.13)."),
        ["toda"] = ("TODA", "In metri; NU = non utilizzabile (AD 2.13)."),
        ["asda"] = ("ASDA", "In metri; NU = non utilizzabile (AD 2.13)."),
        ["lda"] = ("LDA", "In metri; NU = non utilizzabile (AD 2.13)."),
        ["int"] = ("Decolli dagli intermedi", "Taxiway:TORA in metri, nell'ordine dell'AD 2.13 (B:2540,C:1893)."),
        ["circuit"] = ("Circuito VFR", "Il lato: L o R (AD 2.20/2.22)."),
        ["dep"] = ("Decolli", "dep=no: niente decolli per quel verso."),
        ["arr"] = ("Atterraggi", "arr=no: niente atterraggi per quel verso."),
        ["magvar"] = ("Declinazione magnetica", "4E (AD 2.2)."),
        ["magvar.year"] = ("Anno della declinazione", "2025.0."),
        ["refcode"] = ("Codice di riferimento", "Annesso 14: 4D (AD 2.2)."),
        ["rff"] = ("Antincendio", "La categoria (AD 2.6)."),
        ["traffic"] = ("Traffico", "IFR/VFR o VFR (AD 2.2)."),
        ["pref"] = ("Pista preferenziale", "AD 2.20."),
        ["tailwind"] = ("Vento in coda massimo", "AD 2.20."),
        ["ats"] = ("Orario ATS", "L'orario della TWR: H24 (AD 2.3)."),
        ["code"] = ("Codice ICAO", "La dimensione massima, A-F."),
        ["kind"] = ("Tipo di stand", "A contatto o remoto."),
        ["use"] = ("Uso", "Schengen, extra-Schengen, cargo, aviazione generale, militare, elicotteri."),
        ["airlines"] = ("Compagnie", "Le compagnie abituali."),
        ["push"] = ("Pushback", "Obbligatorio o no."),
        ["pushdir"] = ("Verso del pushback", "Il verso."),
        ["apron"] = ("Piazzale", "Il piazzale dello stand."),
        ["oneway"] = ("Senso unico", "oneway=E: solo verso est."),
    };
}
