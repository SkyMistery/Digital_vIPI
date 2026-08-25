using System.Text.Json;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Ui;

/// <summary>
/// Traduce una riga di audit in una frase leggibile — <b>un solo</b> formattatore per tutte le pagine che
/// mostrano eventi.
///
/// <para><b>Perché condiviso.</b> Fino al 22 agosto 2026 il pannello «storia» di <c>/services/vsop/versions</c> aveva un
/// parser suo che leggeva <c>{"Areas":[…],"Saves":N}</c> — chiavi che <b>nessuno scriveva</b> — e restituiva
/// sempre stringa vuota, mentre la pagina Audit mostrava il JSON crudo. Due rese diverse dello stesso evento,
/// entrambe sbagliate a modo loro.</para>
///
/// <para>⚠️ <b>Legge anche il vocabolario vecchio.</b> La revoca di un permesso è stata <c>Archive</c> fino al
/// 22 agosto 2026 e <c>Delete</c> dopo; la chiave dell'ACC nei dettagli è stata <c>acc</c> minuscola e poi
/// <c>Acc</c>. Le righe vecchie non si riscrivono: si leggono. Da qui la ricerca delle proprietà JSON
/// <b>senza distinzione fra maiuscole e minuscole</b>.</para>
/// </summary>
public static class AuditNarrator
{
    /// <summary>Famiglia dell'evento: è ciò su cui filtrano i chip della pagina, e non coincide con
    /// <see cref="AuditAction"/> (una eliminazione di documento e una revoca di permesso sono due famiglie).</summary>
    public enum Categoria { Pubblicazione, Bozza, Documento, Permesso, Gerarchia, Lock, Sorgenti, Incarico, Statistiche, Altro }

    public static Categoria CategoriaDi(AuditEntry e) => (e.EntityType, e.Action) switch
    {
        ("DocumentVersion", AuditAction.Publish) => Categoria.Pubblicazione,
        ("DocumentVersion", AuditAction.Discard) => Categoria.Bozza,
        ("EditGrant", _) => Categoria.Permesso,
        ("ImportPolicy", _) => Categoria.Sorgenti,
        // ⚠️ È la famiglia più prolifica del registro: un incarico attraversa quattro stati, e ogni passaggio
        // è una riga. Il chip di famiglia serve proprio a poterla mettere da parte quando si cerca altro.
        ("EditorTask", _) => Categoria.Incarico,
        // L'unica famiglia che descrive una LETTURA: lo staff ha aperto le statistiche personali di qualcuno.
        ("StatsProfile", _) => Categoria.Statistiche,
        (_, AuditAction.HierarchyChange) => Categoria.Gerarchia,
        (_, AuditAction.ForceUnlock) => Categoria.Lock,
        ("Document", _) => Categoria.Documento,
        _ => Categoria.Altro,
    };

    /// <summary>Etichetta corta della famiglia (la pill nella colonna «Evento»).</summary>
    public static string Etichetta(Categoria c, IStringLocalizer L) => L["Audit_Cat_" + c].Value;

    /// <summary>Classe colore della pill: verde ciò che pubblica, rosso ciò che toglie, ambra ciò che forza.</summary>
    public static string ClassePill(AuditEntry e) => (CategoriaDi(e), e.Action) switch
    {
        (Categoria.Pubblicazione, _) => "green",
        (Categoria.Lock, _) => "amber",
        // Ambra come il lock, e per la stessa ragione: non è una perdita di dati, è un cambio di regime che
        // qualcuno dovrà spiegare se i dati smettono di aggiornarsi.
        (Categoria.Sorgenti, _) => "amber",
        // Ambra per la stessa ragione delle altre due: non si è perso niente, ma è un atto che chi l'ha
        // fatto potrebbe dover spiegare.
        (Categoria.Statistiche, _) => "amber",
        (_, AuditAction.Delete) => "red",
        (_, AuditAction.Archive) => "red",
        (_, AuditAction.Discard) => "red",
        (Categoria.Gerarchia, _) => "neutral",
        _ => "blue",
    };

    /// <summary>
    /// Di quale <b>documento</b> parla la riga, quando ne parla. Le righe di pubblicazione e di scarto hanno
    /// come <c>EntityId</c> la <i>versione</i>, non il documento: l'Id del documento sta nei dettagli
    /// (<c>DocumentId</c>, o <c>Id</c> nelle righe di publish). Serve a chi legge per andarsi a prendere i
    /// titoli mancanti in una query sola, invece che una per riga.
    /// </summary>
    public static int? DocumentoDi(AuditEntry e)
    {
        var d = Dettagli(e);
        if (Int(d, "DocumentId") is int docId) return docId;
        if (e.EntityType == "DocumentVersion" && Int(d, "Id") is int idPub) return idPub;
        if (e.EntityType == "Document" && int.TryParse(e.EntityId, out var idEnt)) return idEnt;
        return null;
    }

    /// <summary>
    /// Il bersaglio in chiaro: il titolo del documento, il callsign del nodo, la chiave della risorsa.
    ///
    /// <para>L'ordine delle fonti non e' casuale. Prima il <b>titolo scritto nella riga</b>: e' quello che il
    /// documento aveva <b>al momento dell'atto</b>, e per un documento eliminato e' l'unico rimasto. Poi la
    /// mappa <paramref name="titoli"/>, che serve alle righe scritte prima del 22 agosto 2026 — quelle
    /// portano solo l'Id. L'Id nudo (&#171;documento #12&#187;) resta l'ultima spiaggia: vuol dire che quel
    /// documento non c'e' piu' e la sua riga e' troppo vecchia per averne registrato il nome.</para>
    /// </summary>
    public static string Bersaglio(AuditEntry e, IStringLocalizer L, IReadOnlyDictionary<int, string>? titoli = null)
    {
        var d = Dettagli(e);
        var titolo = Str(d, "Title") ?? Str(d, "Nodo");
        if (titolo is not null) return titolo;
        if (DocumentoDi(e) is int doc)
        {
            if (titoli is not null && titoli.TryGetValue(doc, out var dalDb)) return dalDb;
            return L["Audit_DocN", doc].Value;
        }
        if (e.EntityType == "ImportPolicy") return L["Sorg_Title"].Value;
        if (e.EntityType == "EditResourceLock") return e.EntityId;
        if (e.EntityType == "EditGrant") return L["Audit_GrantN", e.EntityId].Value;
        // Il bersaglio è la PERSONA guardata, non una pagina: l'EntityId è il suo VID.
        if (e.EntityType == "StatsProfile") return L["Audit_VidN", e.EntityId].Value;
        return $"{e.EntityType} {e.EntityId}";
    }

    /// <summary>
    /// Cosa è successo, in una frase. È la colonna che sostituisce il JSON crudo — che resta, ma nel
    /// <c>title</c> della cella: è la verità grezza e non si butta, però non è quella che si legge scorrendo.
    /// </summary>
    public static string Frase(AuditEntry e, IStringLocalizer L)
    {
        var d = Dettagli(e);
        var acc = Str(d, "Acc");
        switch (CategoriaDi(e))
        {
            case Categoria.Pubblicazione:
                // ⚠️ La clausola AIRAC c'è solo se la riga la porta: le pubblicazioni fatte dal pannello
                // release scrivono `Reason` e non il ciclo, e «(AIRAC —)» in fondo a ogni riga è rumore che
                // si legge 200 volte per un dato che non c'è.
                return Str(d, "AiracCycle") is { } ciclo
                    ? L["Audit_Fr_Publish", Int(d, "VersionNumber") ?? 0, ciclo].Value
                    : L["Audit_Fr_PublishNoAirac", Int(d, "VersionNumber") ?? 0].Value;
            case Categoria.Bozza:
                return L["Audit_Fr_Discard", Int(d, "VersionNumber") ?? 0].Value;
            case Categoria.Documento:
                if (e.Action == AuditAction.Delete)
                    return L["Audit_Fr_DocDelete", Int(d, "Releases") ?? 0].Value;
                return Bool(d, "Hidden") == true ? L["Audit_Fr_DocHide"].Value : L["Audit_Fr_DocShow"].Value;
            case Categoria.Permesso:
                // Archive (righe fino al 22-ago) e Delete (dopo) sono lo stesso atto: stessa frase.
                var chi = Int(d, "UserId");
                var quale = chi is int v ? L["Audit_VidN", v].Value : "—";
                return e.Action is AuditAction.Delete or AuditAction.Archive
                    ? L["Audit_Fr_GrantRevoke", quale, acc ?? "—"].Value
                    : L["Audit_Fr_GrantAdd", quale, acc ?? "—"].Value;
            case Categoria.Incarico:
                // Tre atti sotto la stessa famiglia, distinti da COSA porta la riga, non dall'azione: la
                // riassegnazione e il cambio di stato sono entrambi `Update`.
                if (e.Action == AuditAction.Delete)
                    return L["Audit_Fr_TaskDelete", Persona(d, "AssigneeUserId", "AssigneeName", L),
                                                    Stato(Str(d, "Stato"), L)].Value;
                if (Str(d, "A") is { } aStato)
                    return L["Audit_Fr_TaskStatus", Stato(Str(d, "Da"), L), Stato(aStato, L)].Value;
                if (Prop(d, "AUserId") is not null)
                    return L["Audit_Fr_TaskReassign", Persona(d, "DaUserId", "DaNome", L),
                                                      Persona(d, "AUserId", "ANome", L)].Value;
                return L["Audit_Fr_TaskCreate", Persona(d, "AssigneeUserId", "AssigneeName", L)].Value;
            case Categoria.Gerarchia:
                return L["Audit_Fr_Hierarchy", Str(d, "Da") ?? L["Audit_NoParent"].Value,
                                               Str(d, "A") ?? L["Audit_NoParent"].Value].Value;
            case Categoria.Lock:
                return L["Audit_Fr_ForceUnlock", Str(d, "HeldByName") ?? Int(d, "HeldByUserId")?.ToString() ?? "—"].Value;
            case Categoria.Statistiche:
                return L["Audit_Fr_StatsView"].Value;
            case Categoria.Sorgenti:
                // Le sole categorie CAMBIATE, e nelle due direzioni separate: «manuale → da sorgente» è
                // l'unica che, al prossimo import, sovrascrive il lavoro fatto a mano.
                var verso = Categorie(d, "DaSorgente", L);
                var manuali = Categorie(d, "Manuali", L);
                var frasi = new List<string>();
                if (verso.Length > 0) frasi.Add(L["Audit_Fr_SrcToSource", string.Join(", ", verso)].Value);
                if (manuali.Length > 0) frasi.Add(L["Audit_Fr_SrcToManual", string.Join(", ", manuali)].Value);
                return frasi.Count > 0 ? string.Join(" · ", frasi) : L["Audit_Fr_SrcChanged"].Value;
            default:
                return e.Action.ToString();
        }
    }

    /// <summary>Chi, in chiaro: il nome se la riga lo porta (regola 136), altrimenti il VID — che un VID non
    /// è un nome, ma è meglio di un trattino quando la persona non l'ha mai avuto scritto.</summary>
    private static string Persona(JsonElement? d, string chiaveId, string chiaveNome, IStringLocalizer L) =>
        Str(d, chiaveNome) ?? (Int(d, chiaveId) is int vid ? L["Audit_VidN", vid].Value : "—");

    /// <summary>Il nome di uno stato d'incarico nella lingua della pagina. ⚠️ Nel JSON sta il nome dell'enum,
    /// e vale il patto del narratore: chiave sconosciuta ⇒ testo grezzo, mai <c>TaskStatus_Qualcosa</c> a video.</summary>
    private static string Stato(string? valore, IStringLocalizer L)
    {
        if (string.IsNullOrWhiteSpace(valore)) return "—";
        var s = L["TaskStatus_" + valore];
        return s.ResourceNotFound ? valore : s.Value;
    }

    /// <summary>ACC toccato dall'evento, se la riga lo porta (colonna e chip della pagina).</summary>
    public static string? Acc(AuditEntry e) => Str(Dettagli(e), "Acc");

    // ---- lettura tollerante del JSON --------------------------------------------------------------------
    // ⚠️ I dettagli sono JSON scritto in momenti diversi da versioni diverse dell'app: qui non si pretende una
    // forma, si cerca una chiave. Un JSON illeggibile non è un motivo per rompere la pagina del registro.
    private static JsonElement? Dettagli(AuditEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.DetailsJson)) return null;
        try { return JsonDocument.Parse(e.DetailsJson).RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    private static JsonElement? Prop(JsonElement? root, string nome)
    {
        if (root is not { ValueKind: JsonValueKind.Object } o) return null;
        foreach (var p in o.EnumerateObject())
            if (string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }

    private static string? Str(JsonElement? root, string nome) =>
        Prop(root, nome) is { ValueKind: JsonValueKind.String } v && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString() : null;

    private static int? Int(JsonElement? root, string nome) =>
        Prop(root, nome) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var n) ? n : null;

    /// <summary>Elenco di categorie di import, tradotto col vocabolario della pagina Sorgenti
    /// (<see cref="ImportCategoryLabels"/>). Chiave assente o non un array ⇒ elenco vuoto.</summary>
    private static string[] Categorie(JsonElement? root, string nome, IStringLocalizer L)
    {
        if (Prop(root, nome) is not { ValueKind: JsonValueKind.Array } arr) return Array.Empty<string>();
        return arr.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => ImportCategoryLabels.Etichetta(x.GetString()!, L))
            .ToArray();
    }

    private static bool? Bool(JsonElement? root, string nome) => Prop(root, nome) switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        _ => null,
    };
}
