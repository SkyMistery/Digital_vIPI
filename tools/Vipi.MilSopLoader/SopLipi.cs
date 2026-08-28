using Vipi.Domain;

namespace Vipi.MilSopLoader;

/// <summary>
/// <b>LIPI Udine Rivolto</b>, trascritto da <c>LIPI_SOP_v2305_new.pdf</c> (dieci pagine, il più corto dei
/// quindici).
///
/// <para>
/// ⚠️ <b>La prosa è in italiano, i dati no.</b> Il documento nasce in italiano (carta vSOP militari §1d) e
/// l'inglese lo produce la traduzione. Ma nomi dei punti (NOVEMBER, SIERRA), callsign (PONY, IAM44xx),
/// identificativi di frequenza (<c>LIVK_CRC_CTR</c>), canali TACAN, coordinate e quote <b>si copiano
/// esatti</b>: tradurli sarebbe cambiarli.
/// </para>
///
/// <para>
/// ⚠️ <b>Questa trascrizione è mia, non di un controllore militare.</b> È la prima stesura, e come tutte le
/// prime stesure va riletta da chi ha la competenza — è esattamente il ruolo di chi rivede il documento.
/// Le note dell'originale («GCI positions», «Airborne Early Warning») sono riportate; l'unica cosa che ho
/// corretto è il refuso <i>Worning</i> del PDF.
/// </para>
/// </summary>
public static class SopLipi
{
    public static SopTrascritto Costruisci() => new(
        Icao: "LIPI",
        Fonte: "LIPI_SOP_v2305_new.pdf (V 2305)",
        // ⚠️ Rivolto non è una base di difesa aerea (QRA compare in quattro SOP su quindici, e non in questo)
        // e il suo SOP non ha una sezione di bassa quota. Lasciarle vuote e in vista direbbe al lettore che
        // manca qualcosa: su questo campo non manca niente, quelle procedure non ci sono.
        DaNascondere: new[] { "qra", "lowlevel" },
        SenzaContenuto: new Dictionary<string, string>
        {
            // ⚠️ Sono FIGURE, non testo. La carta §7.3 le mette in conto come lavoro manuale: vanno
            // estratte dai PDF e caricate come immagini, e finché non lo si fa la sezione resta vuota.
            // Dirlo qui è ciò che distingue «non trascritto perché è un disegno» da «dimenticato».
            ["taxiing"] = "due figure: Apron flow e Manoeuvring area flow (pag. 4 e 5). Resta la nota sulla taxiway F.",
            ["arming"] = "tre figure: arming rwy 06, dearming 06/arming 24, dearming 24 (pag. 6).",
            ["vfrjet"] = "le figure dei circuiti e delle porte (pag. 8). Restano la nota sulle quote e la tabella dei punti.",
        },
        Sezioni: new SezioneSop[]
        {
            // ---- 2. Dati generali -----------------------------------------------------------------------
            new("navaids", new[]
            {
                Blocco.Tabella(
                    new[] { "Tipo", "Nome", "Frequenza", "Coordinate" },
                    new[] { "TACAN", "RIV", "CH 37X – 110.00", "N 45 59 44.62 E 13 05 18.20" },
                    new[] { "NDB", "RIV", "371", "N 45 56 07.20 E 12 56 31.24" }),
            }),

            // ⚠️ La SCHEDA delle frequenze la disegna la pagina dalle posizioni IVAO dello scalo. Qui va
            // ciò che il catalogo settori NON ha: CRC, GCI e AEW (carta §2, riga 2.2).
            new("frequencies", new[]
            {
                Blocco.Tabella(
                    new[] { "Ente", "Nominativo", "Frequenza", "Note" },
                    new[] { "LIPI_TWR", "Rivolto Tower", "126.850", "" },
                    new[] { "LIPI_G_APP", "Rivolto Precision", "127.525", "" },
                    new[] { "LIPA_APP", "Aviano Radar", "120.130", "" },
                    new[] { "LIPP_MIL_CTR", "Padova Military", "123.175", "" },
                    new[] { "LIVK_CRC_CTR", "Pioppo", "136.200", "posizione GCI" },
                    new[] { "LIRO_CRC_CTR", "Barca", "136.250", "posizione GCI" },
                    new[] { "LIZZ_AEW_CTR", "Legion", "136.400", "Airborne Early Warning" }),
            }),

            new("diversion", new[]
            {
                Blocco.Tabella(
                    new[] { "Aeroporto", "Radioassistenza", "Rilevamento", "Distanza" },
                    new[] { "LIPA Aviano", "AVI TACAN – CH 111X – 116.40", "272°", "21 NM" },
                    new[] { "LIPS Istrana", "ISA TACAN – CH 80X – 113.30", "243°", "44.7 NM" },
                    new[] { "LIPC Cervia", "CEV TACAN – CH 83X – 113.60", "193°", "111.3 NM" },
                    new[] { "LIPL Ghedi", "GHE TACAN – CH 46X – 110.90", "252°", "123.4 NM" }),
            }),

            // ⚠️ La scheda piste la disegna la pagina dall'anagrafica. Qui vanno le coordinate delle SOGLIE,
            // che `AirportRunway` non ha (carta §2, riga 2.4).
            new("runways", new[]
            {
                Blocco.Prosa("Coordinate delle soglie."),
                Blocco.Tabella(
                    new[] { "Pista", "Coordinate della soglia" },
                    new[] { "06", "N45°58'24.71\" - E13°02'06.23\"" },
                    new[] { "24", "N45°59'07.12\" - E13°03'48.14\"" }),
            }),

            new("callsigns", new[]
            {
                Blocco.Tabella(
                    new[] { "Reparto", "Nominativo OAT", "Nominativo GAT" },
                    new[] { "313° Gruppo Addestramento Acrobatico", "PONY", "IAM44xx" }),
            }),

            // ---- 3. Procedure di terra ------------------------------------------------------------------
            new("parkings", new[]
            {
                Blocco.Tabella(
                    new[] { "Piazzale", "Stand", "Usato da" },
                    new[] { "06S", "S1→S15", "transiti" },
                    new[] { "06N", "N1, N2", "transiti" },
                    new[] { "24N", "posizionamento autonomo", "transiti" },
                    new[] { "24S", "posizionamento autonomo", "transiti" },
                    new[] { "PAN", "1-7 (soft shelter)", "Frecce Tricolori" },
                    new[] { "PAN", "8-19", "cargo a supporto delle Frecce Tricolori" }),
            }),

            new("enginestart", new[]
            {
                Blocco.Prosa(
                    "• Il traffico **IFR** chiede alla TWR la messa in moto e l'autorizzazione IFR.\n" +
                    "• Il traffico **VFR** non ha bisogno dell'autorizzazione alla messa in moto: riporta pronto al rullaggio."),
            }),

            new("taxiing", new[]
            {
                Blocco.Prosa("La via di rullaggio **F** è disponibile ai soli aeromobili a getto in uscita dal piazzale PAN."),
                Blocco.Avvertenza(
                    "I flussi di rullaggio del piazzale e dell'area di manovra sono due figure dell'originale, non ancora riportate qui.",
                    CalloutKind.Info),
            }),

            new("arming", new[]
            {
                Blocco.Avvertenza(
                    "Le posizioni di armamento e disarmo per pista 06 e 24 sono figure dell'originale, non ancora riportate qui.",
                    CalloutKind.Info),
            }),

            // ---- 4. Procedure di volo -------------------------------------------------------------------
            // ⚠️ «NIL» nell'originale vuol dire «nessuna restrizione», e va scritto: una sezione VUOTA si
            // legge come «non lo sappiamo», che è un'altra cosa.
            new("takeoff", new[] { Blocco.Prosa("Nessuna restrizione al decollo.") }),

            new("sfo", new[]
            {
                Blocco.Prosa(
                    "• A **nord** del campo.\n" +
                    "• Comunicare all'ATC l'altitudine o il livello dell'**High Key**."),
            }),

            new("commfail", new[] { Blocco.Prosa("Nessuna procedura particolare.") }),

            new("gca", new[] { Blocco.Prosa("A **sud-est** del campo.") }),

            new("vfrjet", new[]
            {
                Blocco.Prosa(
                    "Rispettare i vincoli di quota indicati nelle figure, salvo diversa istruzione dell'ATC."),
                Blocco.Prosa("**Punti significativi VFR jet**"),
                Blocco.Tabella(
                    new[] { "Punto", "Coordinate", "Riferimento", "Quota" },
                    new[] { "NOVEMBER (porta nord)", "46°05'42\"N - 13°03'33\"E", "R-346/6 NM \"RIV\" TAC", "uscita 1500 ft / ingresso 2000 ft" },
                    new[] { "SIERRA (porta sud)", "45°47'20\"N - 12°47'20\"E", "R-223/18 NM \"RIV\" TAC", "uscita 1500 ft / ingresso 2000 ft" },
                    new[] { "ALPHA (IP pista 06)", "45°57'17\"N - 12°59'22\"E", "R-237/4.8 NM \"RIV\" TAC", "uscita 1500 ft / ingresso 2000 ft" },
                    new[] { "BRAVO (IP pista 24)", "RIV TACAN", "—", "uscita 1500 ft / ingresso 2000 ft" },
                    new[] { "CHARLIE", "45°55'25\"N - 13°08'40\"E", "R-150/5 NM \"RIV\" TAC", "2000 ft" },
                    new[] { "DELTA", "45°52'00\"N - 12°58'27\"E", "R-210/9 NM \"RIV\" TAC", "uscita 1500 ft / ingresso 2000 ft" }),
            }),

            new("ifrsignificant", new[]
            {
                Blocco.Tabella(
                    new[] { "Punto", "Coordinate", "Note" },
                    new[] { "BRAVO", "45 32 48 N - 012 54 31 E", "SID CHI1E/F/G/H" },
                    new[] { "MARTE", "45 51 00 N - 012 39 01.20 E", "ROSKA 1C/1D · IAF TACAN pista 06 · IAF ILS o LOC pista 06" }),
            }),

            new("gat", new[]
            {
                Blocco.Prosa(
                    "Secondo il documento RAD — Airport Connectivity di Eurocontrol, chi opera come **GAT** " +
                    "deve pianificare uno dei punti seguenti come primo o ultimo punto del piano di volo."),
                Blocco.Tabella(
                    new[] { "Partenza", "Arrivo" },
                    new[] { "NAXAV", "NAXAV" },
                    new[] { "CHI", "CHI" },
                    new[] { "RON", "RON" },
                    new[] { "VIC", "VIC" }),
            }),

            // ⚠️ `qra` non si semina con contenuto: nei quindici PDF una sezione QRA/Scramble NON esiste, e
            // Rivolto non è una base di difesa aerea (carta §2). Nasce e si nasconde.

            // ---- 5. Aree di lavoro ----------------------------------------------------------------------
            new("regulated", new[]
            {
                Blocco.Prosa(
                    "L'area acrobatica **PAN** è una zona segregata, riservata in via esclusiva " +
                    "all'addestramento delle Frecce Tricolori.\n" +
                    "I limiti laterali coincidono con quelli dell'ATZ di Rivolto; il limite superiore sale " +
                    "fino a **6000 ft AGL**."),
            }),

            new("operationaltechnique", new[]
            {
                Blocco.Prosa(
                    "Quando l'area è attiva, ogni altro traffico è vietato: fanno eccezione i voli **SAR**, " +
                    "**HEMS** e le **emergenze**."),
            }),

            // ---- 6. Validità ----------------------------------------------------------------------------
            // Il timbro (ciclo, data, chi ha pubblicato) lo scrive il documento alla release: qui va soltanto
            // da dove viene il contenuto.
            new("validity", new[]
            {
                Blocco.Prosa("Trascritto dal SOP **LIPI Rivolto V 2305** della divisione italiana IVAO."),
            }),
        });
}
