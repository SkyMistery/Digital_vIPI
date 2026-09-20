using System.Text.RegularExpressions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un blocco dell'editor in cui cercare SID scritte a mano.</summary>
/// <param name="Id">L'id del blocco.</param>
/// <param name="Dove">Il titolo della sezione, per dire a chi scrive dove sta.</param>
/// <param name="Format">Il formato: di una tabella si guardano le celle.</param>
/// <param name="Body">Il testo (prosa, callout, didascalia, nota).</param>
/// <param name="BodyJson">Il JSON del blocco: per una tabella, le celle.</param>
public sealed record BloccoDaCercare(int Id, string Dove, BlockFormat Format, string? Body, string? BodyJson);

/// <summary>Una SID scritta a mano che si può convertire in riferimento.</summary>
/// <param name="BloccoId">Il blocco in cui sta.</param>
/// <param name="Dove">La sezione.</param>
/// <param name="Trovato">Com'è scritta nel testo (<c>CDC6A</c>, <c>BANAV 8A</c>).</param>
/// <param name="Sid">La SID della tabella che le corrisponde.</param>
/// <param name="Volte">Quante volte compare in quel blocco.</param>
/// <param name="Contesto">Un pezzo di testo attorno alla prima occorrenza, per riconoscerla.</param>
public sealed record PropostaSid(int BloccoId, string Dove, string Trovato, ProceduraCitabile Sid, int Volte, string Contesto);

/// <summary>Una forma compatta (<c>CDC6A/B</c>) che nessun riconoscimento può convertire da solo.</summary>
/// <param name="BloccoId">Il blocco in cui sta.</param>
/// <param name="Dove">La sezione.</param>
/// <param name="Trovato">Com'è scritta.</param>
public sealed record FormaDaSistemare(int BloccoId, string Dove, string Trovato);

/// <summary>L'esito di una ricerca: le proposte e le forme da sistemare a mano.</summary>
public sealed record EsitoRicercaSid(IReadOnlyList<PropostaSid> Proposte, IReadOnlyList<FormaDaSistemare> DaSistemare)
{
    public static EsitoRicercaSid Vuoto { get; } = new(Array.Empty<PropostaSid>(), Array.Empty<FormaDaSistemare>());
}

/// <summary>
/// La conversione dei testi già scritti (§A73, slice 5): trova le SID scritte a mano che corrispondono a una SID
/// della tabella di uno scalo e le propone, una per una, come riferimenti. Nessuna conversione è automatica.
///
/// <para>⚠️ Si riconosce solo ciò che corrisponde ESATTAMENTE a un nome della tabella, in una delle forme in cui
/// lo si scrive: il codice (<c>BANA8A</c>), il nome completo (<c>BANAV 8A</c>) o il nome completo senza spazio
/// (<c>BANAV8A</c>). Le forme compatte (<c>CDC6A/B</c>, misurate sulla vSOP MIL LIBV il 18 settembre 2026) sono due
/// SID in una: si ELENCANO, e le sistema chi scrive.</para>
/// </summary>
public static class ConversioneSid
{
    private static readonly Regex Compatta = new(
        // `CDC6A/B`, `ROZHU5A/5B`, e anche `CDC6A/CDC6B` (due SID intere con la barra in mezzo).
        @"(?<![A-Z0-9])([A-Z]{2,7})([0-9])([A-Z])(?:/(?:[A-Z]{2,7})?[0-9]?[A-Z])+(?![A-Z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static EsitoRicercaSid Cerca(IEnumerable<BloccoDaCercare> blocchi, IReadOnlyList<ProceduraCitabile> sids)
    {
        if (sids.Count == 0) return EsitoRicercaSid.Vuoto;

        // Ogni forma scritta → la sua SID. Le più lunghe prima: «BANAV 8A» va cercato prima di un eventuale nome
        // più corto che ne fosse un pezzo.
        var forme = new Dictionary<string, ProceduraCitabile>(StringComparer.Ordinal);
        foreach (var s in sids)
            foreach (var f in FormeDi(s))
                forme.TryAdd(f, s);
        // Una regex per forma, costruita UNA volta: dentro il giro sui blocchi e sulle celle sarebbero decine di
        // migliaia di costruzioni su un documento grande (revisione del 18 settembre 2026).
        var ordinate = forme.Keys.OrderByDescending(f => f.Length).Select(f => (Forma: f, Re: Occorrenze(f))).ToList();

        var radici = new HashSet<string>(sids.SelectMany(s => FormeDi(s)).Select(RiferimentiProcedura.Radice), StringComparer.Ordinal);

        var proposte = new List<PropostaSid>();
        var daSistemare = new List<FormaDaSistemare>();
        foreach (var b in blocchi)
        {
            var campi = CampiDi(b).ToList();
            foreach (var (forma, re) in ordinate)
            {
                var volte = 0;
                string? contesto = null;
                foreach (var campo in campi)
                {
                    foreach (Match m in re.Matches(campo))
                    {
                        if (m.Groups["rif"].Success) continue;   // dentro un riferimento già fatto
                        volte++;
                        contesto ??= Contesto(campo, m.Index, m.Length);
                    }
                }
                if (volte > 0) proposte.Add(new PropostaSid(b.Id, b.Dove, forma, forme[forma], volte, contesto!));
            }

            foreach (var campo in campi)
                foreach (Match m in Compatta.Matches(campo))
                    if (radici.Contains(RiferimentiProcedura.Radice(m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value))
                        && !daSistemare.Any(d => d.BloccoId == b.Id && d.Trovato == m.Value))
                        daSistemare.Add(new FormaDaSistemare(b.Id, b.Dove, m.Value));
        }
        return new EsitoRicercaSid(proposte, daSistemare);
    }

    /// <summary>
    /// Il blocco con le scelte applicate: ogni forma trovata diventa il riferimento della sua SID, in tutto il
    /// blocco. Torna <c>null</c> per il campo che non cambia — la stessa convenzione del salvataggio dell'editor.
    /// </summary>
    public static (string? Body, string? BodyJson) Converti(BloccoDaCercare b, IEnumerable<PropostaSid> scelte)
    {
        var mie = scelte.Where(p => p.BloccoId == b.Id).OrderByDescending(p => p.Trovato.Length)
            .Select(p => (Re: Occorrenze(p.Trovato), p.Sid.Riferimento)).ToList();
        if (mie.Count == 0) return (null, null);

        string Applica(string testo) => mie.Aggregate(testo, (t, p) =>
            p.Re.Replace(t, m => m.Groups["rif"].Success ? m.Value : p.Riferimento));

        string? body = null;
        if (!string.IsNullOrEmpty(b.Body))
        {
            var nuovo = Applica(b.Body);
            if (nuovo != b.Body) body = nuovo;
        }

        string? json = null;
        if (HaCelle(b))
            json = ConvertiCelle(b.BodyJson!, Applica);
        return (body, json);
    }

    /// <summary>
    /// Le celle convertite SUL POSTO nel JSON originale, o <c>null</c> se nessuna cambia.
    /// <para>⚠️ Non con <c>TabellaGenerica.Leggi/Scrivi</c> (revisione del 18 settembre 2026): quelle tengono solo
    /// colonne e celle, e una tabella di contenuto porta anche <c>tableId</c>, <c>unified</c> e, per riga,
    /// <c>primary</c>, <c>star</c>, <c>group</c>, <c>r</c> — che <c>TableBlock</c> legge. Un clic su «Converti tutte»
    /// avrebbe tolto la ★ alle frequenze e il raggruppamento alle tabelle unificate, senza dirlo.</para>
    /// </summary>
    private static string? ConvertiCelle(string bodyJson, Func<string, string> applica)
    {
        System.Text.Json.Nodes.JsonNode? radice;
        try { radice = System.Text.Json.Nodes.JsonNode.Parse(bodyJson); }
        catch (System.Text.Json.JsonException) { return null; }
        if (radice?["rows"] is not System.Text.Json.Nodes.JsonArray righe) return null;

        var cambiata = false;
        foreach (var riga in righe)
        {
            if (riga?["cells"] is not System.Text.Json.Nodes.JsonArray celle) continue;
            for (var i = 0; i < celle.Count; i++)
            {
                if (celle[i] is not System.Text.Json.Nodes.JsonValue v || !v.TryGetValue<string>(out var testo)) continue;
                var nuova = applica(testo);
                if (nuova == testo) continue;
                celle[i] = nuova;
                cambiata = true;
            }
        }
        return cambiata ? radice.ToJsonString() : null;
    }

    /// <summary>Le forme in cui una SID si scrive a mano.</summary>
    private static IEnumerable<string> FormeDi(ProceduraCitabile s) =>
        new[] { s.Codice, s.Esteso, s.Esteso.Replace(" ", ""), s.Codice.Replace(" ", "") }
            .Where(f => f.Length >= 3).Distinct(StringComparer.Ordinal);

    // Un riferimento già fatto (gruppo «rif», da saltare) o la forma a parola intera. ⚠️ Né «/» né «-» ATTACCATI, da
    // tutti e due i lati (revisione del 18 settembre 2026): `CDC6A/B` è una forma compatta da sistemare a mano;
    // `CDC6A/CDC6B` si convertiva a metà; `…/CDC6A.pdf` in un indirizzo diventava un riferimento e rompeva il link;
    // e il pezzo di un composto (`ARL1K` in `BRL1Z-ARL1K`) si proponeva da solo.
    private static Regex Occorrenze(string forma) => new(
        @"(?<rif>\[\[SID [^\]]*\]\])|(?<![A-Z0-9/\-])" + Regex.Escape(forma) + @"(?![A-Z0-9/\-])",
        RegexOptions.CultureInvariant);

    private static IEnumerable<string> CampiDi(BloccoDaCercare b)
    {
        if (!string.IsNullOrEmpty(b.Body)) yield return b.Body;
        if (!HaCelle(b)) yield break;
        var (_, righe) = TabellaGenerica.Leggi(b.BodyJson);
        foreach (var riga in righe)
            foreach (var cella in riga)
                if (!string.IsNullOrEmpty(cella)) yield return cella;
    }

    // Le celle si guardano solo in una tabella di CONTENUTO: il payload di una sezione resa dalla pagina ha la sua
    // forma e non è testo da convertire.
    private static bool HaCelle(BloccoDaCercare b) =>
        b.Format == BlockFormat.Table && SectionPayload.EEditoriale(b.BodyJson);

    private static string Contesto(string campo, int indice, int lunghezza)
    {
        var da = Math.Max(0, indice - 30);
        var a = Math.Min(campo.Length, indice + lunghezza + 30);
        return (da > 0 ? "…" : "") + campo[da..a].Replace('\n', ' ') + (a < campo.Length ? "…" : "");
    }
}
