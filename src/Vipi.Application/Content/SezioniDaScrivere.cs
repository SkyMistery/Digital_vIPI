using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un blocco come serve a dire se c'è qualcosa: il testo, il JSON, il riuso.</summary>
public sealed record BloccoDaContare(string? Body, string? BodyJson, bool Riusato = false);

/// <summary>Una sezione di un documento come serve al conto: dov'è, com'è, e i suoi blocchi.</summary>
/// <param name="ParentId">La sezione madre, o null per una radice.</param>
/// <param name="Nascosta">Il flag della sezione stessa: chi conta guarda anche le madri.</param>
public sealed record SezioneDaContare(int Id, int? ParentId, string Chiave, string Titolo, bool Nascosta,
    IReadOnlyList<BloccoDaContare> Blocchi);

/// <summary>Una sezione da scrivere ancora vuota, col percorso che la fa trovare («Gestione del traffico › IFR»).</summary>
public sealed record SezioneVuota(int SectionId, string Chiave, string Percorso);

/// <summary>
/// Il conto di una parte di documento: il documento intero, o un blocco della vIPI di ACC.
/// </summary>
/// <param name="Titolo">Il titolo del blocco della vIPI di ACC; null per un documento che non ha blocchi.</param>
public sealed record ContoSezioni(string? Titolo, int Scritte, IReadOnlyList<SezioneVuota> Vuote)
{
    public int Totale => Scritte + Vuote.Count;
}

/// <summary>Il conto di un documento: il totale, e per la vIPI di ACC le parti (un conto per blocco).</summary>
public sealed record StatoSezioni(int DocumentId, IReadOnlyList<ContoSezioni> Parti)
{
    public int Scritte => Parti.Sum(p => p.Scritte);
    public int Vuote => Parti.Sum(p => p.Vuote.Count);
    public int Totale => Scritte + Vuote;
}

/// <summary>
/// Le sezioni da scrivere dei documenti, lette dalla versione di lavoro. Visibile a chi modifica: le pagine che la
/// chiamano lo sono già (editor e Versioni).
/// </summary>
public interface ISezioniDaScrivereService
{
    /// <summary>Tutti i documenti in una passata, per la pagina Versioni. Fuori i documenti senza profilo.</summary>
    Task<IReadOnlyDictionary<int, StatoSezioni>> QuadroAsync(CancellationToken ct = default);

    /// <summary>Un documento, per il suo editor. null se non c'è o non ha un profilo di catalogo.</summary>
    Task<StatoSezioni?> DocumentoAsync(int documentId, CancellationToken ct = default);
}

/// <summary>
/// Le <b>sezioni da scrivere</b> di un documento: quante hanno qualcosa e quante sono ancora vuote. Carta
/// <c>docs/feature/2026-10-18-sezioni-da-scrivere.md</c>.
///
/// <para>Chiesto al committente (18 ottobre 2026) «un indicatore di quante delle sezioni obbligatorie non siano mai
/// state toccate e quante invece ci sia almeno scritto o messo qualcosa». Deciso con lui: contano le sezioni
/// <b>editoriali</b> del catalogo, quelle che scrive una persona; le nascoste no.</para>
///
/// <para>Pura, senza I/O: la parte che si sbaglia senza che nulla protesti — una sezione contata vuota per sempre
/// è un indicatore che si smette di guardare.</para>
/// </summary>
public static class SezioniDaScrivere
{
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// I contenitori: titoli che raccolgono figlie, col contenuto sotto. Contati sarebbero vuoti per sempre;
    /// contano le figlie.
    /// </summary>
    private static readonly IReadOnlySet<string> Contenitori = new HashSet<string>(OIC)
    {
        "generaldata", "groundprocedures", "flightprocedures",
        SectionKeys.TrafficManagement, SectionKeys.Charts,
        SectionKeys.DepartureProcedures, SectionKeys.ArrivalProcedures,
    };

    /// <summary>
    /// Vero se la sezione con questa chiave, in questo profilo, è da scrivere: di catalogo, editoriale, e non un
    /// contenitore né una sezione che di editoriale ha solo il nome.
    /// </summary>
    public static bool DaScrivere(SectionProfile profilo, string chiave)
    {
        if (SectionCatalog.Find(profilo, chiave) is null) return false;              // libera: non è obbligatoria
        if (SectionCatalog.KindOf(chiave) != SectionKind.Editorial) return false;    // la riempiono i dati
        if (Contenitori.Contains(chiave)) return false;
        // Editoriale nel catalogo, ma la disegna la pagina con le soglie delle piste: nessuno ci scrive.
        if (OIC.Equals(chiave, SectionKeys.RunwayThresholds)) return false;
        // La tecnica della torre radar: blocchi veri dal 21 ottobre 2026, ma c'è solo con la torre dotata di radar
        // (`TorreRadar.Filtra`), e il conto non sa il sì/no, che sta nell'anagrafica. Contarla chiederebbe di
        // scrivere, sugli scali senza radar, una sezione che il documento non mostra.
        if (OIC.Equals(chiave, SectionKeys.RadarTowerTechnique)) return false;
        // I due versi dei coordinamenti della vLOA: li rende la pagina dagli accordi. Nel catalogo non hanno una
        // natura scritta e passano per editoriali — trovato sulla copia di produzione del 12 ottobre 2026, dove le
        // quattro vLOA risultavano con due sezioni vuote ciascuna. ⚠️ Non si dichiarano derivate nel catalogo: lì
        // la natura accende anche l'interruttore Live/Frozen dell'editor, che per loro nessuno esegue.
        if (OIC.Equals(chiave, SectionKeys.CoordinationOut) || OIC.Equals(chiave, SectionKeys.CoordinationIn)) return false;
        // Nel blocco Aerovia le aree nascono in automatico (`RegulatedSelection.OwnAuto`): tutte quelle dell'ACC,
        // e seguono gli import. È una derivata di fatto.
        if (profilo == SectionProfile.AccAerovia && OIC.Equals(chiave, "regulated")) return false;
        return true;
    }

    /// <summary>
    /// Conta le sezioni da scrivere fra queste: tutte quelle date, a ogni profondità, di un profilo solo.
    /// Le nascoste — o sotto una madre nascosta — non contano.
    /// </summary>
    /// <param name="sezioni">Le sezioni della parte di documento; le madri fuori da qui si ignorano.</param>
    public static ContoSezioni Conta(SectionProfile profilo, IReadOnlyList<SezioneDaContare> sezioni, string? titolo = null)
    {
        var perId = sezioni.ToDictionary(s => s.Id);
        var figlie = sezioni.Where(s => s.ParentId is int m && perId.ContainsKey(m))
            .ToLookup(s => s.ParentId!.Value);
        var piene = new Dictionary<int, bool>();
        var scritte = 0;
        var vuote = new List<SezioneVuota>();
        foreach (var s in sezioni)
        {
            if (!DaScrivere(profilo, s.Chiave) || NascostaConLeMadri(s, perId)) continue;
            if (Piena(s, figlie, piene, 0)) scritte++;
            else vuote.Add(new SezioneVuota(s.Id, s.Chiave, Percorso(s, perId)));
        }
        return new ContoSezioni(titolo, scritte, vuote);
    }

    /// <summary>
    /// Vero se la sezione ha qualcosa: nei suoi blocchi, o in una sottosezione visibile a qualunque profondità — di
    /// catalogo o libera. 🔴 Il committente, 18 ottobre 2026: una sezione senza testo suo ma con le sottosezioni
    /// scritte «non sarebbe corretto» dirla vuota. Le nascoste sotto non contano: non escono nel documento.
    /// </summary>
    private static bool Piena(SezioneDaContare s, ILookup<int, SezioneDaContare> figlie, Dictionary<int, bool> piene, int profondita)
    {
        if (piene.TryGetValue(s.Id, out var gia)) return gia;
        var esito = s.Blocchi.Any(HaContenuto)
                    || (profondita < 64 && figlie[s.Id].Any(f => !f.Nascosta && Piena(f, figlie, piene, profondita + 1)));
        piene[s.Id] = esito;
        return esito;
    }

    /// <summary>Vero se il blocco porta qualcosa scritto o messo da una persona.</summary>
    public static bool HaContenuto(BloccoDaContare b) =>
        b.Riusato || !string.IsNullOrWhiteSpace(b.Body) || JsonHaContenuto(b.BodyJson);

    /// <summary>
    /// Vero se il JSON di un blocco ha contenuto: un'immagine, un allegato, un titolo di callout, o almeno un valore
    /// dentro una lista — le righe di una tabella, di una scheda, le aree scelte.
    ///
    /// <para>⚠️ Che il JSON ci sia non basta. Le sezioni rese dalla pagina nascono con un blocco segnaposto, una
    /// tabella scritta a mano può avere le intestazioni (<c>columns</c>) e nessuna riga, una scheda porta la sua
    /// <c>variant</c> anche vuota, e i numeri o gli interruttori in cima (<c>OwnAuto</c>) sono impostazioni, non
    /// contenuto.</para>
    /// </summary>
    public static bool JsonHaContenuto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var radice = doc.RootElement;
            switch (radice.ValueKind)
            {
                case JsonValueKind.Array:
                    return radice.EnumerateArray().Any(e => Pieno(e, inLista: true));
                case JsonValueKind.Object:
                    foreach (var p in radice.EnumerateObject())
                    {
                        // Immagine, allegato, titolo di una callout: le forme di SectionPayload.EEditoriale.
                        if (p.Name is "mediaId" or "ref" or "title")
                        {
                            if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                                return true;
                            continue;
                        }
                        if (p.Name is "columns" or "variant") continue;
                        if (p.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object && Pieno(p.Value, inLista: false))
                            return true;
                        if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                            return true;
                    }
                    return false;
                case JsonValueKind.String:
                    return !string.IsNullOrWhiteSpace(radice.GetString());
                default:
                    return false;
            }
        }
        catch (JsonException)
        {
            // Illeggibile ma scritto: qualcuno ci ha messo qualcosa, e dirla vuota la farebbe riscrivere.
            return true;
        }
    }

    /// <summary>
    /// Vero se il valore porta qualcosa. Un testo non bianco sempre; un numero solo dentro una lista (una riga che
    /// dice «5 NM» è contenuto, un <c>Order: 0</c> in cima no); vero/falso mai.
    /// </summary>
    private static bool Pieno(JsonElement e, bool inLista) => e.ValueKind switch
    {
        JsonValueKind.String => !string.IsNullOrWhiteSpace(e.GetString()),
        JsonValueKind.Number => inLista,
        JsonValueKind.Array => e.EnumerateArray().Any(x => Pieno(x, inLista: true)),
        JsonValueKind.Object => e.EnumerateObject().Any(p => p.Name != "variant" && Pieno(p.Value, inLista)),
        _ => false,
    };

    private static bool NascostaConLeMadri(SezioneDaContare s, IReadOnlyDictionary<int, SezioneDaContare> perId)
    {
        for (var (c, passi) = (s, 0); passi < 64; passi++)
        {
            if (c.Nascosta) return true;
            if (c.ParentId is not int p || !perId.TryGetValue(p, out var madre)) return false;
            c = madre;
        }
        return false;
    }

    private static string Percorso(SezioneDaContare s, IReadOnlyDictionary<int, SezioneDaContare> perId)
    {
        var titoli = new List<string> { s.Titolo };
        for (var (c, passi) = (s, 0); passi < 64 && c.ParentId is int p && perId.TryGetValue(p, out var madre); passi++)
        {
            titoli.Add(madre.Titolo);
            c = madre;
        }
        titoli.Reverse();
        return string.Join(" › ", titoli);
    }
}
