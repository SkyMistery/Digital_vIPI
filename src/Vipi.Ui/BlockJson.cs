using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui;

/// <summary>Utility per leggere il discriminatore "variant" dei blocchi con BodyJson.</summary>
internal static class BlockJson
{
    /// <summary>
    /// Vero se questo blocco è il <b>payload</b> di una sezione resa dalla pagina, e non contenuto da
    /// mostrare: le tabelle del vSOP militare, la selezione delle aree.
    ///
    /// <para>
    /// ⚠️ Serve <b>al viewer e all'editor insieme</b>, e la verifica dal vivo del 30 agosto 2026 ha mostrato
    /// perché: appena le sezioni militari hanno cominciato a tenere i propri blocchi, il payload di
    /// «Nominativi» è finito nella tabella generica dell'<i>editor</i> — che legge le righe come oggetti con
    /// <c>cells</c> — e la pagina è andata in <b>500</b>. Una regola sola, in un posto solo.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>E «un posto solo» vuol dire DAVVERO uno.</b> Fino all'8 settembre 2026 qui la regola era
    /// «tabella <b>con</b> una variante», mentre <see cref="SectionPayload.EEditoriale"/> — che decide dove
    /// il payload si legge e si scrive — ne usava un'altra: «è editoriale chi ha <c>mediaId</c>,
    /// <c>ref</c>, o <c>columns</c> senza <c>variant</c>». Due regole per la stessa domanda, e il caso in
    /// cui divergono esisteva già: la selezione delle aree regolamentate è
    /// <c>{"OwnAuto":…,"OwnIds":[…]}</c> — un blocco <c>Table</c> <b>senza</b> <c>variant</c>. Per la
    /// scrittura era payload, per l'editor era una tabella scritta a mano: la disegnava con la sua
    /// intestazione e il tasto <b>«+ riga»</b>, e chi lo premeva ci scriveva sopra
    /// <c>{"columns":…,"rows":…}</c> — <b>tutte le aree scelte cancellate</b>, senza un errore.
    /// Segnalato dal committente su 1.16.0, riprodotto a schermo su LIBG.
    /// </para>
    /// <para>
    /// Adesso la domanda la fa <see cref="SectionPayload.EEditoriale"/> e basta. Il formato del blocco non
    /// serve più: il payload militare è anch'esso <c>Table</c>, quindi il formato non ha mai distinto
    /// niente — è la <b>forma del JSON</b> che distingue, ed è scritta in un posto solo.
    /// </para>
    /// </summary>
    public static bool EStruttura(string? bodyJson) =>
        !string.IsNullOrWhiteSpace(bodyJson) && !SectionPayload.EEditoriale(bodyJson);

    public static string? Variant(string? bodyJson)
    {
        if (string.IsNullOrWhiteSpace(bodyJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(bodyJson);
            // ⚠️ `TryGetProperty` su una radice che NON e' un oggetto alza `InvalidOperationException`, non
            // `JsonException`: la forma legacy delle aree regolamentate e' un array (`["1029",…]`), e senza
            // questa guardia una sola riga vecchia in archivio manda in 500 la pagina che la mostra.
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            return doc.RootElement.TryGetProperty("variant", out var v) ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
