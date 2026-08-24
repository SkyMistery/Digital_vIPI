namespace Vipi.Application.Content;

/// <summary>Template (default globale) della frase di coordinamento, caricato da file di progetto.
/// L'implementazione vive fuori da Application (Host/Infrastructure) e può ricaricarsi a caldo.</summary>
public interface ICoordinationSentenceTemplate
{
    /// <summary>Il template corrente (mai null: se il file manca, ritorna il default hardcoded).</summary>
    CoordinationSentenceTemplate Current { get; }
}

/// <summary>Testi del template della frase di coordinamento. Tutti i campi hanno un default sensato,
/// così un file parziale resta valido. Placeholder supportati:
/// frase → {owner} {target} {airport} {stato} {fl} {point}; target → {name} {code}; airport → {name} {icao}.
/// <br/>Il placeholder <c>{airport}</c> include già la <b>relazione</b> (arrivo/partenza): per gli arrivi usa
/// <see cref="AirportArrival"/> («con destinazione …»), per le partenze <see cref="AirportDeparture"/> («in partenza da …»).</summary>
public sealed class CoordinationSentenceTemplate
{
    // {fl} include già l'eventuale «per …» (composto dal service) così, se assente, non resta un «per» orfano.
    // {airport} include già la relazione arrivo/partenza (vedi AirportArrival/AirportDeparture): NON aggiungere qui
    // «con destinazione», sennò per le partenze la frase risulterebbe errata.
    public string Template { get; init; } =
        "{owner} trasferisce a {target} il traffico {airport} {stato} {fl} su {point}.";

    /// <summary>
    /// Variante della frase per gli accordi in cui l'AUTORIZZAZIONE e il TRASFERIMENTO sono due eventi distinti
    /// (tipicamente ACC→APP): «autorizza … via {point} {fl} e lo trasferisce a {target} {handoff} {handoffLevel}».
    /// Scelta al posto di <see cref="Template"/> quando il punto porta una faccetta trasferimento; senza, non si
    /// usa mai e le righe storiche restano parola per parola quelle di prima.
    /// <para>Placeholder aggiuntivi: <c>{handoff}</c> (dove passa il controllo) e <c>{handoffLevel}</c> (a che
    /// livello ci si arriva). È un template a sé e non una coda appesa perché cambia il VERBO della principale,
    /// e il verbo non si può appendere.</para>
    /// </summary>
    public string TemplateCleared { get; init; } =
        "{owner} autorizza il traffico {airport} via {point} {fl} e lo trasferisce a {target} {handoff} {handoffLevel} {stato}.";

    // ---- Il verso ENTRANTE (24 agosto 2026) ----
    //
    // Un accordo si scrive UNA volta sola, dal lato di chi cede; il documento di chi riceve mostrava quelle
    // stesse parole, cioè leggeva come il documento dell'altro («Zagreb Radar trasferisce a Brindisi Radar CS0
    // il traffico…» dentro la vIPI di Brindisi).
    //
    // ⚠️ Gli SLOT NON CAMBIANO DI SIGNIFICATO: {owner} resta chi cede, {target} resta chi riceve. Qui cambia
    // solo l'ORDINE DELLE PAROLE. Scambiare gli argomenti al chiamante avrebbe cambiato in silenzio la regola
    // dei codici di posizione, che fra i due slot è asimmetrica (OmitTargetCode, in BuildData).
    //
    // La coda — aeroporto, stato, livello, punto, e poi condizione/velocità/comunicazioni appese dal composer —
    // è VERBATIM quella del verso uscente: cambia la testa della frase, non il modo di dire l'accordo.

    /// <summary>Frase distesa quando la riga ENTRA nell'ente del documento. Stessi placeholder di
    /// <see cref="Template"/>, testa rovesciata: «{target} riceve da {owner} …».</summary>
    public string TemplateReceive { get; init; } =
        "{target} riceve da {owner} il traffico {airport} {stato} {fl} su {point}.";

    /// <summary>Capofila del verso entrante: gemella di <see cref="TemplateLead"/>, senza livello né punto.</summary>
    public string TemplateLeadReceive { get; init; } =
        "{target} riceve da {owner} il traffico {airport} secondo la tabella seguente:";

    /// <summary>
    /// Verso entrante con la faccetta trasferimento (autorizzazione e trasferimento sono due eventi): gemella
    /// di <see cref="TemplateCleared"/>. Chi riceve non autorizza — subisce l'autorizzazione dell'altro — quindi
    /// il participio («autorizzato … trasferito …») e non il verbo attivo.
    /// </summary>
    public string TemplateClearedReceive { get; init; } =
        "{target} riceve da {owner} il traffico {airport} autorizzato via {point} {fl}, trasferito {handoff} {handoffLevel} {stato}.";

    public string TargetWithCode { get; init; } = "{name} {code}";
    public string TargetNoCode { get; init; } = "{name}";
    /// <summary>Aeroporto neutro (senza relazione): fallback per flussi non arrivo/partenza. Placeholder {name} {icao}.</summary>
    public string Airport { get; init; } = "{name} {icao}";
    /// <summary>Relazione aeroporto per gli ARRIVI (il traffico è destinato all'aeroporto). Placeholder {name} {icao}.</summary>
    public string AirportArrival { get; init; } = "con destinazione {name} {icao}";
    /// <summary>Relazione aeroporto per le PARTENZE (il traffico proviene dall'aeroporto). Placeholder {name} {icao}.</summary>
    public string AirportDeparture { get; init; } = "in partenza da {name} {icao}";
    public CoordinationSentenceState Stato { get; init; } = new();
    /// <summary>Fraseologia del livello ({fl}): parole/format-string usati dal composer, così è lingua-neutro.</summary>
    public CoordinationSentenceLevel Level { get; init; } = new();
    /// <summary>Clausola condizione (pista in uso / area attiva), appesa a fine frase quando il punto ha una condizione.
    /// Placeholder {label}. Vuota quando la condizione è None.</summary>
    public CoordinationSentenceCondition Condition { get; init; } = new();
    /// <summary>Fraseologia del trasferimento: dove passa il controllo, a che livello, e dove passano le
    /// comunicazioni quando non è lo stesso posto. Usata solo con <see cref="TemplateCleared"/>.</summary>
    public CoordinationSentenceHandoff Handoff { get; init; } = new();
    /// <summary>Fraseologia della restrizione di velocità al trasferimento. Placeholder {v}.</summary>
    public CoordinationSentenceSpeed Speed { get; init; } = new();
    /// <summary>Premesso alla condizione di una riga che SCAVALCA le alternative del gruppo («in ogni caso, di
    /// notte …»): senza, il lettore la scambierebbe per un'alternativa in più.</summary>
    public string GroupWide { get; init; } = "in ogni caso";
    /// <summary>Reso quando il CoP è VUOTO (non compilato): distinto da «ALL». Il default globale può renderlo «—».</summary>
    public string FallbackMissingPoint { get; init; } = "tutti i punti";
    /// <summary>Reso quando il CoP è «ALL»: istruzione esplicita «tutti i punti di consegna».</summary>
    public string FallbackAllPoints { get; init; } = "tutti i punti";
    /// <summary>Reso quando il CoP è «ALL to X»: tutti i punti verso una nazione/FIR. Placeholder {dest}.</summary>
    public string FallbackAllToward { get; init; } = "tutti i punti verso {dest}";

    /// <summary>
    /// La frase CAPOFILA: una sola, che introduce l'intera tabella invece di descriverne una riga. È la forma dei
    /// documenti veri — «TS EXE trasferisce a US1 EXE il traffico secondo la seguente tabella:» — e serve alle
    /// sezioni dove le righe sono tante e la prosa, ripetuta per ognuna, si legge due volte per scoprire che
    /// dice la stessa cosa.
    /// <para>Placeholder: <c>{owner}</c>, <c>{target}</c>, <c>{airport}</c>. Non porta livello né punto,
    /// <b>apposta</b>: quelli sono ciò che la tabella dice riga per riga, e ripeterli qui vorrebbe dire scegliere
    /// quale riga è più importante delle altre.</para>
    /// </summary>
    public string TemplateLead { get; init; } =
        "{owner} trasferisce a {target} il traffico {airport} secondo la tabella seguente:";

    public static CoordinationSentenceTemplate Default { get; } = new();

    /// <summary>Template inglese (usato dalle vLOA, documenti bilaterali in EN). Stessi placeholder, testi in inglese.</summary>
    public static CoordinationSentenceTemplate English { get; } = new()
    {
        Template = "{owner} transfers to {target} the traffic {airport} {stato} {fl} over {point}.",
        TemplateLead = "{owner} transfers to {target} the traffic {airport} as per the table below:",
        // Il verso entrante non lo usa nessuna vLOA di oggi (due alberi separati, entrambi resi dalla parte di
        // chi cede) — ma il template inglese è UNO SOLO, e lasciarlo monco vorrebbe dire una frase italiana
        // dentro un documento bilaterale il giorno in cui una vLOA userà il verso entrante.
        TemplateReceive = "{target} receives from {owner} the traffic {airport} {stato} {fl} over {point}.",
        TemplateLeadReceive = "{target} receives from {owner} the traffic {airport} as per the table below:",
        TemplateClearedReceive =
            "{target} receives from {owner} the traffic {airport} cleared via {point} {fl}, transferred {handoff} {handoffLevel} {stato}.",
        AirportArrival = "inbound to {name} {icao}",
        AirportDeparture = "departing from {name} {icao}",
        Stato = new CoordinationSentenceState
        {
            Descending = "descending",
            Climbing = "climbing",
            Level = "level",
        },
        Level = new CoordinationSentenceLevel
        {
            FlBody = "at level {v}",
            FtBody = "at {v} ft",
            OrBelow = "or below",
            OrAbove = "or above",
            // La parità in inglese va fra parentesi dopo il livello e come aggettivo prima del sostantivo:
            // «at level 260 (even)», «for an odd level». Ricalcare l'ordine italiano dava «at level 260 even»
            // e «for a level odd».
            WithParity = "{body} ({parity})",
            ForLevelParity = "for an {parity} level",
            ParityEven = "even",
            ParityOdd = "odd",
        },
        Condition = new CoordinationSentenceCondition
        {
            Runway = "with runway {label} in use",
            Area = "with {label} active",
            RunwayAndArea = "with runway {runway} in use and {area} active",
            Custom = "under condition {label}",
            Join = "and",
        },
        Handoff = new CoordinationSentenceHandoff
        {
            Point = "over {label}",
            AorBoundary = "at the AoR boundary",
            Custom = "{label}",
            LevelPassing = "passing {v}",
            LevelAtOrBelow = "at {v} or below",
            LevelAtOrAbove = "at {v} or above",
            Comms = "communications {handoff}",
        },
        Speed = new CoordinationSentenceSpeed
        {
            AtOrBelow = "at {v} kt or less",
            AtOrAbove = "at {v} kt or more",
            Exact = "at {v} kt",
        },
        GroupWide = "in any case",
        TemplateCleared =
            "{owner} clears the traffic {airport} via {point} {fl} and transfers it to {target} {handoff} {handoffLevel} {stato}.",
        FallbackMissingPoint = "—",
        FallbackAllPoints = "all points",
        FallbackAllToward = "all points toward {dest}",
    };
}

/// <summary>Fraseologia del TRASFERIMENTO (accordi ACC→APP): dove passa il controllo, a che livello, e dove
/// passano le comunicazioni quando avviene altrove. Lingua-specifica come il resto del template.</summary>
public sealed class CoordinationSentenceHandoff
{
    /// <summary>Trasferimento su un punto/fix, placeholder {label}: «su {label}».</summary>
    public string Point { get; init; } = "su {label}";
    /// <summary>Trasferimento al confine dell'area di responsabilità: si descrive da sé, nessun placeholder.</summary>
    public string AorBoundary { get; init; } = "al confine dell'AoR";
    /// <summary>Trasferimento descritto a parole («20 NM da AVN»), placeholder {label}: il testo così com'è.</summary>
    public string Custom { get; init; } = "{label}";

    /// <summary>Livello al trasferimento con vincolo esatto — la forma di riferimento. Placeholder {v} = «FL110».
    /// <para>«Passando» e non «a»: al trasferimento il traffico ATTRAVERSA quel livello, non ci si stabilizza.
    /// È la differenza che rende la faccetta utile e la ragione per cui non si riusa la fraseologia del
    /// livello autorizzato.</para></summary>
    public string LevelPassing { get; init; } = "passando {v}";
    /// <summary>Livello al trasferimento con vincolo ≤, placeholder {v}.</summary>
    public string LevelAtOrBelow { get; init; } = "a {v} o inferiore";
    /// <summary>Livello al trasferimento con vincolo ≥, placeholder {v}.</summary>
    public string LevelAtOrAbove { get; init; } = "a {v} o superiore";

    /// <summary>Clausola del passaggio comunicazioni, quando avviene altrove rispetto al controllo.
    /// Placeholder {handoff} = una delle forme qui sopra.</summary>
    public string Comms { get; init; } = "comunicazioni {handoff}";
}

/// <summary>Fraseologia della restrizione di velocità al trasferimento. Placeholder {v} = il valore in nodi.</summary>
public sealed class CoordinationSentenceSpeed
{
    public string AtOrBelow { get; init; } = "a {v} kt o inferiore";
    public string AtOrAbove { get; init; } = "a {v} kt o superiore";
    public string Exact { get; init; } = "a {v} kt";
}

/// <summary>Parola per lo stato verticale del traffico, scelto a mano sul punto (<see cref="Vipi.Domain.TransferVerticalState"/>).
/// Indipendente dal vincolo di livello. <c>Unspecified</c> non ha parola (frase senza stato).</summary>
public sealed class CoordinationSentenceState
{
    public string Descending { get; init; } = "in discesa";
    public string Climbing { get; init; } = "in salita";
    public string Level { get; init; } = "stabile";
}

/// <summary>Fraseologia del livello nella frase di coordinamento ({fl}). Testi lingua-specifici estratti dal composer
/// così che il template inglese (vLOA) possa renderli in EN. <see cref="FlBody"/>/<see cref="FtBody"/> usano il
/// placeholder {v} per il valore; <see cref="ForLevelParity"/> è la frase intera quando manca un valore
/// numerico ma c'è la parità.</summary>
public sealed class CoordinationSentenceLevel
{
    /// <summary>Corpo con unità FL, placeholder {v}: «a livello {v}».</summary>
    public string FlBody { get; init; } = "a livello {v}";
    /// <summary>Corpo con unità piedi, placeholder {v}: «a {v} ft».</summary>
    public string FtBody { get; init; } = "a {v} ft";
    /// <summary>Suffisso vincolo ≤ (AtOrBelow): «o livello inferiore».</summary>
    public string OrBelow { get; init; } = "o livello inferiore";
    /// <summary>Suffisso vincolo ≥ (AtOrAbove): «o livello superiore».</summary>
    public string OrAbove { get; init; } = "o livello superiore";
    /// <summary>
    /// Come la parità si attacca a un livello con valore. Placeholder <c>{body}</c> (es. «a livello 260»)
    /// e <c>{parity}</c>.
    ///
    /// <para><b>È un pattern e non una concatenazione perché l'ordine delle parole è lingua-specifico.</b>
    /// In italiano l'aggettivo segue («a livello 260 pari»); in inglese la stessa forma dava
    /// «at level 260 even», che nessuno scriverebbe. Scoperto leggendo una vLOA resa, non dai test:
    /// il compositore era corretto, era la lingua a non entrarci.</para>
    /// </summary>
    public string WithParity { get; init; } = "{body} {parity}";

    /// <summary>
    /// Frase completa quando c'è la parità ma <b>nessun valore</b> numerico. Placeholder <c>{parity}</c>:
    /// «per un livello dispari» in italiano, «for an odd level» in inglese — di nuovo, ordine diverso.
    /// </summary>
    public string ForLevelParity { get; init; } = "per un livello {parity}";

    public string ParityEven { get; init; } = "pari";
    public string ParityOdd { get; init; } = "dispari";
}

/// <summary>Clausola condizione operativa appesa a fine frase (pista in uso / area attiva / condizione libera).
/// Placeholder {label}. Lingua-specifica come il resto del template (IT default, EN nelle vLOA).</summary>
public sealed class CoordinationSentenceCondition
{
    /// <summary>Condizione di pista in uso, placeholder {label}: «con pista {label} in uso».</summary>
    public string Runway { get; init; } = "con pista {label} in uso";
    /// <summary>Condizione di area attiva, placeholder {label}: «con {label} attiva».</summary>
    public string Area { get; init; } = "con {label} attiva";
    /// <summary>Condizione combinata pista + area in AND, placeholder {runway}/{area}: «con pista {runway} in uso e {area} attiva».</summary>
    public string RunwayAndArea { get; init; } = "con pista {runway} in uso e {area} attiva";
    /// <summary>Condizione libera, placeholder {label}: «in condizione {label}».</summary>
    public string Custom { get; init; } = "in condizione {label}";
    /// <summary>Congiunzione tra clausole condizione presenti (pista/area/personalizzata): «e» (EN «and»).</summary>
    public string Join { get; init; } = "e";
}
