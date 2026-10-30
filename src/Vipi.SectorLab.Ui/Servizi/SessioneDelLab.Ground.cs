using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Ui.Servizi;

public sealed partial class SessioneDelLab
{
    /// <summary>
    /// Gli strati del sector che fanno il ground (carta «Gli aeroporti nel Lab», S5): i disegni <c>.geo</c>, la terra
    /// (<c>.pol</c>, <c>.txi</c>, <c>.gts</c>) e le piste. Sopra la carta d'aerodromo posata, sulla mappa dell'AIP espanso.
    /// </summary>
    public static readonly IReadOnlyList<string> StratiDelGround = ["geo", "terra", "piste"];

    /// <summary>Il ground del sector sta sulla mappa dell'AIP espanso, sopra la carta posata.</summary>
    public bool GroundSullaCarta { get; private set; }

    /// <summary>
    /// «Il ground del sector sopra la carta» (S5, il metodo misto di §2 D): sulla mappa dell'AIP espanso gli strati del ground
    /// del sector aperto, sopra la carta posata e sotto lo strato dell'AIP. Spento, la mappa torna alle coste e all'AIP.
    /// </summary>
    public void MostraIlGroundSullaCarta(bool mostra)
    {
        if (GroundSullaCarta == mostra)
            return;
        GroundSullaCarta = mostra;
        var mappa = _mappe[SpazioDiLavoro.Aip];
        foreach (var id in StratiDelGround)
        {
            if (mostra)
                mappa.Accesi.Add(id);
            else
                mappa.Accesi.Remove(id);
        }
        mappa.VersioneDellaVista++;
        Registro.Scrivi("carte", mostra ? "il ground del sector sulla mappa dell'AIP" : "la mappa dell'AIP senza il ground del sector");
        Avvisa();
    }

    /// <summary>Gli strati del ground per la mappa dell'AIP espanso: quelli del sector aperto, se il ground è acceso.</summary>
    private IEnumerable<StratoDellaMappa> StratiDelGroundSullaCarta() =>
        GroundSullaCarta ? Strati.Where(s => StratiDelGround.Contains(s.Id)) : [];

    // --- controllo 4: le eccezioni dello scalo ------------------------------------------------------------------------

    private readonly Dictionary<string, IReadOnlyList<EccezioneDelGround>> _eccezioni = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Le eccezioni del ground di uno scalo (dal repo dell'AIP espanso), vuoto se nessuna o se il file non si legge.</summary>
    public IReadOnlyList<EccezioneDelGround> EccezioniDi(string icao)
    {
        if (CartellaDellAipEspanso is not { } espanso)
            return [];
        if (_eccezioni.TryGetValue(icao, out var eccezioni))
            return eccezioni;
        try
        {
            eccezioni = EccezioniDelGround.Leggi(espanso, icao);
        }
        catch (InvalidDataException e)
        {
            Registro.Scrivi("ground", e.Message);
            eccezioni = [];
        }
        _eccezioni[icao] = eccezioni;
        return eccezioni;
    }

    /// <summary>L'eccezione di quella voce di quel controllo nello scalo, o null.</summary>
    public EccezioneDelGround? EccezioneDi(string icao, ControlloDelGround controllo, string voce) =>
        EccezioniDelGround.Di(EccezioniDi(icao), controllo, voce);

    /// <summary>
    /// Scrive un'eccezione dello scalo (controllo 4) in <c>eccezioni\&lt;ICAO&gt;.json</c>, con la data di oggi: il controllo
    /// salta la voce e dice il perché. Si registra con «Registra l'AIP». Falso col perché se manca o il file non si scrive.
    /// </summary>
    public bool ScriviLEccezione(string icao, ControlloDelGround controllo, string voce, string? perche)
    {
        if (CartellaDellAipEspanso is not { } espanso)
            return Rifiuta("Manca la cartella dell'AIP espanso.");
        if (string.IsNullOrWhiteSpace(perche))
            return Rifiuta("Scrivi il perché: il controllo lo mostrerà al posto della differenza.");
        try
        {
            EccezioniDelGround.Scrivi(espanso, icao, new EccezioneDelGround(controllo, voce, perche, DateOnly.FromDateTime(DateTime.Now)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return Rifiuta($"L'eccezione non si scrive: {e.Message}");
        }
        _eccezioni.Remove(icao);
        Rifiuto = null;
        Registro.Scrivi("ground", $"{icao}: eccezione {controllo} {voce.Trim()} — {perche.Trim()}");
        Avvisa();
        return true;
    }

    /// <summary>Toglie l'eccezione: il controllo torna a dire la differenza.</summary>
    public bool TogliLEccezione(string icao, ControlloDelGround controllo, string voce)
    {
        if (CartellaDellAipEspanso is not { } espanso)
            return false;
        try
        {
            EccezioniDelGround.Togli(espanso, icao, controllo, voce);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Rifiuta($"L'eccezione non si toglie: {e.Message}");
        }
        _eccezioni.Remove(icao);
        Rifiuto = null;
        Registro.Scrivi("ground", $"{icao}: tolta l'eccezione {controllo} {voce.Trim()}");
        Avvisa();
        return true;
    }
}
