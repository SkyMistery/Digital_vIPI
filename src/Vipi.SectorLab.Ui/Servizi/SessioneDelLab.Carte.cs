using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>L'immagine di una pagina dell'AIP nella cache del Lab, con la misura della pagina in punti PDF.</summary>
public sealed record ImmagineDellaCarta(string File, double LarghezzaInPunti, double AltezzaInPunti);

/// <summary>
/// Una carta che si sta posando (carta «Gli aeroporti nel Lab», S3): lo scalo, il ciclo, la carta di AD 2.24, la sua
/// immagine, i punti fatti, il punto in attesa (cliccato sulla carta, aspetta il suo posto sulla mappa o il suo nome) e se
/// ci sono cambi da salvare. <see cref="DataPosata"/>: la data della pagina quando la posa salvata è stata fatta.
/// </summary>
public sealed record PosaInCorso(
    string Icao,
    string Ciclo,
    AerodromeChart Carta,
    ImmagineDellaCarta Immagine,
    IReadOnlyList<PuntoDellaPosa> Punti,
    (double X, double Y)? InAttesa,
    bool Modificata,
    DateOnly? DataPosata,
    bool Salvata)
{
    /// <summary>Come la carta sta sulla mappa coi punti di adesso, o null se non bastano.</summary>
    public PosaSullaMappa? Posa => PosaSullaMappa.Da(Punti);

    /// <summary>La posa salvata è di una pagina con un'altra data: la carta è cambiata da allora, e la posa va guardata.</summary>
    public bool DiUnAltraData => Salvata && DataPosata != Carta.Effective;
}

/// <summary>La carta posata sulla mappa dell'AIP espanso: l'immagine, tre angoli (in alto a sinistra, a destra, in basso a sinistra) e l'opacità.</summary>
public sealed record CartaSullaMappa(string Indirizzo, IReadOnlyList<Coordinate> Angoli, double Opacita);

public sealed partial class SessioneDelLab
{
    /// <summary>La risoluzione delle immagini delle carte: 200 punti per pollice, un A4 è 1 654 × 2 339 pixel.</summary>
    public const int DpiDelleCarte = 200;

    /// <summary>Dove il Lab tiene le immagini delle carte: nei suoi dati, mai nel repo (D4: si rifanno dal PDF).</summary>
    public string CacheDelleCarte => Path.Combine(_cartellaDeiDati, "cache", "carte");

    /// <summary>La carta che si sta posando, o null. Finché è aperta, la carta sta sulla mappa dell'AIP espanso.</summary>
    public PosaInCorso? Posa { get; private set; }

    /// <summary>Quanto si vede la carta sulla mappa, da 0,1 a 1.</summary>
    public double OpacitaDellaCarta { get; private set; } = 0.6;

    /// <summary>Cresce quando la carta sulla mappa cambia (aperta, chiusa, un punto, l'opacità): la mappa la ridisegna.</summary>
    public int VersioneDellaCarta { get; private set; }

    /// <summary>Cresce quando la mappa deve portarsi sulla carta (aperta una posa già salvata).</summary>
    public int InquadratureDellaCarta { get; private set; }

    /// <summary>La carta sulla mappa dell'AIP espanso, o null (nessuna posa aperta, o meno di due punti).</summary>
    public CartaSullaMappa? CartaSullaMappa =>
        Posa is { } p && p.Posa is { } posa && IndirizzoDellaCarta is { } indirizzo
            ? new CartaSullaMappa(indirizzo, posa.Angoli(p.Immagine.LarghezzaInPunti, p.Immagine.AltezzaInPunti), OpacitaDellaCarta)
            : null;

    /// <summary>
    /// L'indirizzo dell'immagine della carta che si sta posando (<c>/carta/immagine</c>), o null. Il «v» cambia con la carta:
    /// il browser non mostra quella di prima.
    /// </summary>
    public string? IndirizzoDellaCarta =>
        Posa is { } p ? "/carta/immagine?v=" + Uri.EscapeDataString(p.Ciclo + "|" + p.Carta.Page) : null;

    /// <summary>
    /// La mappa aspetta un clic per il punto in attesa della posa, o per il prossimo punto del tratto che si ridisegna (S4): il
    /// clic su un punto dell'AIP ne prende le coordinate.
    /// </summary>
    public bool PrendeUnPuntoDallaMappa => Posa?.InAttesa is not null || Tracciato is not null;

    /// <summary>
    /// L'immagine di una pagina dell'AIP (<c>«AD 2 LIRN 5-3»</c>) del ciclo: dalla cache, o la fa PDFium e la mette lì. Null
    /// col perché (<see cref="Rifiuto"/>) se la pagina non è nell'indice, il PDF non c'è o non si disegna.
    /// </summary>
    public ImmagineDellaCarta? ImmagineDellaPagina(string ciclo, string riferimento)
    {
        if (CartellaDellAipEspanso is not { } espanso || !File.Exists(IndiceDellePagine.File(espanso, ciclo)))
            return Nessuna($"Del {ciclo} non c'è l'indice delle pagine: leggi il ciclo.");
        var pagina = IndiceDellePagine.Leggi(File.ReadAllText(IndiceDellePagine.File(espanso, ciclo))).FirstOrDefault(p => p.Reference == riferimento);
        if (pagina is null)
            return Nessuna($"La pagina {riferimento} non è nell'indice del {ciclo}.");
        if (CartellaDeiPdf is not { } cartellaDeiPdf)
            return Nessuna("Manca la cartella dei PDF dell'AIP.");
        var pdf = Core.PdfAip.PdfDellAip.Elenco(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(cartellaDeiPdf, ciclo))
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), pagina.File, StringComparison.OrdinalIgnoreCase));
        if (pdf is null)
            return Nessuna($"«{pagina.File}» non è nella cartella «AIP {ciclo}».");

        var png = Path.Combine(CacheDelleCarte, ciclo, $"{Path.GetFileNameWithoutExtension(pdf)}-{pagina.Position}-{DpiDelleCarte}.png");
        try
        {
            if (!File.Exists(png))
            {
                var partenza = Environment.TickCount64;
                var immagine = Pdfium.Disegna(File.ReadAllBytes(pdf), pagina.Position, DpiDelleCarte);
                Directory.CreateDirectory(Path.GetDirectoryName(png)!);
                // Accanto e poi al posto: un'immagine a metà (il Lab chiuso mentre scrive) non resta nella cache.
                var mezza = png + ".part";
                File.WriteAllBytes(mezza, Png.Scrivi(immagine));
                File.Move(mezza, png, overwrite: true);
                Registro.Scrivi("carte", $"{riferimento} ({pagina.File} p.{pagina.Position}): {immagine.Larghezza}×{immagine.Altezza} in {Environment.TickCount64 - partenza} ms");
            }
            Span<byte> testata = stackalloc byte[24];
            using (var flusso = File.OpenRead(png))
                flusso.ReadExactly(testata);
            if (Png.Misura(testata) is not var (larghezza, altezza))
                return Nessuna($"L'immagine di {riferimento} nella cache non è un PNG: toglila ({png}).");
            return new ImmagineDellaCarta(png, larghezza * 72.0 / DpiDelleCarte, altezza * 72.0 / DpiDelleCarte);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
            return Nessuna($"{riferimento} non si disegna: {e.Message}");
        }

        ImmagineDellaCarta? Nessuna(string perche)
        {
            Rifiuta(perche);
            return null;
        }
    }

    private readonly Dictionary<string, IReadOnlyList<PosaDellaCarta>> _pose = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Le carte posate di uno scalo (dal repo dell'AIP espanso), vuoto se nessuna o se il file non si legge.</summary>
    public IReadOnlyList<PosaDellaCarta> PoseDi(string icao)
    {
        if (CartellaDellAipEspanso is not { } espanso)
            return [];
        if (_pose.TryGetValue(icao, out var pose))
            return pose;
        try
        {
            pose = PoseDelleCarte.Leggi(espanso, icao);
        }
        catch (InvalidDataException e)
        {
            Registro.Scrivi("carte", e.Message);
            pose = [];
        }
        _pose[icao] = pose;
        return pose;
    }

    /// <summary>
    /// «Posa sulla mappa»: apre la posa di una carta dello scalo. L'immagine della pagina, i punti salvati se la carta è già
    /// posata; la carta va sulla mappa appena ha due punti. Falso col perché se l'immagine non si fa.
    /// </summary>
    public bool ApriLaPosa(string icao, AerodromeChart carta)
    {
        ArgumentNullException.ThrowIfNull(carta);
        if (StratoDellAip is not { } strato)
            return Rifiuta("Nessun ciclo dell'AIP espanso sulla mappa.");
        if (ImmagineDellaPagina(strato.Ciclo, carta.Page) is not { } immagine)
            return false;
        var salvata = PoseDi(icao).FirstOrDefault(p => p.Pagina == carta.Page);
        // Il tratto di prima stava sopra un'altra carta.
        if (Tracciato is not null)
        {
            Tracciato = null;
            VersioneDelTracciato++;
        }
        Posa = new PosaInCorso(icao, strato.Ciclo, carta, immagine, salvata?.Punti ?? [], null, false, salvata?.Data, salvata is not null);
        Rifiuto = null;
        Registro.Scrivi("carte", $"posa di {carta.Page} «{carta.Title}»" + (salvata is null ? "" : $", {salvata.Punti.Count} punti salvati"));
        VersioneDellaCarta++;
        // Una carta già posata: la mappa ci si porta sopra. Una nuova no: la mappa resta dove l'AOD cerca i punti.
        if (Posa.Posa is not null)
            InquadratureDellaCarta++;
        Avvisa();
        return true;
    }

    /// <summary>Chiude la posa: la carta lascia la mappa, e con lei il tratto che le si ridisegnava sopra. I cambi non salvati si perdono.</summary>
    public void ChiudiLaPosa()
    {
        if (Posa is null)
            return;
        Posa = null;
        if (Tracciato is not null)
        {
            Tracciato = null;
            VersioneDelTracciato++;
        }
        VersioneDellaCarta++;
        Avvisa();
    }

    /// <summary>Un clic sulla carta: il punto (in punti PDF) aspetta il suo posto sulla mappa, o il suo nome.</summary>
    public void PuntoSullaCarta(double x, double y)
    {
        if (Posa is not { } p)
            return;
        Posa = p with { InAttesa = (Math.Clamp(x, 0, p.Immagine.LarghezzaInPunti), Math.Clamp(y, 0, p.Immagine.AltezzaInPunti)) };
        Avvisa();
    }

    /// <summary>Lascia stare il punto in attesa.</summary>
    public void LasciaIlPunto()
    {
        if (Posa is not { InAttesa: not null } p)
            return;
        Posa = p with { InAttesa = null };
        Avvisa();
    }

    /// <summary>Il posto sulla mappa del punto in attesa: un clic sulla mappa, o su un punto dell'AIP (col suo nome).</summary>
    public void PuntoDallaMappa(double lat, double lon, string? nome = null)
    {
        // Un punto della posa in attesa viene prima: poi il clic torna al tratto.
        if (Posa is not { InAttesa: var (x, y) } p)
        {
            PuntoDelTracciatoDallaMappa(lat, lon, nome);
            return;
        }
        Posa = p with { Punti = [.. p.Punti, new PuntoDellaPosa(x, y, lat, lon, string.IsNullOrWhiteSpace(nome) ? null : nome.Trim())], InAttesa = null, Modificata = true };
        Registro.Scrivi("carte", $"punto {Posa.Punti.Count} di {p.Carta.Page}: {(nome ?? "dalla mappa")} {PoseDelleCarte.InGradi(lat, lon)}");
        VersioneDellaCarta++;
        Avvisa();
    }

    /// <summary>
    /// Il posto del punto in attesa dal suo nome: un punto o una radioassistenza dell'AIP espanso, uno scalo, la soglia di una
    /// pista dello scalo («RWY 24») o il suo ARP. Falso col perché se il nome non si trova.
    /// </summary>
    public bool PuntoDellaPosaPerNome(string nome)
    {
        if (Posa is not { InAttesa: not null } p || string.IsNullOrWhiteSpace(nome))
            return false;
        var cercato = nome.Trim().ToUpperInvariant();
        if (DoveSta(p.Icao, cercato) is not { } dove)
            return Rifiuta($"«{nome.Trim()}» non è un punto dell'AIP espanso, né una soglia o l'ARP di {p.Icao}.");
        Rifiuto = null;
        PuntoDallaMappa(dove.LatitudeDeg, dove.LongitudeDeg, cercato);
        return true;
    }

    private Coordinate? DoveSta(string icao, string nome)
    {
        var scalo = _modelloSullaMappa?.Aerodromes?.FirstOrDefault(a => string.Equals(a.Icao, icao, StringComparison.OrdinalIgnoreCase));
        if (scalo is not null)
        {
            if (nome == "ARP" && scalo.Arp is { } arp)
                return new Coordinate(arp.Lat, arp.Lon);
            var pista = nome.StartsWith("RWY", StringComparison.Ordinal) ? nome[3..].Trim() : null;
            if (pista is { Length: > 0 } && scalo.Runways.FirstOrDefault(r => r.Designator == pista)?.Threshold is { } soglia)
                return new Coordinate(soglia.Lat, soglia.Lon);
            if (scalo.NavigationAids.FirstOrDefault(n => string.Equals(n.Designator, nome, StringComparison.OrdinalIgnoreCase)) is { } aiuto)
                return new Coordinate(aiuto.Position.Lat, aiuto.Position.Lon);
        }
        // I punti dell'AIP espanso per nome: un fix, una radioassistenza («NAP» di «NAP VOR/DME»), uno scalo.
        string[] generi = [AipSullaMappa.Punti, AipSullaMappa.Radioassistenze, AipSullaMappa.Scali];
        var forma = StratoDellAip?.Strato.Forme.FirstOrDefault(f =>
            f.Tipo == Core.Mappa.TipoDiForma.Punto && f.Tratti.Count > 0 && f.Tratti[0].Count > 0 && generi.Contains(f.Genere)
            && (string.Equals(f.Etichetta, nome, StringComparison.OrdinalIgnoreCase)
                || f.Etichetta.StartsWith(nome + " ", StringComparison.OrdinalIgnoreCase)));
        // Se no, il punto del sector (il master scelto): un fix che l'AIP espanso non ha ma il sector sì.
        return forma?.Tratti[0][0] ?? PuntoPerNome(nome);
    }

    /// <summary>Toglie un punto della posa.</summary>
    public void TogliIlPuntoDellaPosa(int indice)
    {
        if (Posa is not { } p || indice < 0 || indice >= p.Punti.Count)
            return;
        Posa = p with { Punti = [.. p.Punti.Where((_, i) => i != indice)], Modificata = true };
        VersioneDellaCarta++;
        Avvisa();
    }

    /// <summary>Quanto si vede la carta sulla mappa (da 0,1 a 1).</summary>
    public void CambiaLOpacitaDellaCarta(double opacita)
    {
        OpacitaDellaCarta = Math.Clamp(opacita, 0.1, 1);
        VersioneDellaCarta++;
        Avvisa();
    }

    /// <summary>
    /// «Salva la posa»: nel repo dell'AIP espanso, <c>posa\&lt;ICAO&gt;.json</c> (D4). Si registra con «Registra l'AIP», come
    /// una correzione. Falso col perché se i punti non bastano o il file non si scrive.
    /// </summary>
    public bool SalvaLaPosa() => ScriviLaPosa(togli: false);

    /// <summary>«Togli la posa»: la carta non è più posata (nel file non c'è più). La posa resta aperta, senza punti.</summary>
    public bool TogliLaPosa() => ScriviLaPosa(togli: true);

    private bool ScriviLaPosa(bool togli)
    {
        if (Posa is not { } p || CartellaDellAipEspanso is not { } espanso)
            return false;
        if (!togli && p.Posa is null)
            return Rifiuta("Servono almeno due punti, in posti diversi (tre non in fila).");
        var punti = togli ? [] : p.Punti;
        try
        {
            PoseDelleCarte.Scrivi(espanso, p.Icao, new PosaDellaCarta(p.Carta.Page, p.Carta.Title, p.Carta.Effective, punti));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Rifiuta($"La posa non si scrive: {e.Message}");
        }
        _pose.Remove(p.Icao);
        Posa = p with { Punti = punti, Modificata = false, DataPosata = togli ? null : p.Carta.Effective, Salvata = !togli };
        Rifiuto = null;
        Registro.Scrivi("carte", togli ? $"tolta la posa di {p.Carta.Page}" : $"salvata la posa di {p.Carta.Page}: {punti.Count} punti");
        VersioneDellaCarta++;
        Avvisa();
        return true;
    }
}
