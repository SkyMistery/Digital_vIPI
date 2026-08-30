namespace Vipi.Domain.Entities;

// =========================================================================================
//  SPAZI AEREI DELL'AIP — carta docs/feature/2026-08-29-spazi-aerei-dal-kmz.md
//
//  Il catalogo della GEOMETRIA dell'AIP, che non è il catalogo operativo IVAO e non lo
//  sostituisce. ⚠️ Le aree regolamentate (R, P, D) e le altre aree restano di `SpecialArea`,
//  cioè di IVAO: qui arrivano lette dal file ma non utilizzabili, per decisione del
//  committente del 29 agosto 2026. Chi decide che cosa si usa è la lista bianca delle
//  famiglie, e sta in un posto solo (`AirspaceFamilies`, in Vipi.Application).
// =========================================================================================

/// <summary>
/// Un caricamento del file dell'AIP: chi l'ha portato, quando, e <b>il file stesso</b>.
///
/// <para>⚠️ Il KMZ si conserva <b>intero</b> (decisione del committente: sono 1,3 MB). Non è
/// sentimentalismo d'archivio — è l'unico modo di rispondere fra sei mesi alla domanda «da dove viene
/// questo confine», e di rifare la lettura se la regola di lettura cambia senza chiedere di nuovo il
/// file a qualcuno.</para>
///
/// <para>Uno solo è <see cref="IsCurrent"/>: quello da cui si pesca. I precedenti restano, e restano
/// leggibili, perché un documento pubblicato può portare una geometria che veniva da loro.</para>
/// </summary>
public class AirspaceImport
{
    public int Id { get; set; }

    /// <summary>Il nome del file come l'ha chiamato chi l'ha caricato: <c>it (2).kmz</c>.</summary>
    public string FileName { get; set; } = default!;

    /// <summary>Impronta del contenuto. Serve a riconoscere «è lo stesso file di prima».</summary>
    public string Sha256 { get; set; } = default!;

    /// <summary>Il KMZ così com'è arrivato.</summary>
    public byte[] Content { get; set; } = [];

    public long SizeBytes { get; set; }

    /// <summary>
    /// Il ciclo AIRAC <b>dichiarato da chi carica</b> (<c>YYNN</c>), perché il file non lo porta: dice solo
    /// la data in cui è stato generato. ⚠️ Non è un cancello — il file dell'AIP descrive quel che è
    /// <b>già pubblicato</b>, a differenza del sectorfile che scriviamo noi in anticipo — è la risposta a
    /// «di quando è questo confine».
    /// </summary>
    public string? AiracCycle { get; set; }

    /// <summary>Quando il file è stato generato, se il file lo dice (AirspaceConverter lo scrive in testa).</summary>
    public DateTime? GeneratedUtc { get; set; }

    public DateTime UploadedUtc { get; set; }
    public int? UploadedByUserId { get; set; }

    /// <summary>Il nome di chi ha caricato, congelato: l'anagrafica staff cambia, la riga di archivio no.</summary>
    public string? UploadedByName { get; set; }

    /// <summary>Quanti volumi il lettore ha trovato, quanti se ne possono usare, quante chiavi in doppio.</summary>
    public int VolumesRead { get; set; }

    public int VolumesUsable { get; set; }
    public int DuplicateKeys { get; set; }
    public int PointCount { get; set; }

    /// <summary>Le segnalazioni del lettore, come JSON: quel che la pagina rimostra senza rileggere il file.</summary>
    public string? IssuesJson { get; set; }

    /// <summary>Il caricamento <b>in vigore</b>: uno solo, ed è quello da cui si pesca.</summary>
    public bool IsCurrent { get; set; }

    public ICollection<AirspaceVolume> Volumes { get; set; } = new List<AirspaceVolume>();
}

/// <summary>
/// Un volume di spazio aereo: il contorno più tutto quel che il file sa dirne.
///
/// <para><see cref="PolygonJson"/> è <b>nella stessa forma del <c>regionMapPolygon</c> IVAO</b> —
/// <c>[[lng,lat],…]</c>, longitudine prima — e non è un dettaglio di comodo: è la ragione per cui
/// <c>AorPolygonProjector</c>, la mappa Leaflet, il viewer 3D e la stampa disegnano un volume dell'AIP
/// senza sapere che viene dall'AIP.</para>
///
/// <para>⚠️ <b>Un anello per riga.</b> <see cref="RingCount"/> dice quanti ne aveva il volume nel file;
/// se è maggiore di uno, in <see cref="PolygonJson"/> c'è il primo e il caricamento lo <b>segnala</b>.
/// Sul file del 15 luglio 2026 sono uno su tutti e 1 536, e la colonna esiste perché il giorno che non
/// lo saranno più la cosa si veda, invece di perdere metà di un confine in silenzio — che è esattamente
/// il modo in cui <c>PolygonGeometry.ParsePoints</c> tratterebbe un annidamento in più.</para>
/// </summary>
public class AirspaceVolume
{
    public int Id { get; set; }

    public int ImportId { get; set; }
    public AirspaceImport? Import { get; set; }

    /// <summary>L'identità: <c>FAMIGLIA|NOME|BASE|TETTO</c>, composta da <c>AirspaceKmlReader</c>.</summary>
    public string NaturalKey { get; set; } = default!;

    /// <summary>0, o l'ordinale del doppione esatto: nel file ce ne sono tre.</summary>
    public int Ordinal { get; set; }

    public AirspaceFamily Family { get; set; }

    /// <summary>Il nome come lo scrive l'AIP: <c>CATANIA CTR Z1</c>, <c>ATZ CROTONE LIBC</c>.</summary>
    public string Name { get; set; } = default!;

    /// <summary>La categoria alla lettera del file: <c>Control Traffic Region</c>, <c>Airspace class D</c>.</summary>
    public string Category { get; set; } = default!;

    /// <summary>La classe di spazio aereo, quando la categoria ne dichiara una: <c>A</c>…<c>G</c>.</summary>
    public string? AirspaceClass { get; set; }

    public AirspaceDatum BaseDatum { get; set; }

    /// <summary>Piedi confrontabili (per un FL è FL×100); null solo per l'illimitato.</summary>
    public int? BaseFeet { get; set; }

    /// <summary>La quota <b>come la scrive il file</b>: è quel che un documento stampa.</summary>
    public string BaseRaw { get; set; } = default!;

    public AirspaceDatum TopDatum { get; set; }
    public int? TopFeet { get; set; }
    public string TopRaw { get; set; } = default!;

    /// <summary>L'anello in forma IVAO: <c>[[lng,lat],…]</c>, senza il vertice di chiusura.</summary>
    public string PolygonJson { get; set; } = default!;

    /// <summary>Quanti anelli aveva il volume nel file. &gt; 1 = il caricamento l'ha segnalato.</summary>
    public int RingCount { get; set; } = 1;

    public int PointCount { get; set; }

    /// <summary>Il riquadro: filtra per area senza rileggere i punti.</summary>
    public double MinLat { get; set; }

    public double MinLon { get; set; }
    public double MaxLat { get; set; }
    public double MaxLon { get; set; }
}

/// <summary>
/// «Questo settore disegna la sua AoR con questi volumi dell'AIP». La scelta di una persona, non un import.
///
/// <para>⚠️ <b>Cita la CHIAVE NATURALE, non l'id della riga.</b> Un caricamento nuovo crea righe nuove: con
/// l'id ogni aggancio si romperebbe a ogni ri-caricamento del file. Con la chiave l'aggancio sopravvive
/// finché quel volume esiste nel caricamento in vigore — e quando non esiste più il settore torna alla
/// forma di IVAO, e la pagina dice quali agganci sono rimasti scoperti.</para>
///
/// <para>⚠️ <b>L'indirizzo del settore è la coppia catalogo + id</b>, non il callsign: alla sorgente i due
/// cataloghi sono due sequenze che si sovrappongono, e un callsign si può rinominare. Il callsign si
/// conserva accanto perché è come lo si cerca e come lo si mostra, e perché la risoluzione in fase di
/// disegno parte da lì.</para>
///
/// <para>⚠️ <b>Non scrive la shape del settore.</b> Vale sull'AoR — mappa, 3D, stampa — e non sui
/// confinanti, sull'attribuzione del traffico e sulla vLOA, che restano sulla forma di IVAO. Il perché sta
/// nella carta, §6-bis: la colonna della shape tiene UN anello, e Catania sono sette zone.</para>
/// </summary>
public class SectorAirspaceBinding
{
    public int Id { get; set; }

    /// <summary>Da quale catalogo viene il settore: subcenter di un ACC o postazione d'aeroporto.</summary>
    public SourceCatalog Catalog { get; set; }

    public int SectorId { get; set; }

    /// <summary>Il callsign, maiuscolo: come si cerca, come si mostra, e da dove parte il disegno.</summary>
    public string Callsign { get; set; } = default!;

    /// <summary>La chiave naturale del volume: <c>FAMIGLIA|NOME|BASE|TETTO</c>.</summary>
    public string VolumeKey { get; set; } = default!;

    /// <summary>L'ordinale del volume dentro la sua chiave (0, salvo i doppioni esatti del file).</summary>
    public int VolumeOrdinal { get; set; }

    /// <summary>L'ordine in cui i volumi sono stati scelti: è l'ordine in cui si disegnano.</summary>
    public int Position { get; set; }

    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }

    /// <summary>Chi ha scelto, congelato: la pagina deve poterlo dire anche fra un anno.</summary>
    public string? CreatedByName { get; set; }
}
