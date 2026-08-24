using Vipi.Domain;

namespace Vipi.Application.Content;

// Modelli del profilo dell'APP non remotizzato (standalone). Parti editoriali (salvate) + derivate (live).
// Mirror, in chiave APP, di AirportModels.

/// <summary>Riga della sezione Separazioni (editoriale): colonne fisse verticale + laterale (es. «1000 ft» / «3 NM»);
/// dalla 2ª riga in poi un free-text <see cref="Applicability"/> specifica quando si applica (la 1ª è la predefinita).</summary>
public sealed record AppSeparationRow(string Vertical, string Lateral, string? Applicability = null);

/// <summary>Override d'ordine di una singola frequenza, per callsign (FreqOrderJson).</summary>
public sealed record AppFreqOrderOverride(string Callsign, int Order);

/// <summary>
/// Riga della sezione Frequenze (DERIVATA): dal sottoalbero (settori + ATIS dal catalogo) o da un link extra.
/// <see cref="IsPrimary"/> = frequenza principale dell'APP (★). <see cref="IsLink"/> = frequenza linkata.
/// </summary>
public sealed record AppFreqRow(
    int? SourceSectorId, string Name, string Callsign, string FrequencyMhz,
    string Position, bool IsPrimary, bool IsLink);

/// <summary>Riga di un gruppo di coordinamento (DERIVATA dai trasferimenti del settore APP).
/// I membri init opzionali portano il contesto per comporre la frase di coordinamento (una per riga CoP).</summary>
public sealed record AppCoordRow(string Cop, string Level, string Next, TransferFlowKind Kind)
{
    /// <summary>Callsign del settore che cede il traffico (mittente).</summary>
    public string? OwnerCallsign { get; init; }
    /// <summary>ICAO dell'aeroporto destinazione del flusso (null = nessuna frase).</summary>
    public string? AirportIcao { get; init; }
    /// <summary>Vincolo di livello: guida lo stato «in discesa/stabile/in salita».</summary>
    public LevelConstraint? Constraint { get; init; }
    /// <summary>Frase di coordinamento già composta (null/vuota = non mostrata).</summary>
    public string? Sentence { get; init; }
    /// <summary>Etichetta condizione operativa (pista in uso / area attiva); null/vuota = riga sempre valida.</summary>
    public string? ConditionLabel { get; init; }

    // ---- Faccetta trasferimento: colonne che compaiono SOLO se qualche riga le compila ----
    // Testo già reso, non l'enum: la parola del luogo («al confine dell'AoR») è lingua, e la lingua vive nel
    // template — che la vLOA ha in inglese. Una vista che traducesse da sé rifarebbe quel lavoro in italiano.
    /// <summary>Dove passa il controllo, già a parole; vuoto = coincide con l'ingresso (riga «come prima»).</summary>
    public string? Handoff { get; init; }
    /// <summary>Livello al trasferimento già formattato («FL110»); vuoto = la riga non lo porta.</summary>
    public string? HandoffLevel { get; init; }
    /// <summary>Dove passano le comunicazioni, se altrove rispetto al controllo; vuoto altrimenti.</summary>
    public string? CommsHandoff { get; init; }
    /// <summary>Restrizione di velocità già formattata («≤250 kt»); vuota se assente.</summary>
    public string? Speed { get; init; }

    // ---- Varianti ----
    /// <summary>Flusso di provenienza. Serve a distinguere i gruppi di varianti: il numero di gruppo è
    /// progressivo <b>per flusso</b>, e una tabella raccoglie righe di più flussi — senza questo, due gruppi «1»
    /// di flussi diversi si fonderebbero in uno.</summary>
    public int FlowId { get; init; }
    /// <summary>Identità del gruppo di varianti dentro il flusso (null = riga singola). Le righe con lo stesso
    /// <see cref="FlowId"/> e lo stesso gruppo appartengono allo stesso accordo e vanno rese insieme.</summary>
    public int? VariantGroup { get; init; }
    /// <summary>Rientro nell'outline del gruppo: 0 = alternativa pari-grado, 1 = sua eccezione, 2 = eccezione
    /// dell'eccezione. Guida il rientro della colonna condizione.</summary>
    public int VariantDepth { get; init; }
    /// <summary>La riga scavalca le alternative del gruppo: resa in fondo, senza rientro.</summary>
    public bool IsGroupWide { get; init; }

    // ---- provenienza (additivo, 16 agosto 2026) ----
    // La riga viene da una CLAUSOLA di un accordo, che puo' valere su piu' punti e su piu' aeroporti.
    // L'espansione apre quell'elenco in tante righe perche' e' la lettura giusta per il matcher e per la vista
    // live; la tabella del documento lo richiude, e per farlo deve sapere da dove ogni riga viene.
    //
    // ⚠️ Additivo e basta: AccCoordination/AppCoordination finiscono serializzate dentro le release congelate
    // (AccFrozenSectionProvider & co.), quindi rinominare o cambiare tipo a un campo esistente renderebbe
    // illeggibili le release gia' pubblicate.

    /// <summary>La clausola da cui la riga e' proiettata; <c>null</c> se la riga non viene da un accordo — cioe'
    /// se arriva da una release congelata prima del 16 agosto 2026.</summary>
    public int? ClauseId { get; init; }

    /// <summary>L'elenco completo dei punti della clausola («EKMUR, PISIP»).</summary>
    public string? Points { get; init; }

    /// <summary>Gli aeroporti dell'accordo quando sono <b>piu' d'uno</b>: dice che questa riga non riguarda solo
    /// lo scalo sotto cui la si sta leggendo.</summary>
    public string? AgreementAirports { get; init; }

    /// <summary>La frase CAPOFILA della tabella, gia' composta: «X trasferisce a Y il traffico Z secondo la
    /// tabella seguente:». Arriva composta perche' la lingua vive nel template — che esiste anche in inglese per
    /// le vLOA — e una vista che la mettesse insieme da se' rifarebbe quel lavoro in italiano.</summary>
    public string? LeadSentence { get; init; }

    /// <summary>
    /// La riga è ciò che <b>entra</b> nell'ente del documento: <see cref="Next"/> è chi <b>consegna</b>, non chi
    /// riceve, e la frase è dalla parte di chi riceve («X riceve da Y…»).
    /// <para>Sta sulla RIGA e non sulla tabella perché le tabelle sono <b>miste</b>: l'albero dei coordinamenti
    /// raggruppa per settore → ACC → aeroporto/tipo, e la direzione non è una chiave di raggruppamento. Il
    /// nodo «Sorvoli · Zagabria» del blocco LIBB, misurato, porta 8 righe entranti e 6 uscenti.</para>
    /// <para><c>false</c> = riga uscente: il comportamento storico, ed è anche il valore che una release
    /// congelata <b>prima del 24 agosto 2026</b> assume quando la si deserializza — quei documenti restano
    /// identici a com'erano stati pubblicati.</para>
    /// </summary>
    public bool IsIncoming { get; init; }
}

/// <summary>Gruppo di coordinamenti: la chiave è un callsign ente (ACC/torre) o un'etichetta di tipo (sorvoli).</summary>
public sealed record AppCoordGroup(string TargetCallsign, IReadOnlyList<AppCoordRow> Rows);

/// <summary>Coordinamenti derivati di un APP, per controparte: verso gli ACC, verso le torri, verso gli altri
/// APP (TMA confinanti), più i flussi senza aeroporto (<see cref="Overflights"/>: sorvoli/VFR/altro, per
/// etichetta di tipo). Arrivi e partenze insieme in ogni gruppo: la sezione estesa porta tutto ciò che entra
/// o esce dall'ente.</summary>
public sealed class AppCoordination
{
    public required IReadOnlyList<AppCoordGroup> TowardAcc { get; init; }
    public required IReadOnlyList<AppCoordGroup> TowardTowers { get; init; }
    /// <summary>Coordinamenti con un altro APP (TMA confinanti). Prima non avevano un gruppo dove finire e
    /// cadevano fuori dal documento in silenzio.</summary>
    public IReadOnlyList<AppCoordGroup> TowardApps { get; init; } = Array.Empty<AppCoordGroup>();
    public IReadOnlyList<AppCoordGroup> Overflights { get; init; } = Array.Empty<AppCoordGroup>();

    public static AppCoordination Empty { get; } =
        new() { TowardAcc = Array.Empty<AppCoordGroup>(), TowardTowers = Array.Empty<AppCoordGroup>() };
}

/// <summary>Poligono AoR: punti reali [lat,lon] (per overlay su mappa) + path SVG di fallback + bounding box/centro. null = nessuna shape.</summary>
public sealed record AppAorPolygon(
    string ViewBox, string Path, IReadOnlyList<double[]> Points,
    double MinLat, double MinLon, double MaxLat, double MaxLon, double CenterLat, double CenterLon);

/// <summary>Riga della tabella VFR (trasferimento VFR APP↔torre): situazione → procedura.</summary>
public sealed record AppVfrRow(string Situation, string Procedure);

/// <summary>Contenuto editoriale della sezione VFR: prosa introduttiva (markdown) + tabella opzionale.</summary>
public sealed record AppVfrContent(string? Intro, IReadOnlyList<AppVfrRow> Rows)
{
    public static AppVfrContent Empty { get; } = new(null, Array.Empty<AppVfrRow>());
}

