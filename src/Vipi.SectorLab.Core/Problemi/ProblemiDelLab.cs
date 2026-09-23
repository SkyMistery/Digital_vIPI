using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Problemi;

/// <summary>
/// Un problema del validatore, agganciato all'app (slice 10): il file col nome che ha nella sessione
/// (<c>SectorFiles/Include/IT/…</c>) e il record che ha quella riga, se ce l'ha — il clic porta lì.
/// </summary>
/// <param name="File">Relativo alla radice del clone, barre dritte, come <see cref="FileAperto.Relativo"/>.</param>
/// <param name="Record">L'indice del record con quella riga; nullo per un commento, una riga non capita, un file intero.</param>
public sealed record ProblemaNelLab(ProblemaDelSector Problema, string File, int? Record)
{
    public Gravita Gravita => Problema.Gravita;

    /// <summary>Il nome del file senza cartelle, per l'elenco.</summary>
    public string Nome => File[(File.LastIndexOf('/') + 1)..];
}

/// <summary>
/// Il pannello dei problemi (carta F3 §2.2 passo 8, slice 10): il validatore del motore sull'albero intero, gli stessi
/// numeri che ha dato nella carta F2 (130 errori, 353 avvisi sul master del 22 settembre), agganciati ai file e ai
/// record della sessione.
/// </summary>
public static class ProblemiDelLab
{
    /// <summary>
    /// Valida l'albero dal disco (1,7-2,0 s sull'albero vero: fuori dal circuito) e aggancia ogni problema.
    /// <para>⚠️ Il validatore scrive i percorsi relativi a <c>SectorFiles</c> e con le barre del sistema
    /// (<c>Include\IT\NAVAIDS\itvor.vor</c> su Windows); la sessione li tiene relativi alla radice del clone e con le
    /// barre dritte. Si traducono qui, in un posto solo.</para>
    /// </summary>
    public static IReadOnlyList<ProblemaNelLab> DellAlbero(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        return Aggancia(sessione, Validatore.ValidaLAlbero(sessione.Cartella.SectorFiles), dalValidatoreDellAlbero: true);
    }

    /// <summary>
    /// Aggancia dei problemi ai file della sessione. Quelli del validatore dell'albero hanno il file relativo a
    /// <c>SectorFiles</c>; quelli del controllo delle modifiche, già il nome della sessione.
    /// </summary>
    public static IReadOnlyList<ProblemaNelLab> Aggancia(SessioneAperta sessione, IEnumerable<ProblemaDelSector> problemi,
                                                         bool dalValidatoreDellAlbero = false)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(problemi);
        return [.. problemi.Select(p =>
        {
            string file = dalValidatoreDellAlbero
                ? "SectorFiles/" + p.File.Replace('\\', '/').TrimStart('/')
                : p.File;
            int? record = p.Riga > 0 && sessione.File.TryGetValue(file, out var aperto) && aperto is IFileConRecord conRecord
                ? conRecord.RecordDellaRiga(p.Riga)
                : null;
            // Il nome come lo ha la sessione (maiuscole e minuscole comprese): il clic lo cerca lì.
            if (sessione.File.TryGetValue(file, out var trovato))
                file = trovato.Relativo;
            return new ProblemaNelLab(p, file, record);
        })];
    }

    /// <summary>
    /// Il filtro del pannello: un testo che si cerca nel percorso, nella regola, nel dettaglio e nella riga (un ICAO
    /// trova i file che lo portano nel nome e le righe che lo citano), e quali gravità mostrare.
    /// </summary>
    public static IEnumerable<ProblemaNelLab> Filtra(IEnumerable<ProblemaNelLab> problemi, string? testo, bool errori, bool avvisi)
    {
        ArgumentNullException.ThrowIfNull(problemi);
        string cercato = (testo ?? "").Trim();
        return problemi.Where(p =>
            (p.Gravita == Gravita.Errore ? errori : avvisi)
            && (cercato.Length == 0
                || p.File.Contains(cercato, StringComparison.OrdinalIgnoreCase)
                || p.Problema.Regola.ToString().Contains(cercato, StringComparison.OrdinalIgnoreCase)
                || p.Problema.Dettaglio.Contains(cercato, StringComparison.OrdinalIgnoreCase)
                || p.Problema.Testo.Contains(cercato, StringComparison.OrdinalIgnoreCase)));
    }
}
