using System.Collections;
using System.Reflection;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Un campo del record come si legge a schermo.</summary>
/// <param name="Nome">Il nome del campo, com'è nel modello del motore: è la chiave (modifiche, <c>data-</c>).</param>
/// <param name="Valore">Il valore già scritto per una persona: le coordinate in DMS puntato, i vuoti come «—».</param>
/// <param name="Descrizione">Nome italiano, significato ed editor (slice 3); null = campo sconosciuto.</param>
public sealed record CampoDelRecord(string Nome, string Valore, DescrizioneDelCampo? Descrizione = null)
{
    /// <summary>
    /// Il valore come lo riceve l'editor e come la modifica lo rilegge: come <see cref="Valore"/>, ma un campo vuoto è
    /// vuoto e non «—» (slice 3b: in un elenco a tipo fisso il vuoto è una scelta, «non scritto»).
    /// </summary>
    public string Scritto { get; init; } = Valore;

    /// <summary>Il nome che legge l'AOD: quello della descrizione, o quello del modello per un campo sconosciuto.</summary>
    public string NomeDaMostrare => Descrizione?.Nome ?? Nome;

    public bool Sconosciuto => Descrizione is null;
}

/// <summary>Una riga del file com'è sul disco, col suo numero vero.</summary>
/// <param name="Numero">Il numero di riga nel file, da 1: è quello che si cita a un AOD.</param>
/// <param name="DelRecord">Vero per le righe del record; le altre sono il contesto intorno.</param>
public sealed record RigaGrezza(int Numero, string Testo, bool DelRecord);

/// <summary>Un record intero, come lo mostra l'ispettore (carta F3 §2.2 passo 3).</summary>
/// <param name="Tipo">Il tipo del modello del motore (<c>Fix</c>, <c>SidProcedure</c>).</param>
/// <param name="NomeDelTipo">Il tipo come lo dice l'AOD («Fix», «SID», «Traccia (T)»); il nome del modello se non è descritto.</param>
public sealed record SchedaDelRecord(
    string File,
    int Indice,
    string Tipo,
    string Etichetta,
    IReadOnlyList<CampoDelRecord> Campi,
    IReadOnlyList<RigaGrezza> Righe,
    FormaDellaMappa? Forma,
    string NomeDelTipo = "");

/// <summary>
/// L'ispettore in lettura (slice 5): i campi di un record e le righe da cui è stato letto.
/// <para>I campi si leggono per <b>riflessione</b> sul modello del motore, non con una tabella per tipo: i tipi di
/// record sono 25 e le loro proprietà cambiano con le slice di F2 — una tabella scritta a mano sarebbe una seconda
/// verità, e mostrerebbe campi vecchi senza dirlo. Quel che serve è la traduzione dei <b>valori</b> (coordinate,
/// punti per nome, elenchi), e quella sta qui.</para>
/// <para>Le righe grezze vengono dai <c>Chunks</c> del motore, contati dall'inizio del file: il numero di riga è
/// quello vero del disco, quello che si cita a un AOD e che il validatore usa nei suoi messaggi.</para>
/// </summary>
public static class Ispettore
{
    /// <summary>Quante righe di contesto sopra e sotto il record: tre, come in un diff.</summary>
    internal const int RigheDiContesto = 3;

    /// <summary>La scheda di un record, o null se il file non è interpretato o l'indice non c'è.</summary>
    public static SchedaDelRecord? Scheda(FileAperto file, int indice, CatalogoDeiPunti? catalogo)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord || indice < 0 || indice >= conRecord.RecordDelModello.Count)
            return null;

        object record = conRecord.RecordDelModello[indice];
        var forma = Geometria.DelFile(file, catalogo).FirstOrDefault(f => f.Record == indice);
        var descrizione = DescrizioniDeiCampi.Di(record, file.Relativo);

        return new SchedaDelRecord(
            file.Relativo,
            indice,
            record.GetType().Name,
            forma?.Etichetta is { Length: > 0 } etichetta ? etichetta : Etichetta(record),
            Campi(record, descrizione),
            Righe(file, indice),
            forma,
            descrizione?.Nome ?? record.GetType().Name);
    }

    /// <summary>
    /// Come si chiama un record nell'elenco di un file. Dove la mappa ha già un'etichetta si usa quella (stessa
    /// parola a schermo in due posti); qui si risponde per i record che sulla mappa non ci stanno (frequenze, ATIS).
    /// </summary>
    public static string Etichetta(object record)
    {
        ArgumentNullException.ThrowIfNull(record);
        foreach (string nome in NomiCheFannoDaEtichetta)
        {
            if (record.GetType().GetProperty(nome, BindingFlags.Public | BindingFlags.Instance)?.GetValue(record) is { } valore
                && Testo(valore) is { Length: > 0 } scritto and not "—")
            {
                return scritto;
            }
        }

        return record.GetType().Name;
    }

    /// <summary>Le etichette di tutti i record di un file, per l'elenco: dalla mappa dove c'è, per nome dove no.</summary>
    public static IReadOnlyList<string> Etichette(FileAperto file, CatalogoDeiPunti? catalogo)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord)
            return [];

        var record = conRecord.RecordDelModello;
        var dallaMappa = Geometria.DelFile(file, catalogo).ToDictionary(f => f.Record, f => f.Etichetta);

        var etichette = new string[record.Count];
        for (int i = 0; i < record.Count; i++)
        {
            etichette[i] = dallaMappa.TryGetValue(i, out string? dalla) && dalla.Length > 0
                ? dalla
                : Etichetta(record[i]);
        }

        return etichette;
    }

    /// <summary>
    /// I nomi di proprietà che, nell'ordine, fanno da etichetta: sono quelli veri del modello del motore.
    /// 🔴 <c>Code</c> e <c>Color</c> ci sono perché la misura sull'albero vero ha mostrato che senza di loro gli
    /// elenchi dicevano «AtcPosition» 201 volte (i <c>.frq</c>, che hanno <c>Code</c> = <c>LIRR_NE_CTR</c>) e
    /// «Line» 13 560 volte (i segmenti dei <c>.geo</c>, dove il nome non c'è e resta il colore: <c>COAST</c>).
    /// <para>Lotto «Subito», slice 3e (visto a schermo): le etichette L degli .artcc si chiamavano tutte «LabelPoint» —
    /// il loro nome sta in <c>FixRef</c> (o <c>CustomName</c>).</para>
    /// </summary>
    private static readonly string[] NomiCheFannoDaEtichetta =
        ["Name", "Nome", "Ident", "IcaoCode", "Designator", "Callsign", "Code", "Number", "Identifier", "FixRef", "CustomName", "Color"];

    /// <summary>
    /// I campi nell'ordine della descrizione (quello della riga del file), poi quelli che la descrizione non conosce:
    /// si vedono lo stesso, per riflessione, e la scheda li dice «campo sconosciuto» (slice 3).
    /// </summary>
    internal static IReadOnlyList<CampoDelRecord> Campi(object record, DescrizioneDelTipo? descrizione)
    {
        var letti = new List<(string Nome, string Valore, string Scritto)>();
        foreach (var proprieta in record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (proprieta.GetIndexParameters().Length > 0 || DescrizioniDeiCampi.Nascoste.Contains(proprieta.Name))
                continue;

            object? valore;
            try
            {
                valore = proprieta.GetValue(record);
            }
            catch (TargetInvocationException)
            {
                // Una proprietà calcolata che inciampa non deve portarsi via la scheda intera.
                continue;
            }

            letti.Add((proprieta.Name, Testo(valore), valore switch
            {
                null or "" => "",
                // Il punto si riscrive come un vertice: «ABBOZ», o le coordinate separate da uno spazio (slice 3c).
                Punto p => Modifiche.ElencoDiVertici.ScriviIlPunto(p),
                _ => Testo(valore),
            }));
        }

        var campi = new List<CampoDelRecord>();
        foreach (var campo in descrizione?.Campi ?? [])
        {
            if (letti.FindIndex(l => l.Nome == campo.Proprieta) is var i and >= 0)
                campi.Add(new CampoDelRecord(campo.Proprieta, letti[i].Valore, campo) { Scritto = letti[i].Scritto });
        }

        var descritti = campi.Select(c => c.Nome).ToHashSet(StringComparer.Ordinal);
        campi.AddRange(letti.Where(l => !descritti.Contains(l.Nome)).Select(l => new CampoDelRecord(l.Nome, l.Valore) { Scritto = l.Scritto }));
        return campi;
    }

    /// <summary>Un valore del modello come si legge: le coordinate in DMS puntato, gli elenchi contati.</summary>
    private static string Testo(object? valore) => valore switch
    {
        null => "—",
        Coordinate c => $"{CoordinateConverter.LatitudeToDottedDms(c.LatitudeDeg)} {CoordinateConverter.LongitudeToDottedDms(c.LongitudeDeg)}",
        Punto p => p.PerNome
            ? p.Nome == p.NomeLongitudine ? p.Nome! : $"{p.Nome} / {p.NomeLongitudine}"
            : Testo(p.Posizione!.Value),
        bool b => b ? "sì" : "no",
        string s => s.Length == 0 ? "—" : s,
        IEnumerable elenco and not string => Elenco(elenco),
        _ => valore.ToString() ?? "—",
    };

    private static string Elenco(IEnumerable elenco)
    {
        var pezzi = new List<string>();
        int quanti = 0;
        foreach (object? voce in elenco)
        {
            quanti++;
            if (pezzi.Count < 4)
                pezzi.Add(Testo(voce));
        }

        if (quanti == 0)
            return "—";
        // Un elenco lungo (i vertici di un'area) si dice quant'è e si mostra dalla mappa, non riga per riga qui.
        return quanti > pezzi.Count ? $"{quanti} voci: {string.Join(", ", pezzi)}…" : string.Join(", ", pezzi);
    }

    /// <summary>Le righe del record col loro numero vero nel file, più tre righe di contesto sopra e sotto.</summary>
    private static IReadOnlyList<RigaGrezza> Righe(FileAperto file, int indice)
        => file is IFileConRecord conRecord ? conRecord.RigheDelRecord(indice, RigheDiContesto) : [];
}
