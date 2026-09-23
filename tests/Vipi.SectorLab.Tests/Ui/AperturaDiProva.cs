using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Aprire la cartella in un test a schermo (slice 10). Dall'apertura parte la validazione dell'albero fuori dal
/// circuito, e quando finisce la pagina si ridisegna: se succede fra il <c>Find</c> e il <c>Click</c> di un test, bUnit
/// cerca un gestore che il ridisegno ha rinumerato («There is no event handler with ID …», tre rossi nella prima corsa
/// della suite intera). L'app non ne soffre — Blazor Server tiene gli id vecchi finché il browser non conferma il
/// disegno — ma bUnit sì: nei test si apre e si aspetta anche la validazione.
/// </summary>
internal static class AperturaDiProva
{
    public static async Task<bool> ApriEValidaAsync(this SessioneDelLab lab, string percorso)
    {
        bool aperta = await lab.ApriAsync(percorso);
        await lab.Validazione;
        return aperta;
    }
}
