using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I <b>dati</b> citati nel testo (carta <c>2026-09-20-riferimenti-ai-dati.md</c>): frequenze e nominativi, che
/// cambiano sotto una chiave stabile. La differenza con le procedure sta tutta qui: niente «ultimo valore
/// visto» nel gettone, e il ripiego è <b>la chiave</b>.
/// </summary>
public class RiferimentiDatoTests
{
    /// <summary>I valori dati a mano: le famiglie che compaiono si considerano GUARDATE, che è quel che fa il
    /// risolutore quando una sorgente risponde.</summary>
    private static ValoriDato Valori(params (TipoDato, string, string)[] voci) =>
        new(voci, voci.Select(v => v.Item1).Distinct());

    [Fact]
    public void Il_Gettone_Si_Scrive_Col_Tipo_E_La_Chiave()
    {
        Assert.Equal("[[FREQ LIRF_TWR]]", RiferimentiDato.Scrivi(TipoDato.Frequenza, "lirf_twr"));
        Assert.Equal("[[ATC LIRR_CTR]]", RiferimentiDato.Scrivi(TipoDato.Nominativo, "LIRR_CTR"));
    }

    [Fact]
    public void Esce_Il_Valore_Di_Oggi()
    {
        var valori = Valori((TipoDato.Frequenza, "LIRF_TWR", "118.700"),
                            (TipoDato.Nominativo, "LIRR_CTR", "Roma Radar"));

        Assert.Equal("Contatta Roma Radar su 118.700.",
            RiferimentiDato.Sostituisci("Contatta [[ATC LIRR_CTR]] su [[FREQ LIRF_TWR]].", valori));
    }

    /// <summary>
    /// 🔴 Il ripiego è la CHIAVE, non un valore vecchio: `LIRF_TWR` dice sempre qualcosa di vero, e un
    /// riferimento non esce mai grezzo — nemmeno dove nessuno ha risolto niente.
    /// </summary>
    [Fact]
    public void Quel_Che_Non_Si_Trova_Esce_Con_La_Sua_Chiave()
    {
        Assert.Equal("Contatta LIRF_TWR.", RiferimentiDato.Sostituisci("Contatta [[FREQ LIRF_TWR]].", null));
        Assert.Equal("Contatta LIRF_TWR.",
            RiferimentiDato.Sostituisci("Contatta [[FREQ LIRF_TWR]].", Valori((TipoDato.Frequenza, "LIML_TWR", "118.100"))));
    }

    [Fact]
    public void Le_Piste_Hanno_La_Chiave_Di_Due_Pezzi()
    {
        var citati = RiferimentiDato.Citati(new[] { "Pista [[RWY LIRF 16L]] e punto [[FIX OST]]." });

        Assert.Contains((TipoDato.Pista, "LIRF 16L"), citati);
        Assert.Contains((TipoDato.Punto, "OST"), citati);
        // Esce il pezzo che si legge: la soglia, non «LIRF 16L» — lo scalo è il contesto della frase.
        Assert.Equal("Pista 16L e punto OST.",
            RiferimentiDato.Sostituisci("Pista [[RWY LIRF 16L]] e punto [[FIX OST]].", null));
    }

    [Fact]
    public void Un_Testo_Senza_Riferimenti_Non_Si_Tocca()
    {
        Assert.False(RiferimentiDato.Contiene("Nessun riferimento qui."));
        Assert.False(RiferimentiDato.Contiene("[[SID LIRF OST1E]]"));   // è dell'altra famiglia
        Assert.Empty(RiferimentiDato.Citati(new[] { "niente", null }));
    }

    [Fact]
    public void L_Avviso_Elenca_Solo_Quel_Che_Non_Si_Trova()
    {
        var valori = Valori((TipoDato.Frequenza, "LIRF_TWR", "118.700"));

        var daRivedere = ControlloDatiCitati.Controlla(new[]
        {
            ("Torre", (string?)"[[FREQ LIRF_TWR]] e [[FREQ LIRF_APP]]"),
            ("Note", (string?)"ancora [[FREQ LIRF_APP]]"),
        }, valori);

        var d = Assert.Single(daRivedere);
        Assert.Equal(TipoDato.Frequenza, d.Tipo);
        Assert.Equal("FREQ", d.Parola);
        Assert.Equal("LIRF_APP", d.Chiave);
        Assert.Equal(new[] { "Torre", "Note" }, d.Dove);
    }

    /// <summary>
    /// ⚠️ Senza sorgente guardata non si segnala niente: `ValoriDato.Vuoto` non ha guardato nessuna famiglia,
    /// e «non lo so» non è «non c'è».
    /// </summary>
    [Fact]
    public void Piste_E_Punti_Non_Finiscono_Nell_Avviso()
    {
        var daRivedere = ControlloDatiCitati.Controlla(new[]
        {
            ("Piste", (string?)"[[RWY LIRF 16L]] e [[FIX OST]]"),
        }, ValoriDato.Vuoto);

        Assert.Empty(daRivedere);
    }

    // --- la porta sola ---------------------------------------------------------------------------------

    [Fact]
    public void Le_Due_Famiglie_Si_Sostituiscono_Nello_Stesso_Testo()
    {
        var risolti = new RiferimentiRisolti(
            new NomiProcedura(new Dictionary<(ProcedureKind, string), AirportSidView>
            {
                [(ProcedureKind.Sid, "LIRF")] = new(new[] { new AirportSidRowView("16L", "—", "OST2E", "—", "—", "—", "—", "—", "—") }),
            }),
            Valori((TipoDato.Frequenza, "LIRF_TWR", "118.700")));

        Assert.Equal("OST2E poi 118.700",
            Riferimenti.Sostituisci("[[SID LIRF OST1E]] poi [[FREQ LIRF_TWR]]", risolti));
        Assert.True(Riferimenti.Contiene("[[FREQ LIRF_TWR]]"));
        Assert.True(Riferimenti.Contiene("[[STAR LIRF ELKA3A]]"));
        Assert.False(Riferimenti.Contiene("niente"));
    }

    // --- il risolutore ---------------------------------------------------------------------------------

    private sealed class Catalogo : IFrequenzeDegliEnti
    {
        public int Chiamate { get; private set; }

        public int ChiamateNominativi { get; private set; }
        public int ChiamatePiste { get; private set; }
        public int ChiamatePunti { get; private set; }

        /// <summary>⚠️ Un ente SENZA frequenza: il suo nominativo esiste lo stesso, e si deve poter citare.</summary>
        public IReadOnlyList<EnteRow> Enti { get; set; } = new[]
        {
            new EnteRow(1, "LIRF", "LIRF_TWR", "Fiumicino Tower"),
            new EnteRow(2, null, "LIRR_CTR", null),
            new EnteRow(3, "LIRF", "LIRF_DEL", "Fiumicino Delivery"),
        };

        /// <summary>Le soglie di LIRF: la 16L c'è, la 17L no — è il caso della deriva magnetica.</summary>
        public IReadOnlyList<string> Piste { get; set; } = new[] { "16L", "16R" };
        public IReadOnlySet<string> Punti { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "OST", "ELKAP" };

        public Task<IReadOnlyList<LinkableFrequencyRow>> TutteAsync(CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult<IReadOnlyList<LinkableFrequencyRow>>(new[]
            {
                new LinkableFrequencyRow(1, "LIRF", "LIRF_TWR", "118.700", "Fiumicino Tower"),
                new LinkableFrequencyRow(2, null, "LIRR_CTR", "124.850", null),
            });
        }

        public Task<IReadOnlyList<EnteRow>> NominativiAsync(CancellationToken ct = default)
        {
            ChiamateNominativi++;
            return Task.FromResult(Enti);
        }

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> PisteAsync(
            IReadOnlyCollection<string> icaos, CancellationToken ct = default)
        {
            ChiamatePiste++;
            var d = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in icaos) if (string.Equals(i, "LIRF", StringComparison.OrdinalIgnoreCase)) d[i] = Piste;
            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(d);
        }

        public Task<IReadOnlySet<string>> PuntiAsync(CancellationToken ct = default)
        {
            ChiamatePunti++;
            return Task.FromResult(Punti);
        }

        public int ChiamateAree { get; private set; }

        /// <summary>Due aree del catalogo IVAO, col nome come lo scrive l'import.</summary>
        public IReadOnlyList<SpecialAreaPick> Aree { get; set; } = new[]
        {
            new SpecialAreaPick("1242", "LI R49B - Zita", "R", null, null, new[] { "LIRR" }),
            new SpecialAreaPick("1416", "LI TRA613", "TRA", null, null, new[] { "LIBB" }),
        };

        public Task<IReadOnlyList<SpecialAreaPick>> AreeAsync(CancellationToken ct = default)
        {
            ChiamateAree++;
            return Task.FromResult(Aree);
        }
    }

    private sealed class NienteProcedure : IProcedureReferenceResolver
    {
        public Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
            string? proprioIcao = null, AirportSidView? propriaTabella = null,
            AirportSidView? propriaTabellaStar = null, CancellationToken ct = default) =>
            Task.FromResult(NomiProcedura.Vuoto);

        public Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
            Task.FromResult(NomiProcedura.Vuoto);

        public Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao, ProcedureKind kind = ProcedureKind.Sid,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProceduraCitabile>>(Array.Empty<ProceduraCitabile>());
    }

    [Fact]
    public async Task Il_Risolutore_Chiede_Il_Catalogo_Una_Volta_Sola()
    {
        var catalogo = new Catalogo();
        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo)
            .PerTestiAsync(new[] { "[[FREQ LIRF_TWR]] con [[ATC LIRR_CTR]] e ancora [[FREQ LIRR_CTR]]" });

        Assert.Equal(1, catalogo.Chiamate);
        Assert.Equal("118.700", risolti.Dati.Valore(TipoDato.Frequenza, "LIRF_TWR"));
        Assert.Equal("Fiumicino Tower", risolti.Dati.Valore(TipoDato.Nominativo, "LIRF_TWR"));
        // Senza nominativo IVAO vale il callsign, che è sempre vero.
        Assert.Equal("LIRR_CTR", risolti.Dati.Valore(TipoDato.Nominativo, "LIRR_CTR"));
    }

    /// <summary>
    /// 🔴 <b>Codice e nominativo della stessa postazione</b>, nello stesso testo, con UNA lettura del
    /// catalogo. Chiesto dal campo il 21 settembre 2026: «LIRR_NE e/o Roma Radar». Il codice esce com'è —
    /// la chiave è il valore — e si segnala quando la postazione sparisce, come una pista o un punto.
    /// </summary>
    [Fact]
    public async Task Il_Codice_E_Il_Nominativo_Della_Stessa_Postazione()
    {
        var catalogo = new Catalogo();
        var testi = new[] { "Chiama [[POS LIRF_DEL]] ([[ATC LIRF_DEL]]), poi [[POS LIXX_ZZZ]]." };

        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);

        Assert.Equal("Chiama LIRF_DEL (Fiumicino Delivery), poi LIXX_ZZZ.", Riferimenti.Sostituisci(testi[0], risolti));
        // ⚠️ Una lettura sola: sono due facce della stessa riga.
        Assert.Equal(1, catalogo.ChiamateNominativi);
        // La postazione inventata si segnala; quella vera no.
        var avvisi = ControlloDatiCitati.Controlla(new[] { ("Enti", (string?)testi[0]) }, risolti.Dati);
        var avviso = Assert.Single(avvisi);
        Assert.Equal(TipoDato.Postazione, avviso.Tipo);
        Assert.Equal("LIXX_ZZZ", avviso.Chiave);
        Assert.Equal("POS", avviso.Parola);
    }

    [Fact]
    public void Il_Gettone_Del_Codice_Si_Scrive_E_Si_Riconosce()
    {
        Assert.Equal("[[POS LIRR_NE]]", RiferimentiDato.Scrivi(TipoDato.Postazione, "lirr_ne"));
        Assert.Equal("Da LIRR_NE.",
            RiferimentiDato.Sostituisci("Da [[POS LIRR_NE]].", Valori((TipoDato.Postazione, "LIRR_NE", "LIRR_NE"))));
    }

    /// <summary>
    /// Le aree regolamentate citate nel testo (21 settembre 2026, committente): sotto l'id esce il nome di OGGI,
    /// quindi un'area rinominata dall'import si legge col nome nuovo; un'area sparita si segnala.
    /// </summary>
    [Fact]
    public async Task Un_Area_Regolamentata_Esce_Col_Nome_Di_Oggi_E_Si_Segnala_Se_Sparisce()
    {
        var catalogo = new Catalogo();
        var testi = new[] { "Attiva [[AREA 1242]] e [[AREA 99999]]." };

        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);
        Assert.Equal("Attiva LI R49B - Zita e 99999.", Riferimenti.Sostituisci(testi[0], risolti));
        Assert.Equal(1, catalogo.ChiamateAree);

        var avviso = Assert.Single(ControlloDatiCitati.Controlla(new[] { ("Aree", (string?)testi[0]) }, risolti.Dati));
        Assert.Equal(TipoDato.Area, avviso.Tipo);
        Assert.Equal("99999", avviso.Chiave);
        Assert.Equal("AREA", avviso.Parola);

        // Rinominata dall'import: stesso id, nome nuovo, e il testo non si tocca.
        catalogo.Aree = new[] { new SpecialAreaPick("1242", "LI R49B - Zita Nord", "R", null, null, new[] { "LIRR" }) };
        var dopo = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);
        Assert.StartsWith("Attiva LI R49B - Zita Nord e", Riferimenti.Sostituisci(testi[0], dopo));
    }

    [Fact]
    public async Task Un_Testo_Senza_Aree_Non_Chiede_Il_Catalogo_Delle_Aree()
    {
        var catalogo = new Catalogo();
        await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(new[] { "[[FREQ LIRF_TWR]]" });
        Assert.Equal(0, catalogo.ChiamateAree);
        Assert.Equal("[[AREA 1242]]", RiferimentiDato.Scrivi(TipoDato.Area, "1242"));
    }

    [Fact]
    public async Task Una_Pista_Che_Non_Ce_Piu_Si_Segnala()
    {
        var catalogo = new Catalogo();   // LIRF ha 16L e 16R
        var testi = new[] { "Da [[RWY LIRF 16L]] e da [[RWY LIRF 17L]]." };
        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);

        // Escono com'è scritto, tutte e due: un rinomino non si indovina.
        Assert.Equal("Da 16L e da 17L.", Riferimenti.Sostituisci(testi[0], risolti));

        // Ma l'avviso dice quale non c'è più.
        var daRivedere = ControlloDatiCitati.Controlla(new[] { ("Piste", (string?)testi[0]) }, risolti.Dati);
        var d = Assert.Single(daRivedere);
        Assert.Equal("LIRF 17L", d.Chiave);
        Assert.Equal("RWY", d.Parola);
        Assert.Equal(1, catalogo.ChiamatePiste);
    }

    [Fact]
    public async Task Un_Punto_Che_Il_Catalogo_Non_Ha_Si_Segnala()
    {
        var catalogo = new Catalogo();   // il catalogo ha OST e ELKAP
        var testi = new[] { "Via [[FIX OST]] poi [[FIX ZZZZ]]." };
        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);

        Assert.Equal("Via OST poi ZZZZ.", Riferimenti.Sostituisci(testi[0], risolti));
        var d = Assert.Single(ControlloDatiCitati.Controlla(new[] { ("Punti", (string?)testi[0]) }, risolti.Dati));
        Assert.Equal("ZZZZ", d.Chiave);
    }

    /// <summary>
    /// 🔴 Sorgente muta ≠ dato sparito. Col catalogo dei punti vuoto — rete giù, sectorfile spento — ogni
    /// `[[FIX …]]` sembrerebbe sparito, e la testata dell'editor si riempirebbe di avvisi falsi.
    /// </summary>
    [Fact]
    public async Task Una_Sorgente_Che_Non_Risponde_Non_Fa_Allarmi()
    {
        var catalogo = new Catalogo { Punti = new HashSet<string>(), Piste = Array.Empty<string>() };
        var testi = new[] { "[[FIX OST]] e [[RWY LIRF 16L]]" };
        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);

        Assert.Empty(ControlloDatiCitati.Controlla(new[] { ("Dove", (string?)testi[0]) }, risolti.Dati));
        // Il catalogo dei punti è una sorgente sola: o risponde o non risponde.
        Assert.False(risolti.Dati.Guardata(TipoDato.Punto));
        // Le piste invece si chiedono per SCALO. La domanda è stata fatta — quindi la famiglia è guardata —
        // ma LIRF non ha nemmeno una soglia in anagrafica: per quello scalo è «non lo so», non «non c'è».
        Assert.True(risolti.Dati.Guardata(TipoDato.Pista));
        Assert.True(risolti.Dati.Muta(TipoDato.Pista, "LIRF 16L"));
    }

    /// <summary>
    /// 🔴 L'altra metà, e prima mancava del tutto: uno scalo che <b>non esiste</b>. Con «guardata» dichiarata
    /// per famiglia, un ICAO inventato tornava senza soglie esattamente come uno scalo vero non ancora
    /// importato: se era l'unico citato la famiglia non risultava guardata e l'avviso <b>non compariva</b>,
    /// mentre se il testo citava anche uno scalo vero l'avviso arrivava. Stesso testo, due comportamenti.
    /// </summary>
    [Fact]
    public async Task Uno_Scalo_Che_Non_Esiste_Si_Segnala_Anche_Da_Solo()
    {
        var catalogo = new Catalogo();   // l'anagrafica risponde solo per LIRF
        var testi = new[] { "Da [[RWY LIZZ 07]]." };

        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);

        var d = Assert.Single(ControlloDatiCitati.Controlla(new[] { ("Piste", (string?)testi[0]) }, risolti.Dati));
        Assert.Equal("LIZZ 07", d.Chiave);
    }

    /// <summary>
    /// La pista vuole tutti e due i gettoni. Con il secondo facoltativo, un <c>[[RWY LIRF]]</c> scritto a mano
    /// entrava fra i citati, non si risolveva mai — la chiave delle piste è «scalo soglia» — e la testata
    /// diceva «non si trova più nell'archivio» di un dato che c'è.
    /// </summary>
    [Fact]
    public async Task Una_Pista_Senza_Soglia_Non_E_Un_Riferimento()
    {
        var catalogo = new Catalogo();
        var testi = new[] { "Da [[RWY LIRF]]." };

        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);

        Assert.Empty(RiferimentiDato.Citati(testi));
        Assert.Equal(testi[0], Riferimenti.Sostituisci(testi[0], risolti));   // il testo resta com'è
        Assert.Empty(ControlloDatiCitati.Controlla(new[] { ("Piste", (string?)testi[0]) }, risolti.Dati));
    }

    /// <summary>
    /// 🔴 Il nominativo non dipende dall'avere una frequenza. <c>LIRF_DEL</c> non ne ha una dichiarata — non
    /// è nell'elenco delle linkabili — e prima il suo <c>[[ATC …]]</c> usciva col ripiego e per giunta
    /// segnalato come sparito dall'archivio.
    /// </summary>
    [Fact]
    public async Task Un_Ente_Senza_Frequenza_Ha_Comunque_Un_Nominativo()
    {
        var catalogo = new Catalogo();
        var testi = new[] { "Chiama [[ATC LIRF_DEL]]." };

        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo).PerTestiAsync(testi);

        Assert.Equal("Chiama Fiumicino Delivery.", Riferimenti.Sostituisci(testi[0], risolti));
        Assert.Empty(ControlloDatiCitati.Controlla(new[] { ("Enti", (string?)testi[0]) }, risolti.Dati));
        // ⚠️ E non si è chiesto l'elenco delle frequenze: sono due domande diverse.
        Assert.Equal(0, catalogo.Chiamate);
        Assert.Equal(1, catalogo.ChiamateNominativi);
    }

    /// <summary>
    /// Il valore prende il posto del riferimento anche DENTRO il JSON dei blocchi tabella: una virgoletta
    /// arrivata dal catalogo IVAO spaccherebbe il JSON, e il blocco smetterebbe di rendersi in una pagina
    /// sola, senza un errore che lo dica.
    /// </summary>
    [Fact]
    public async Task Un_Nominativo_Con_Una_Virgoletta_Non_Spacca_Il_Json()
    {
        var catalogo = new Catalogo
        {
            Enti = new[] { new EnteRow(1, "LIRF", "LIRF_TWR", "Fiumicino \"Tower\"") },
        };
        var json = "{\"cells\":[\"Su [[ATC LIRF_TWR]]\"]}";

        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo)
            .PerTestiAsync(new[] { json });

        var reso = Riferimenti.Sostituisci(json, risolti)!;
        Assert.Equal("{\"cells\":[\"Su Fiumicino Tower\"]}", reso);
        System.Text.Json.JsonDocument.Parse(reso);   // resta JSON valido: è tutto il punto
    }

    /// <summary>La via breve: un testo che non cita dati non fa nessuna domanda.</summary>
    [Fact]
    public async Task Senza_Dati_Citati_Non_Si_Chiede_Niente()
    {
        var catalogo = new Catalogo();
        var risolti = await new RiferimentiResolver(new NienteProcedure(), catalogo)
            .PerTestiAsync(new[] { "Solo prosa, e una [[SID LIRF OST1E]]." });

        Assert.Equal(0, catalogo.Chiamate);
        Assert.True(risolti.Dati.Vuota);
    }
}
