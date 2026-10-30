using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>
/// Un tratto che si ridisegna sopra la carta posata (carta «Gli aeroporti nel Lab», S4): lo scalo, la carta di avvicinamento,
/// la parte a cui va in coda (la sua chiave; null = una parte nuova) e i punti presi sulla mappa.
/// </summary>
public sealed record TracciatoInCorso(string Icao, CartaDiAvvicinamento Carta, string? InCodaA, IReadOnlyList<PuntoDelTracciato> Punti);

public sealed partial class SessioneDelLab
{
    /// <summary>Il tratto che si sta ridisegnando, o null. C'è solo con la carta posata sulla mappa.</summary>
    public TracciatoInCorso? Tracciato { get; private set; }

    /// <summary>Cresce quando il tratto cambia: la mappa ne ridisegna l'anteprima.</summary>
    public int VersioneDelTracciato { get; private set; }

    /// <summary>
    /// «Ridisegna sulla carta»: apre un tratto sopra la carta posata. <paramref name="inCodaA"/>: la chiave della parte a cui va
    /// in coda, o null per una parte nuova. Falso col perché se la carta non è posata.
    /// </summary>
    public bool ApriIlTracciato(string icao, CartaDiAvvicinamento carta, string? inCodaA)
    {
        ArgumentNullException.ThrowIfNull(carta);
        if (Posa is not { } posa || posa.Icao != icao || posa.Carta.Page != carta.Carta.Page || posa.Posa is null)
            return Rifiuta("Posa prima la carta sulla mappa: il tratto si ridisegna sopra di lei.");
        if (inCodaA is not null && ProceduraPerChiave(inCodaA) is null)
            return Rifiuta($"{inCodaA}: non è nell'AIP espanso sulla mappa.");
        Tracciato = new TracciatoInCorso(icao, carta, inCodaA, []);
        Rifiuto = null;
        Registro.Scrivi("carte", $"tratto sopra {carta.Carta.Page}" + (inCodaA is null ? ", parte nuova" : $", in coda a {inCodaA}"));
        VersioneDelTracciato++;
        Avvisa();
        return true;
    }

    /// <summary>Chiude il tratto senza salvarlo.</summary>
    public void ChiudiIlTracciato()
    {
        if (Tracciato is null)
            return;
        Tracciato = null;
        VersioneDelTracciato++;
        Avvisa();
    }

    /// <summary>
    /// Un punto del tratto dalla mappa: dove si è cliccato, o un punto dell'AIP (col suo nome, che diventa il fix del tratto se
    /// è un punto, una radioassistenza o una soglia che il Lab sa trovare).
    /// </summary>
    private void PuntoDelTracciatoDallaMappa(double lat, double lon, string? nome)
    {
        if (Tracciato is not { } t)
            return;
        // L'etichetta di una radioassistenza è «NAP VOR/DME»: il fix è il nome.
        var fix = nome?.Trim().Split(' ', 2)[0].ToUpperInvariant();
        if (fix is { Length: 0 } || (fix is not null && DoveSta(t.Icao, fix) is null))
            fix = null;
        Metti(t, new PuntoDelTracciato(lat, lon, fix, Tipo(t)));
    }

    /// <summary>Un punto del tratto dal suo nome: un fix, una radioassistenza, «RWY 24». Falso col perché se non si trova.</summary>
    public bool PuntoDelTracciatoPerNome(string nome)
    {
        if (Tracciato is not { } t || string.IsNullOrWhiteSpace(nome))
            return false;
        var cercato = nome.Trim().ToUpperInvariant();
        if (DoveSta(t.Icao, cercato) is not { } dove)
            return Rifiuta($"«{nome.Trim()}» non è un punto dell'AIP espanso, né una soglia di {t.Icao}.");
        Rifiuto = null;
        // «RWY 24» come la scrivono i tratti degli avvicinamenti: «RWY24».
        Metti(t, new PuntoDelTracciato(dove.LatitudeDeg, dove.LongitudeDeg, cercato.Replace(" ", ""), Tipo(t)));
        return true;
    }

    /// <summary>Cambia un punto del tratto (il tipo, la quota, la velocità, il centro e il verso di un arco).</summary>
    public void CambiaIlPuntoDelTracciato(int indice, PuntoDelTracciato punto)
    {
        if (Tracciato is not { } t || indice < 0 || indice >= t.Punti.Count)
            return;
        Tracciato = t with { Punti = [.. t.Punti.Select((p, i) => i == indice ? punto : p)] };
        VersioneDelTracciato++;
        Avvisa();
    }

    /// <summary>Toglie un punto del tratto.</summary>
    public void TogliIlPuntoDelTracciato(int indice)
    {
        if (Tracciato is not { } t || indice < 0 || indice >= t.Punti.Count)
            return;
        Tracciato = t with { Punti = [.. t.Punti.Where((_, i) => i != indice)] };
        VersioneDelTracciato++;
        Avvisa();
    }

    /// <summary>
    /// L'anteprima del tratto sulla mappa: i punti in fila, e per una coda l'ultimo punto della parte prima, così si vede da
    /// dove il tratto parte. Gli archi in anteprima sono corde: il disegno vero lo fa la mappa dopo il salvataggio.
    /// </summary>
    public IReadOnlyList<Coordinate> AnteprimaDelTracciato
    {
        get
        {
            if (Tracciato is not { } t)
                return [];
            var punti = new List<Coordinate>();
            if (t.InCodaA is { } chiave && ProceduraPerChiave(chiave)?.Legs.LastOrDefault(l => l.Position is not null || l.Fix is not null) is { } ultimo)
            {
                if (ultimo.Position is { } p)
                    punti.Add(new Coordinate(p.Lat, p.Lon));
                else if (DoveSta(t.Icao, ultimo.Fix!) is { } c)
                    punti.Add(c);
            }
            punti.AddRange(t.Punti.Select(p => new Coordinate(p.Lat, p.Lon)));
            return punti;
        }
    }

    /// <summary>
    /// «Salva nell'AIP espanso»: la parte nuova come aggiunta, o la parte coi tratti in coda come correzione, con la carta per
    /// fonte. Il tratto si chiude; la carta resta posata.
    /// </summary>
    public async Task<bool> SalvaIlTracciatoAsync(string? segmento, string? perche, string? fonte)
    {
        if (Tracciato is not { } t)
            return false;
        string? chiave = t.InCodaA;
        var fatto = await CorreggiLAipAsync((modello, file, ciclo) =>
        {
            if (t.InCodaA is null)
            {
                var (nuova, no) = TracciatiSullaCarta.NuovaParte(t.Icao, t.Carta, segmento, t.Punti);
                if (nuova is null)
                    return (null, no);
                chiave = nuova.Key;
                return CorrezioniDellAip.Aggiungi(modello, file, CorrectionKind.Procedure, nuova, perche, fonte, ciclo, Oggi);
            }
            var prima = (modello.Procedures ?? []).FirstOrDefault(p => p.Key == t.InCodaA);
            if (prima is null)
                return (null, $"{t.InCodaA}: non è nell'AIP espanso del ciclo {ciclo}.");
            var (lunga, noCoda) = TracciatiSullaCarta.InCoda(prima, t.Punti);
            return lunga is null ? (null, noCoda) : CorrezioniDellAip.Correggi(modello, file, CorrectionKind.Procedure, t.InCodaA, lunga, perche, fonte, ciclo, Oggi);
        }, sceglieLaStessa: true).ConfigureAwait(false); // resta sullo scalo: la sua pagina, con la carta aperta
        if (fatto)
        {
            Tracciato = null;
            VersioneDelTracciato++;
            // La parte salvata si accende sulla mappa: si guarda subito sopra la carta.
            if (StratoDellAip is { } strato && strato.Voci.ToList().FindIndex(v => v.Chiave == chiave && v.Tipo is null) is var i and >= 0)
                AccendiLeVociDellAip([i], true);
            Avvisa();
        }
        return fatto;
    }

    /// <summary>La correzione in piedi che ha fatto (o allungato) la procedura con quella chiave, o null se è com'è nel PDF.</summary>
    public AipCorrection? CorrezioneDellaProcedura(string? chiave) =>
        chiave is not null && _correzioniSullaMappa is { } file ? CorrezioniDellAip.Attiva(file, CorrectionKind.Procedure, chiave) : null;

    /// <summary>La procedura del modello sulla mappa con quella chiave, o null.</summary>
    public Procedure? ProceduraPerChiave(string chiave) =>
        _modelloSullaMappa?.Procedures?.FirstOrDefault(p => p.Key == chiave);

    // Il primo punto di una parte nuova è l'inizio (IF); poi dritto al punto (TF).
    private static string Tipo(TracciatoInCorso t) => t.InCodaA is null && t.Punti.Count == 0 ? "IF" : "TF";

    private void Metti(TracciatoInCorso t, PuntoDelTracciato punto)
    {
        Tracciato = t with { Punti = [.. t.Punti, punto] };
        Registro.Scrivi("carte", $"punto {Tracciato.Punti.Count} del tratto: {punto.Fix ?? PoseDelleCarte.InGradi(punto.Lat, punto.Lon)}");
        VersioneDelTracciato++;
        Avvisa();
    }
}
