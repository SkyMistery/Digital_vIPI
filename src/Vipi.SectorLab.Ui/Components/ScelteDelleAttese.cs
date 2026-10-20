using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Ui.Components;

/// <summary>
/// Le attese che mancano nel sector, come l'AOD le sceglie prima di portarle (F6 slice 2c; nel Confronto dalla R7):
/// spuntata o no, in rotta o nello scalo, scritta o ovale. Le tiene la pagina dei Confronti e le leggono la riga e la
/// sezione. Le regole sono quelle di prima, spostate da «Lettura e confronti».
/// </summary>
public sealed class ScelteDelleAttese
{
    public sealed record SceltaInCorso(bool Scelta, string? Scalo, bool Ovale);

    private readonly Dictionary<string, SceltaInCorso> _attese = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>La scelta di un'attesa: di base non spuntata, nel primo scalo proposto, scritta.</summary>
    public SceltaInCorso Stato(PropostaDiAttesa proposta) =>
        _attese.GetValueOrDefault(proposta.Attesa.Fix) ?? new SceltaInCorso(false, proposta.Scali.FirstOrDefault()?.Icao, false);

    /// <summary>Una riga spuntata o no.</summary>
    public void Cambia(PropostaDiAttesa proposta, bool scelta) =>
        _attese[proposta.Attesa.Fix] = Stato(proposta) with { Scelta = scelta };

    /// <summary>Lo scalo scelto (vuoto = in rotta: allora niente ovale, per ora).</summary>
    public void CambiaLoScalo(PropostaDiAttesa proposta, string? scalo)
    {
        var dove = string.IsNullOrEmpty(scalo) ? null : scalo;
        _attese[proposta.Attesa.Fix] = Stato(proposta) with { Scalo = dove, Ovale = dove is not null && Stato(proposta).Ovale };
    }

    /// <summary>La scritta o l'ovale.</summary>
    public void CambiaIlCome(PropostaDiAttesa proposta, bool ovale) =>
        _attese[proposta.Attesa.Fix] = Stato(proposta) with { Ovale = ovale };

    /// <summary>Le attese mancanti spuntate, da portare.</summary>
    public List<SessioneDelLab.SceltaDiAttesa> Scelte(IEnumerable<DifferenzaDiEnr3> esiti) =>
        [.. esiti.Where(e => e.Esito == EsitoDelConfronto.MancaNelSector)
            .Select(e => (e.Nome, Stato: _attese.GetValueOrDefault(e.Nome)))
            .Where(x => x.Stato is { Scelta: true })
            .Select(x => new SessioneDelLab.SceltaDiAttesa(x.Nome, x.Stato!.Scalo, x.Stato.Ovale && x.Stato.Scalo is not null))];

    /// <summary>Le attese portate escono dalla scelta.</summary>
    public void Portate(IEnumerable<SessioneDelLab.SceltaDiAttesa> portate)
    {
        foreach (var s in portate)
            _attese.Remove(s.Fix);
    }
}
