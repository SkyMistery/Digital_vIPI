using Vipi.Domain;

namespace Vipi.Ui.Shared;

/// <summary>Link all'editor per il documento collegato a un incarico, dalle stesse chiavi delle release
/// (<see cref="ReleaseTargetType"/> + TargetKey). Per i tipi la cui chiave non porta il codice ACC (App/Airport/vLOA)
/// si rimanda all'hub editor (l'etichetta del target guida l'utente); AccVipi ha l'ACC nella chiave → link diretto.</summary>
public static class TaskDocLink
{
    public static string LinkFor(ReleaseTargetType? type, string? key)
    {
        if (type is ReleaseTargetType.AccVipi && !string.IsNullOrWhiteSpace(key))
        {
            var parts = key!.Split('|', 2);
            var acc = parts[0].ToLowerInvariant();
            var root = parts.Length > 1 ? parts[1] : "";
            return string.IsNullOrEmpty(root) ? $"/vsop/{acc}/editor" : $"/vsop/{acc}/editor?tree={root}";
        }
        return "/vsop/versioni";
    }
}
