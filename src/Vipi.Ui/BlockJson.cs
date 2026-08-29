using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Ui;

/// <summary>Utility per leggere il discriminatore "variant" dei blocchi con BodyJson.</summary>
public static class BlockJson
{
    /// <summary>
    /// Vero se questo blocco è il <b>payload</b> di una sezione resa dalla pagina, e non contenuto da
    /// mostrare: le tabelle del vSOP militare, la selezione delle aree.
    ///
    /// <para>
    /// ⚠️ La regola è «tabella <b>con</b> una variante»: le tabelle generiche scritte a mano non ne hanno
    /// (<c>{"columns":…,"rows":…}</c>), mentre un payload la porta sempre. Le altre varianti — <c>tip</c>,
    /// <c>area</c> — stanno su blocchi di formato diverso e non passano di qui.
    /// </para>
    /// <para>
    /// ⚠️ Serve <b>al viewer e all'editor insieme</b>, e la verifica dal vivo del 30 agosto 2026 ha mostrato
    /// perché: appena le sezioni militari hanno cominciato a tenere i propri blocchi, il payload di
    /// «Nominativi» è finito nella tabella generica dell'<i>editor</i> — che legge le righe come oggetti con
    /// <c>cells</c> — e la pagina è andata in <b>500</b>. Una regola sola, in un posto solo.
    /// </para>
    /// </summary>
    public static bool EStruttura(BlockFormat format, string? bodyJson) =>
        format == BlockFormat.Table && !string.IsNullOrEmpty(Variant(bodyJson));

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
