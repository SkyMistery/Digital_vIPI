using System.Globalization;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Mappa;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Una voce dell'AIP com'è scritta nel modello, per la scheda della mappa: che cos'è, come si chiama, i suoi campi e,
/// per una rotta, le righe dei tratti.
/// </summary>
public sealed record VoceDellAip(string Genere, string Titolo, IReadOnlyList<(string Nome, string Valore)> Campi, IReadOnlyList<string> Righe);

/// <summary>Lo strato dell'AIP espanso di un ciclo: le forme per la mappa e, con lo stesso indice, le voci per la scheda.</summary>
public sealed record StratoDellAip(string Ciclo, StratoDellaMappa Strato, IReadOnlyList<VoceDellAip> Voci)
{
    /// <summary>Il «file» delle forme dell'AIP: non è un file del sector, e nessun gesto lo tocca.</summary>
    public string File => AipSullaMappa.Prefisso + Ciclo;
}

/// <summary>
/// L'AIP espanso sulla mappa del Lab (carta F6, prima della slice 2b): le aree di ENR 2 ed ENR 5, le rotte e le attese di
/// ENR 3, in uno strato <b>suo</b> — «AIP espanso» — sopra quelli del sector, un genere per tipo (CTR, ATZ, TMA… rotte,
/// attese) da accendere e spegnere, un colore per genere. Sola lettura: le forme hanno per «file» <c>aip:&lt;ciclo&gt;</c>,
/// che non è un file del sector, e un clic apre la scheda della voce, non un record.
/// <list type="bullet">
/// <item>Le aree con <see cref="Outline"/> a un punto per grado; un tratto di confine o di costa (l'AIP non lo disegna) è
/// una retta fra i due capi, e la scheda lo dice.</item>
/// <item>Le rotte come linee, spezzate dove l'AIP le interrompe.</item>
/// <item>Le attese di ENR 3.4.1 non hanno coordinate (il punto è per nome): si mettono dove il sector ha quel punto, e
/// dove non ce l'ha non si disegnano (la scheda resta, e lo dice).</item>
/// </list>
/// </summary>
public static class AipSullaMappa
{
    public const string Prefisso = "aip:";

    /// <summary>Lo strato: un solo id, fuori dall'elenco degli strati del sector.</summary>
    public static readonly TipoDiStrato Tipo = new("aip", "AIP espanso");

    public const string Rotte = "Rotte";
    public const string Attese = "Attese";

    /// <summary>Il colore di ogni genere sulla mappa (i colori del Lab: lo strato è uno, i tipi no).</summary>
    public static readonly IReadOnlyDictionary<string, string> Colori = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CTR"] = "#2f6fd6",
        ["ATZ"] = "#1aa0b8",
        ["TMA"] = "#7a4fc9",
        ["CTA"] = "#a06ad8",
        ["FIR/UIR"] = "#8a8f99",
        ["RMZ/TMZ"] = "#2f9f7a",
        ["P"] = "#d03030",
        ["R"] = "#e07020",
        ["D"] = "#c9a000",
        ["TSA/TRA"] = "#c0409a",
        ["Altre"] = "#6b6b6b",
        [Rotte] = "#2e8b3e",
        [Attese] = "#b06a10",
    };

    /// <summary>Vero se il «file» è quello di una forma dell'AIP.</summary>
    public static bool EDellAip(string? file) => file?.StartsWith(Prefisso, StringComparison.Ordinal) == true;

    /// <summary>
    /// Lo strato del modello. <paramref name="puntoDelSector"/>: dove il sector ha un punto, per nome (il catalogo del
    /// master scelto); null se non lo ha.
    /// </summary>
    public static StratoDellAip Di(AipDocument aip, string ciclo, Func<string, Coordinate?> puntoDelSector)
    {
        ArgumentNullException.ThrowIfNull(aip);
        ArgumentNullException.ThrowIfNull(puntoDelSector);
        var file = Prefisso + ciclo;
        var forme = new List<FormaDellaMappa>();
        var voci = new List<VoceDellAip>();

        void Aggiungi(VoceDellAip voce, TipoDiForma tipo, IReadOnlyList<IReadOnlyList<Coordinate>> tratti, IReadOnlyList<string>? nonRisolti = null)
        {
            forme.Add(new FormaDellaMappa(file, voci.Count, tipo, voce.Titolo, tratti, nonRisolti ?? [], Tratto: Colori[voce.Genere], Genere: voce.Genere));
            voci.Add(voce);
        }

        foreach (var area in aip.Airspaces)
        {
            var problemi = new List<AuroraIssue>();
            var punti = Outline.Points(area.Geometry, new OutlineOptions { PointsPerDegree = 1 }, problemi, area.Key);
            IReadOnlyList<Coordinate> anello = [.. punti.Select(Coordinata)];
            Aggiungi(Voce(area, problemi.Any(p => p.Kind == AuroraIssueKind.BorderNotDrawn)), TipoDiForma.Area, [anello]);
        }

        foreach (var rotta in aip.Routes ?? [])
        {
            // Una linea per pezzo: l'interruzione (la riga di barre dell'AIP) stacca la penna.
            var pezzi = new List<IReadOnlyList<Coordinate>>();
            var pezzo = new List<Coordinate> { Coordinata(rotta.Points[0].Position) };
            for (var i = 0; i < rotta.Segments.Count; i++)
            {
                if (rotta.Segments[i].Discontinuity)
                {
                    pezzi.Add(pezzo);
                    pezzo = [];
                }
                pezzo.Add(Coordinata(rotta.Points[i + 1].Position));
            }
            pezzi.Add(pezzo);
            Aggiungi(Voce(rotta), TipoDiForma.Linea, [.. pezzi.Where(p => p.Count > 1)]);
        }

        foreach (var attesa in aip.Holdings ?? [])
        {
            var dove = puntoDelSector(attesa.Fix);
            Aggiungi(Voce(attesa, dove is not null), TipoDiForma.Punto, dove is { } c ? [[c]] : [], dove is null ? [attesa.Fix] : null);
        }

        return new StratoDellAip(ciclo, new StratoDellaMappa(Tipo, forme), voci);
    }

    private static Coordinate Coordinata(Position p) => new(p.Lat, p.Lon);

    /// <summary>Il genere di un'area sulla mappa: i tipi dell'AIP raggruppati come li guarda un AOD.</summary>
    public static string Genere(AirspaceType tipo) => tipo switch
    {
        AirspaceType.Ctr => "CTR",
        AirspaceType.Atz => "ATZ",
        AirspaceType.Tma => "TMA",
        AirspaceType.Cta or AirspaceType.Uta => "CTA",
        AirspaceType.Fir or AirspaceType.Uir => "FIR/UIR",
        AirspaceType.Rmz or AirspaceType.Tmz => "RMZ/TMZ",
        AirspaceType.Prohibited => "P",
        AirspaceType.Restricted => "R",
        AirspaceType.Danger => "D",
        AirspaceType.Tsa or AirspaceType.Tra or AirspaceType.Cba => "TSA/TRA",
        _ => "Altre",
    };

    private static string Pagina(AipPage? p) =>
        p is null ? "—" : p.Effective is { } d ? $"{p.Reference} ({d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})" : p.Reference;

    private static VoceDellAip Voce(Airspace a, bool confineDritto)
    {
        var campi = new List<(string, string)>
        {
            ("Tipo", a.Type.ToString().ToUpperInvariant()),
            ("Sezione", a.Section),
        };
        if (a.Name is { } nome) campi.Add(("Nome", nome));
        if (a.Bands is { Count: > 0 } fasce)
            foreach (var f in fasce) campi.Add(("Fascia", $"{f.Upper.Text} – {f.Lower.Text}{(f.Class is { } c ? $" · classe {c}" : "")}"));
        else
            campi.Add(("Limiti", $"{a.Upper.Text} – {a.Lower.Text}"));
        if (a.Class is { } classe) campi.Add(("Classe", classe));
        if (a.Activity is { } attivita) campi.Add(("Attività", attivita));
        if (a.Unit is { } ente) campi.Add(("Ente", ente.Replace('\n', ' ')));
        if (a.Hours is { } orari) campi.Add(("Orari", orari));
        if (a.TransitionAltitude is { } ta) campi.Add(("Altitudine di transizione", ta));
        if (a.Remarks is { } note && note != "NIL") campi.Add(("Note", note));
        campi.Add(("Pagina", Pagina(a.Page)));
        if (confineDritto) campi.Add(("⚠", "un tratto di confine o di costa che l'AIP non disegna è qui una retta"));
        return new VoceDellAip(Genere(a.Type), a.Key, campi, []);
    }

    private static VoceDellAip Voce(Route r)
    {
        var campi = new List<(string, string)> { ("Sezione", r.Section), ("Punti", r.Points.Count.ToString(CultureInfo.InvariantCulture)) };
        if (r.Navigation is { } nav) campi.Add(("Navigazione", nav));
        campi.Add(("Pagina", Pagina(r.Page)));
        var righe = new List<string>();
        for (var i = 0; i < r.Points.Count; i++)
        {
            var p = r.Points[i];
            righe.Add($"{(p.Compulsory ? "▲" : "∆")} {p.Designator}{(p.Remarks is { } n ? $" — {n.Replace('\n', ' ')}" : "")}");
            if (i >= r.Segments.Count) break;
            var t = r.Segments[i];
            if (t.Discontinuity)
            {
                righe.Add("    — interruzione —");
                continue;
            }
            var verso = (t.Forward, t.Backward) switch { (true, true) => "↕", (true, false) => "↓ solo avanti", (false, true) => "↑ solo indietro", _ => "?" };
            righe.Add($"    {t.Track} {t.DistanceNm?.ToString("0.0", CultureInfo.InvariantCulture)} NM · {t.Upper?.Text} – {t.Lower?.Text} · {verso}"
                      + $" · MNM {t.MinimumOdd ?? "—"}/{t.MinimumEven ?? "—"}{(t.Remarks is { } note ? $" · {note}" : "")}");
        }
        return new VoceDellAip(Rotte, r.Designator, campi, righe);
    }

    private static VoceDellAip Voce(Holding h, bool disegnata)
    {
        var campi = new List<(string, string)>
        {
            ("Sezione", h.Section),
            ("Quota minima", h.Minimum.Text),
            ("Virata", h.Turn == TurnDirection.Right ? "destra" : "sinistra"),
            ("Rotta d'avvicinamento", h.InboundTrack.ToString("000", CultureInfo.InvariantCulture) + "°"),
        };
        if (h.Bearing is { } rif) campi.Add(("Riferimento", rif));
        if (h.NoteRefs is { } rimandi) campi.Add(("Rimandi", rimandi));
        if (h.Remarks is { } note) campi.Add(("Note", note.Replace('\n', ' ')));
        campi.Add(("Pagina", Pagina(h.Page)));
        campi.Add(disegnata
            ? ("Posizione", "quella del punto nel sector: ENR 3.4.1 non dà coordinate")
            : ("⚠", $"il sector non ha il punto {h.Fix}: sulla mappa non c'è"));
        return new VoceDellAip(Attese, $"{h.Fix} {h.Minimum.Text} {(h.Turn == TurnDirection.Right ? "R" : "L")} {h.InboundTrack:000}°", campi, []);
    }
}
