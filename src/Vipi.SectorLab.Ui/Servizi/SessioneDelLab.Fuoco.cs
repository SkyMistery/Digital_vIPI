using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>Il riquadro di quel che sta nel fuoco, in gradi: la mappa ci si porta sopra.</summary>
public sealed record RiquadroDelFuoco(double Sud, double Ovest, double Nord, double Est);

public sealed partial class SessioneDelLab
{
    // --- il fuoco: lavorare su uno scalo (carta «Lavorare su uno scalo o su un settore», R1-R2, D1) ------------------

    /// <summary>
    /// Lo scalo (R1) o la posizione ATC (R7) su cui si lavora, o null: col fuoco la mappa del Sector mostra solo i suoi file
    /// e le sue righe, più le coste. Resta finché non si esce, anche dopo un riavvio del Lab (si ricorda in <c>fuoco.txt</c>).
    /// </summary>
    public string? Fuoco { get; private set; }

    /// <summary>R7: il fuoco è su un settore (una posizione ATC, D2), non su uno scalo.</summary>
    public bool FuocoSuUnSettore { get; private set; }

    /// <summary>Come si chiama quel che sta nel fuoco, nei testi: «scalo» o «settore».</summary>
    public string GenereDelFuoco => FuocoSuUnSettore ? "settore" : "scalo";

    // In fuoco.txt un settore si scrive col prefisso: uno scalo, com'era prima di R7, solo col codice.
    private const string PrefissoDelSettore = "settore:";

    /// <summary>D1: col fuoco il resto del sector è nascosto; con questa casella si vede in grigio, e non si sceglie.</summary>
    public bool RestoInGrigio { get; private set; }

    /// <summary>Quante volte il fuoco è cambiato (acceso, spento, il resto in grigio): la mappa lo confronta col suo.</summary>
    public int VersioneDelFuoco { get; private set; }

    /// <summary>Quante volte si è chiesto di portare la mappa sul fuoco: all'accensione, e dal tasto «⌖ Lo scalo».</summary>
    public int InquadratureDelFuoco { get; private set; }

    /// <summary>
    /// Le chiavi della vista del fuoco (R2): per uno scalo le righe dell'<c>.ap</c> e del <c>.rw</c> (<c>file#3</c>), i
    /// suoi file e i suoi disegni a terra interi (<c>file#*</c>); per un settore (R7) i settori dei <c>.tfl</c> e i bordi
    /// col suo nome (<c>file#3</c>) e i file col suo nome (<c>file#*</c>). Le posizioni no: stanno nel <c>.frq</c> e non
    /// si disegnano. Vuota senza fuoco.
    /// </summary>
    public IReadOnlyList<string> ChiaviDelFuoco
    {
        get
        {
            if (Fuoco is null || Sessione is null)
                return [];
            if (_chiaviDelFuoco is null || _chiaviDi != (Fuoco, FuocoSuUnSettore, _versioneDeiFile))
                (_chiaviDelFuoco, _chiaviDi) = (FuocoSuUnSettore
                    ? Chiavi(VistaPerSettore.Di(Sessione, Fuoco), ["settori", "bordi"], ["file"])
                    : Chiavi(VistaPerScalo.Di(Sessione, Fuoco), ["scalo", "piste"], ["file", "terra"]),
                    (Fuoco, FuocoSuUnSettore, _versioneDeiFile));
            return _chiaviDelFuoco;

            static List<string> Chiavi(IReadOnlyList<SezioneDelloScalo> sezioni, string[] diRecord, string[] diFile) =>
            [
                .. sezioni.Where(s => diRecord.Contains(s.Id)).SelectMany(s => s.Voci).Select(v => ChiaveDellaVista(v.File, v.Record)),
                .. sezioni.Where(s => diFile.Contains(s.Id)).SelectMany(s => s.Voci).Select(v => ChiaveDellaVista(v.File, null)),
            ];
        }
    }

    private List<string>? _chiaviDelFuoco;
    private (string Fuoco, bool Settore, int Versione) _chiaviDi;

    /// <summary>
    /// R7: i bordi dei settori accanto a quello del fuoco — le forme degli altri settori e bordi che arrivano nel suo
    /// riquadro —, che la mappa mostra attenuate anche col resto nascosto: si vede dove si toccano. Vuota per uno scalo.
    /// </summary>
    public IReadOnlyList<string> ChiaviAccanto
    {
        get
        {
            if (!FuocoSuUnSettore || Riquadro() is not { } suo)
                return [];
            if (_accanto is null || _accantoDi != (Fuoco!, _versioneDeiFile) || !ReferenceEquals(_accantoCon, Strati))
            {
                var nelFuoco = new HashSet<string>(ChiaviDelFuoco, StringComparer.Ordinal);
                _accanto = [.. Strati.Where(s => s.Id == StratoDeiSettori).SelectMany(s => s.Forme)
                    .Where(f => !nelFuoco.Contains(ChiaveDellaVista(f.File, f.Record)) && !nelFuoco.Contains(ChiaveDellaVista(f.File, null)))
                    .Where(f => Tocca(f, suo))
                    .Select(f => ChiaveDellaVista(f.File, f.Record))
                    .Distinct(StringComparer.Ordinal)];
                (_accantoDi, _accantoCon) = ((Fuoco!, _versioneDeiFile), Strati);
            }
            return _accanto;

            // Il riquadro della forma entra in quello del settore: basta, per «accanto» (un confine in comune lo fa sempre).
            static bool Tocca(FormaDellaMappa forma, RiquadroDelFuoco riquadro)
            {
                var punti = forma.Tratti.SelectMany(t => t).ToList();
                return punti.Count > 0
                       && punti.Min(p => p.LatitudeDeg) <= riquadro.Nord && punti.Max(p => p.LatitudeDeg) >= riquadro.Sud
                       && punti.Min(p => p.LongitudeDeg) <= riquadro.Est && punti.Max(p => p.LongitudeDeg) >= riquadro.Ovest;
            }
        }
    }

    private List<string>? _accanto;
    private (string Fuoco, int Versione) _accantoDi;
    private object? _accantoCon;

    // Lo strato dei .tfl e dei bordi (.hartcc, .lartcc, .artcc): quello del settore, per il riquadro e per «accanto».
    private const string StratoDeiSettori = "settori";

    /// <summary>R7: quel che la mappa di uno spazio mostra attenuato accanto al fuoco; solo sul Sector, senza una vista a mano.</summary>
    public IReadOnlyList<string> AccantoDaMostrare(SpazioDiLavoro spazio)
        => spazio is SpazioDiLavoro.Sector && _mappe[spazio].InVista.Count == 0 ? ChiaviAccanto : [];

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

        ScaloScelto = scalo;
        AccendiIlFuoco(scalo, settore: false);
        return true;
    }

    /// <summary>
    /// R7: si lavora su questa posizione ATC (D2). La sceglie anche nella linguetta Settori; per il resto come
    /// <see cref="LavoraSu"/>. Falso, col rifiuto, se la posizione non ha niente da disegnare.
    /// </summary>
    public bool LavoraSulSettore(string? codice)
    {
        string? settore = codice?.Trim().ToUpperInvariant();
        if (Sessione is null || string.IsNullOrEmpty(settore) || !SettoriDellaVista().Any(s => s.Codice == settore))
        {
            Rifiuto = $"«{codice}» non è una posizione con un settore o un bordo in questo sector.";
            Avvisa();
            return false;
        }

        SettoreScelto = settore;
        AccendiIlFuoco(settore, settore: true);
        return true;
    }

    private void AccendiIlFuoco(string fuoco, bool settore)
    {
        Fuoco = fuoco;
        FuocoSuUnSettore = settore;
        var mappa = _mappe[SpazioDiLavoro.Sector];
        // La vista a mano era del lavoro di prima: col fuoco si riparte dallo scalo (o dal settore) intero.
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
        Ricorda("fuoco.txt", settore ? PrefissoDelSettore + fuoco : fuoco, "fuoco");
        Avvisa();
    }

    /// <summary>Esce dal fuoco: la mappa torna agli strati accesi, come fuori dal fuoco. Lo scalo (o il settore) resta scelto.</summary>
    public void EsciDalFuoco()
    {
        if (Fuoco is null)
            return;
        Fuoco = null;
        FuocoSuUnSettore = false;
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
        // R7: per un settore, il suo poligono e i suoi bordi.
        IReadOnlyList<string> principali = FuocoSuUnSettore ? [StratoDeiSettori] : StratiDelGround;
        var delGround = Strati.Where(s => principali.Contains(s.Tipo.Id)).SelectMany(FormeDelFuoco).ToList();
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

    /// <summary>All'apertura di un sector: il fuoco ricordato, se lo scalo (o la posizione) c'è ancora.</summary>
    private void RiprendiIlFuoco()
    {
        Fuoco = null;
        FuocoSuUnSettore = false;
        _chiaviDelFuoco = null;
        _accanto = null;
        if (Ricordata("fuoco.txt") is not { } ricordato)
            return;
        if (ricordato.StartsWith(PrefissoDelSettore, StringComparison.Ordinal))
        {
            string settore = ricordato[PrefissoDelSettore.Length..];
            if (SettoriDellaVista().Any(s => s.Codice == settore))
                LavoraSulSettore(settore);
        }
        else if (ScaliDellaVista().Any(s => s.Icao == ricordato))
        {
            LavoraSu(ricordato);
        }
    }

    // --- la vista per settore (R7): la linguetta Settori ---------------------------------------------------------------

    /// <summary>La posizione scelta nella linguetta Settori, o null.</summary>
    public string? SettoreScelto { get; private set; }

    /// <summary>Le posizioni fra cui scegliere: quelle con un settore o un bordo da disegnare.</summary>
    public IReadOnlyList<SettoreDellaVista> SettoriDellaVista()
    {
        if (Sessione is null)
            return [];
        if (_settori is null || !ReferenceEquals(_settoriDi, Strati) || _settoriAlla != _versioneDeiFile)
            (_settori, _settoriDi, _settoriAlla) = (VistaPerSettore.Settori(Sessione), Strati, _versioneDeiFile);
        return _settori;
    }

    /// <summary>Sceglie la posizione della linguetta Settori (null: nessuna).</summary>
    public void ScegliIlSettore(string? codice)
    {
        SettoreScelto = string.IsNullOrWhiteSpace(codice) ? null : codice.Trim().ToUpperInvariant();
        Registro.Scrivi("settore", SettoreScelto ?? "nessuno");
        Avvisa();
    }

    /// <summary>Tutto quello che riguarda la posizione scelta: settori, bordi, file col suo nome, righe dei <c>.frq</c>.</summary>
    public IReadOnlyList<SezioneDelloScalo> VistaDelSettore()
    {
        if (Sessione is null || SettoreScelto is null)
            return [];
        if (_vistaDelSettore is null || _vistaDelSettoreDi != (SettoreScelto, _versioneDeiFile) || !ReferenceEquals(_vistaDelSettoreCon, Strati))
            (_vistaDelSettore, _vistaDelSettoreDi, _vistaDelSettoreCon) = (VistaPerSettore.Di(Sessione, SettoreScelto), (SettoreScelto, _versioneDeiFile), Strati);
        return _vistaDelSettore;
    }

    private IReadOnlyList<SettoreDellaVista>? _settori;
    private object? _settoriDi;
    private int _settoriAlla;
    private IReadOnlyList<SezioneDelloScalo>? _vistaDelSettore;
    private (string Settore, int Versione) _vistaDelSettoreDi;
    private object? _vistaDelSettoreCon;

    /// <summary>Alla chiusura del sector: il settore scelto e quel che ne era calcolato erano di quell'albero.</summary>
    private void ScordaIlSettore()
    {
        SettoreScelto = null;
        FuocoSuUnSettore = false;
        _settori = null;
        _vistaDelSettore = null;
        _accanto = null;
    }
}
