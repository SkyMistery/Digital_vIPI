using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>Il riquadro di quel che sta nel fuoco, in gradi: la mappa ci si porta sopra.</summary>
public sealed record RiquadroDelFuoco(double Sud, double Ovest, double Nord, double Est);

public sealed partial class SessioneDelLab
{
    // --- il fuoco: lavorare su uno scalo (carta «Lavorare su uno scalo o su un settore», R1-R2, D1) ------------------

    /// <summary>
    /// Lo scalo su cui si lavora (R1), o null: col fuoco la mappa del Sector mostra solo i suoi file e le sue righe, più le
    /// coste. Resta finché non si esce, anche dopo un riavvio del Lab (si ricorda in <c>fuoco.txt</c>).
    /// </summary>
    public string? Fuoco { get; private set; }

    /// <summary>D1: col fuoco il resto del sector è nascosto; con questa casella si vede in grigio, e non si sceglie.</summary>
    public bool RestoInGrigio { get; private set; }

    /// <summary>Quante volte il fuoco è cambiato (acceso, spento, il resto in grigio): la mappa lo confronta col suo.</summary>
    public int VersioneDelFuoco { get; private set; }

    /// <summary>Quante volte si è chiesto di portare la mappa sul fuoco: all'accensione, e dal tasto «⌖ Lo scalo».</summary>
    public int InquadratureDelFuoco { get; private set; }

    /// <summary>
    /// Le chiavi della vista del fuoco (R2): le righe dell'<c>.ap</c> e del <c>.rw</c> dello scalo (<c>file#3</c>), i
    /// suoi file e i suoi disegni a terra interi (<c>file#*</c>). Le posizioni no: stanno nel <c>.frq</c> e non si
    /// disegnano. Vuota senza fuoco.
    /// </summary>
    public IReadOnlyList<string> ChiaviDelFuoco
    {
        get
        {
            if (Fuoco is null || Sessione is null)
                return [];
            if (_chiaviDelFuoco is null || _chiaviDi != (Fuoco, _versioneDeiFile))
                (_chiaviDelFuoco, _chiaviDi) = (Chiavi(VistaPerScalo.Di(Sessione, Fuoco)), (Fuoco, _versioneDeiFile));
            return _chiaviDelFuoco;

            static List<string> Chiavi(IReadOnlyList<SezioneDelloScalo> sezioni) =>
            [
                .. sezioni.Where(s => s.Id is "scalo" or "piste").SelectMany(s => s.Voci).Select(v => ChiaveDellaVista(v.File, v.Record)),
                .. sezioni.Where(s => s.Id is "file" or "terra").SelectMany(s => s.Voci).Select(v => ChiaveDellaVista(v.File, null)),
            ];
        }
    }

    private List<string>? _chiaviDelFuoco;
    private (string Scalo, int Versione) _chiaviDi;

    /// <summary>
    /// Quel che la mappa di uno spazio mostra: la vista scelta a mano (Solo questo sulla mappa, «In vista»), se c'è; se no,
    /// sulla mappa del Sector, il fuoco. Così dentro il fuoco «Solo questo» stringe ancora, e «mostra tutto» torna allo scalo.
    /// </summary>
    public IReadOnlyList<string> VistaDaMostrare(SpazioDiLavoro spazio)
        => _mappe[spazio].InVista is { Count: > 0 } aMano ? aMano
            : spazio is SpazioDiLavoro.Sector ? ChiaviDelFuoco
            : [];

    /// <summary>Se la mappa di uno spazio mostra il resto del sector in grigio: solo col fuoco, la casella accesa e senza una vista a mano.</summary>
    public bool RestoInGrigioDi(SpazioDiLavoro spazio)
        => spazio is SpazioDiLavoro.Sector && Fuoco is not null && RestoInGrigio && _mappe[spazio].InVista.Count == 0;

    /// <summary>
    /// R1: si lavora su questo scalo. Lo sceglie anche nella linguetta Scali, accende gli strati dei suoi file e chiede alla
    /// mappa di portarsi sullo scalo. Falso, col rifiuto, se lo scalo non è fra quelli degli <c>.ap</c>.
    /// </summary>
    public bool LavoraSu(string? icao)
    {
        string? scalo = icao?.Trim().ToUpperInvariant();
        if (Sessione is null || string.IsNullOrEmpty(scalo) || !ScaliDellaVista().Any(s => s.Icao == scalo))
        {
            Rifiuto = $"«{icao}» non è uno scalo degli .ap di questo sector.";
            Avvisa();
            return false;
        }

        Fuoco = scalo;
        ScaloScelto = scalo;
        var mappa = _mappe[SpazioDiLavoro.Sector];
        // La vista a mano era del lavoro di prima: col fuoco si riparte dallo scalo intero.
        if (mappa.InVista.Count > 0)
        {
            mappa.InVista.Clear();
            mappa.VersioneDellaVista++;
        }
        // Gli strati dei suoi file si accendono: senza, sulla mappa non ci sarebbe niente dello scalo.
        foreach (string chiave in ChiaviDelFuoco)
            if (StratiDellaMappa.DiFile(chiave[..chiave.LastIndexOf('#')]) is { } tipo && Strati.Any(s => s.Tipo.Id == tipo.Id))
                mappa.Accesi.Add(tipo.Id);

        VersioneDelFuoco++;
        InquadratureDelFuoco++;
        Ricorda("fuoco.txt", scalo, "fuoco");
        Avvisa();
        return true;
    }

    /// <summary>Esce dal fuoco: la mappa torna agli strati accesi, come fuori dal fuoco. Lo scalo resta scelto negli Scali.</summary>
    public void EsciDalFuoco()
    {
        if (Fuoco is null)
            return;
        Fuoco = null;
        VersioneDelFuoco++;
        Ricorda("fuoco.txt", null, "fuoco");
        Avvisa();
    }

    /// <summary>D1: il resto del sector in grigio (vero) o nascosto.</summary>
    public void MostraIlRestoInGrigio(bool inGrigio)
    {
        if (RestoInGrigio == inGrigio)
            return;
        RestoInGrigio = inGrigio;
        VersioneDelFuoco++;
        Avvisa();
    }

    /// <summary>«⌖ Lo scalo»: riporta la mappa sullo scalo intero.</summary>
    public void InquadraIlFuoco()
    {
        if (Fuoco is null)
            return;
        InquadratureDelFuoco++;
        Avvisa();
    }

    /// <summary>
    /// R4: col fuoco scegliere un record dell'elenco non sposta la mappa — si lavora sullo scalo, e ogni salto costava una
    /// rotella per tornare a vedere il piazzale. La mappa si muove solo se il record è fuori vista (e allora ci si porta
    /// senza cambiare zoom, se ci sta), o con «⌖ Portami lì».
    /// </summary>
    public bool SceltaSenzaZoom => Fuoco is not null && SpazioAttivo is SpazioDiLavoro.Sector;

    /// <summary>Quante volte si è chiesto «⌖ Portami lì»: la mappa inquadra il record scelto per intero.</summary>
    public int PortamiLiChiesti { get; private set; }

    /// <summary>«⌖ Portami lì»: la mappa va sul record scelto, come fuori dal fuoco.</summary>
    public void PortamiLi()
    {
        if (Scelta is null)
            return;
        PortamiLiChiesti++;
        Avvisa();
    }

    /// <summary>R3: se il file è dello scalo del fuoco (uno dei suoi file o dei suoi disegni a terra): si apre nella linguetta Scali.</summary>
    public bool EDelFuoco(string? relativo)
        => relativo is not null && ChiaviDelFuoco.Contains(ChiaveDellaVista(relativo, null));

    /// <summary>
    /// R8: i file toccati che sono dello scalo del fuoco, nell'ordine delle modifiche. Le sue righe dentro l'<c>.ap</c> e il
    /// <c>.rw</c> no: quei file hanno anche gli altri scali, e annullarli interi toccherebbe anche loro.
    /// </summary>
    public IReadOnlyList<string> FileToccatiDelFuoco => [.. Modifiche.FileToccati.Where(EDelFuoco)];

    /// <summary>R8: quante modifiche in sospeso stanno nei file dello scalo del fuoco.</summary>
    public int ModificheDelFuoco => Modifiche.Tutte.Count(m => EDelFuoco(m.File));

    /// <summary>R8: annulla le sole modifiche nei file dello scalo del fuoco, in un gesto solo (un Ctrl+Z le rimette).</summary>
    public void AnnullaQuelleDelFuoco()
    {
        if (Fuoco is not { } scalo || FileToccatiDelFuoco is not { Count: > 0 } file)
            return;
        NellaStoria($"annullate le modifiche di {scalo}", () =>
        {
            bool almenoUna = false;
            foreach (string uno in file)
                almenoUna |= AnnullaTutteAdesso(uno);
            return almenoUna;
        });
    }

    /// <summary>
    /// Il riquadro del fuoco (R2): le forme dello scalo negli strati del ground — disegni a terra, aeroporti a terra, piste.
    /// SID, STAR e rotte VFR dello scalo si vedono, ma vanno lontano: nel riquadro lo scalo diventerebbe un puntino (visto
    /// sul banco con LIRF). Se lo scalo non ha ground, tutte le sue forme. Null senza fuoco o senza niente da disegnare.
    /// </summary>
    public RiquadroDelFuoco? Riquadro()
    {
        if (ChiaviDelFuoco.Count == 0)
            return null;
        var delGround = Strati.Where(s => StratiDelGround.Contains(s.Tipo.Id)).SelectMany(FormeDelFuoco).ToList();
        var forme = delGround.Count > 0 ? delGround : [.. Strati.SelectMany(FormeDelFuoco)];
        double sud = double.MaxValue, ovest = double.MaxValue, nord = double.MinValue, est = double.MinValue;
        foreach (var forma in forme)
        {
            foreach (var punto in forma.Tratti.SelectMany(t => t))
            {
                sud = Math.Min(sud, punto.LatitudeDeg);
                nord = Math.Max(nord, punto.LatitudeDeg);
                ovest = Math.Min(ovest, punto.LongitudeDeg);
                est = Math.Max(est, punto.LongitudeDeg);
            }
        }
        return sud > nord ? null : new RiquadroDelFuoco(sud, ovest, nord, est);
    }

    /// <summary>R2: quante forme di uno strato stanno nel fuoco (gli Strati contano quelle dello scalo, non dell'Italia).</summary>
    public int FormeNelFuoco(StratoDellaMappa strato) => FormeDelFuoco(strato).Count;

    /// <summary>
    /// Le forme di uno strato che stanno nel fuoco; senza fuoco, tutte. Le coste restano intere: col fuoco si vedono sempre.
    /// </summary>
    private IReadOnlyList<FormaDellaMappa> FormeDelFuoco(StratoDellaMappa strato)
    {
        ArgumentNullException.ThrowIfNull(strato);
        if (ChiaviDelFuoco is not { Count: > 0 } chiavi || strato.Tipo.Sfondo)
            return strato.Forme;
        var tutte = new HashSet<string>(chiavi, StringComparer.Ordinal);
        return [.. strato.Forme.Where(f => tutte.Contains(ChiaveDellaVista(f.File, null)) || tutte.Contains(ChiaveDellaVista(f.File, f.Record)))];
    }

    /// <summary>I generi di uno strato col fuoco: contati sulle sole forme dello scalo.</summary>
    private IReadOnlyList<GenereDelloStrato> GeneriDelFuoco(StratoDellaMappa strato)
        => GeneriDellaMappa.Di(strato with { Forme = FormeDelFuoco(strato) });

    /// <summary>All'apertura di un sector: il fuoco ricordato, se lo scalo c'è ancora.</summary>
    private void RiprendiIlFuoco()
    {
        Fuoco = null;
        _chiaviDelFuoco = null;
        if (Ricordata("fuoco.txt") is { } scalo && ScaliDellaVista().Any(s => s.Icao == scalo))
            LavoraSu(scalo);
    }
}
