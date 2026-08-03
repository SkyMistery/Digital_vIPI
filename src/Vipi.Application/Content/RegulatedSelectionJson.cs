using System.Text.Json;

namespace Vipi.Application.Content;

/// <summary>
/// Lettura del <c>BodyJson</c> di una sezione <c>regulated</c>. Unico punto che conosce i formati salvati nel tempo,
/// così viewer, editor e diagnostica vedono la stessa selezione: null/vuoto = nessuno stato persistito (automatico
/// per chi l'automatico ce l'ha), array legacy <c>["id",…]</c> = manuale con quegli id, oggetto = selezione nativa.
/// </summary>
public static class RegulatedSelectionJson
{
    /// <summary>Selezione salvata; JSON assente o illeggibile → default automatico (nessun id).</summary>
    public static RegulatedSelection Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new RegulatedSelection { OwnAuto = true };
        try
        {
            if (json.TrimStart().StartsWith('['))
                return new RegulatedSelection { OwnAuto = false, OwnIds = JsonSerializer.Deserialize<List<string>>(json) ?? new() };
            return JsonSerializer.Deserialize<RegulatedSelection>(json) ?? new RegulatedSelection { OwnAuto = true };
        }
        catch (JsonException) { return new RegulatedSelection { OwnAuto = true }; }
    }
}
