using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge.Tests;

/// <summary>
/// Client del protocollo Aurora contro un server finto. Coperti i tre vincoli scoperti in F0: correlazione
/// per eco del comando, messaggi non sollecitati in mezzo, e il rifiuto della scrittura su traffico non assunto.
/// </summary>
public class AuroraClientTests
{
    /// <summary>
    /// Il tempo d'attesa NON è un'asserzione: nei test che pretendono una risposta è solo la rete di sicurezza
    /// che impedisce alla suite di piantarsi. Va quindi largo.
    ///
    /// <para>⚠️ Era 1500 ms, ed è il sospetto numero uno del fallimento a intermittenza osservato l'11 agosto
    /// 2026 (2 volte su ~9 giri della suite intera, mai in isolamento). Il meccanismo si legge in
    /// <c>AuroraClient.SendAsync</c>: se la risposta non arriva entro <c>TimeoutMs</c> torna
    /// <c>Ok = false</c> con «Nessuna risposta», e <c>Assert.True(response.Ok)</c> fallisce senza dire
    /// perché. Con dodici assembly di test in parallelo il thread-pool cresce di circa un thread al secondo
    /// oltre il numero di core: il <c>Task.Run</c> del ciclo di lettura, e quello che serve il socket dal
    /// lato finto, possono aspettare il proprio turno per centinaia di millisecondi. Su localhost il tempo di
    /// rete è nullo — a scadere è la coda, non la trasmissione.</para>
    ///
    /// <para><b>Non è una correzione verificata</b>: il guasto non si è più riprodotto in 9 giri, 3 dei quali
    /// con la macchina sotto carico. È la causa più probabile letta nel codice, più il fatto che 1500 ms qui
    /// non misurano niente di utile. L'altra metà della difesa sono i messaggi di asserzione qui sotto, che
    /// alla prossima occorrenza dicono che cosa è successo invece di «expected True, actual False».</para>
    ///
    /// <para>Il solo test che ha bisogno di un tempo CORTO è quello del silenzio, che se lo passa a mano.</para>
    /// </summary>
    private static AuroraClient Connect(FakeAuroraServer server, int timeoutMs = 15000) =>
        new(new AuroraClientOptions("127.0.0.1", server.Port, timeoutMs));

    [Fact]
    public async Task Comando_semplice_e_risposta()
    {
        await using var server = new FakeAuroraServer().Reply("#CONN", "#CONN;LIZZ_AEW_CTR");
        await using var client = Connect(server);

        var response = await client.SendAsync("#CONN");

        Assert.True(response.Ok, $"atteso esito positivo, ricevuto: {response.Error} (raw: «{response.Raw}»)");
        Assert.Equal("LIZZ_AEW_CTR", response.Fields[0]);
    }

    [Fact]
    public async Task Gli_argomenti_finiscono_nel_pacchetto_separati_da_punto_e_virgola()
    {
        await using var server = new FakeAuroraServer().Reply("#LBALT", "#LBALT;FDX126;250");
        await using var client = Connect(server);

        await client.SendAsync("#LBALT", CancellationToken.None, "FDX126", "250");

        Assert.Contains("#LBALT;FDX126;250", server.Received);
    }

    [Fact]
    public async Task Un_argomento_con_punto_e_virgola_non_parte_nemmeno()
    {
        await using var server = new FakeAuroraServer();
        await using var client = Connect(server);

        var response = await client.SendAsync("#LBALT", CancellationToken.None, "FDX126", "per aerovia; come da LoA");

        Assert.False(response.Ok);
        Assert.Contains("«;»", response.Error);
        Assert.Empty(server.Received);   // il protocollo si romperebbe: non si invia proprio
    }

    [Fact]
    public async Task Errore_di_Aurora_diventa_un_esito_negativo_col_messaggio()
    {
        await using var server = new FakeAuroraServer()
            .Reply("#LBALT", "@ERR;#LBALT;RYR90RC;250;Traffic not assumed.");
        await using var client = Connect(server);

        var response = await client.SendAsync("#LBALT", CancellationToken.None, "RYR90RC", "250");

        Assert.False(response.Ok);
        Assert.Equal("Traffic not assumed.", response.Error);
    }

    [Fact]
    public async Task Comando_sconosciuto_e_riconosciuto_come_tale()
    {
        await using var server = new FakeAuroraServer();   // il finto risponde @ERR;…;Unknown command
        await using var client = Connect(server);

        var response = await client.SendAsync("#LBXFL");

        Assert.False(response.Ok);
        Assert.Equal("Unknown command", response.Error);
    }

    [Fact]
    public async Task I_messaggi_non_sollecitati_non_vengono_scambiati_per_la_risposta()
    {
        await using var server = new FakeAuroraServer().Reply("#SELTFC", "#SELTFC;FDX126;");
        server.PushBeforeReply.Add("#INTERCOMPHONESTATUS;PHONE_RECEIVING");
        server.PushBeforeReply.Add("#INTERCOMCALLSTATUS;CALL_RESULT_IN_OK;LIRR_NE_CTR;;");

        await using var client = Connect(server);
        var pushed = new List<string>();
        client.Unsolicited += pushed.Add;

        var response = await client.SendAsync("#SELTFC");

        Assert.True(response.Ok, $"atteso esito positivo, ricevuto: {response.Error} (raw: «{response.Raw}»)");
        Assert.Equal("FDX126", response.Fields[0]);
        Assert.Equal(2, pushed.Count);
    }

    [Fact]
    public async Task Il_silenzio_scade_e_non_blocca_il_tool()
    {
        await using var server = new FakeAuroraServer();
        server.Silent.Add("#TR");
        await using var client = Connect(server, timeoutMs: 300);

        var response = await client.SendAsync("#TR");

        Assert.False(response.Ok);
        Assert.Contains("Nessuna risposta", response.Error);
    }

    [Fact]
    public async Task Aurora_chiusa_produce_un_errore_leggibile_non_un_eccezione()
    {
        // Una porta CERTAMENTE chiusa: se ne fa assegnare una libera dal sistema e si chiude subito il
        // listener. Prima era la porta 1, «quasi certamente libera» — e su una macchina che la filtra invece
        // di rifiutarla il SYN non riceve risposta e il test resta appeso al timeout del sistema operativo,
        // non ai 300 ms dell'opzione.
        var sonda = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        sonda.Start();
        var portaChiusa = ((System.Net.IPEndPoint)sonda.LocalEndpoint).Port;
        sonda.Stop();

        await using var client = new AuroraClient(new AuroraClientOptions("127.0.0.1", portaChiusa, 300));

        var response = await client.SendAsync("#CONN");

        Assert.False(response.Ok);
        Assert.Contains("non raggiungibile", response.Error);
    }

    /// <summary>
    /// Un host che <b>tace</b> invece di rifiutare (firewall che scarta i SYN, macchina spenta con l'IP
    /// ancora assegnato) non deve piantare il tool per il timeout del sistema operativo: <c>TimeoutMs</c>
    /// copre anche la connessione, non solo l'attesa della risposta.
    /// </summary>
    [Fact]
    public async Task Un_host_che_tace_scade_secondo_l_opzione_non_secondo_il_sistema()
    {
        // 203.0.113.0/24 è la rete TEST-NET-3 della RFC 5737: riservata alla documentazione, non instradata.
        // Un SYN verso lì non riceve né risposta né rifiuto — che è esattamente il caso da provare.
        await using var client = new AuroraClient(new AuroraClientOptions("203.0.113.1", 1130, 500));

        var orologio = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.SendAsync("#CONN");
        orologio.Stop();

        Assert.False(response.Ok);
        Assert.Contains("non raggiungibile", response.Error);
        // Largo il doppio: qui interessa che NON siano i ~21 secondi del sistema operativo.
        Assert.True(orologio.Elapsed < TimeSpan.FromSeconds(10),
            $"il tentativo di connessione è durato {orologio.Elapsed.TotalSeconds:0.0}s: TimeoutMs non copre la connessione.");
    }

    [Fact]
    public async Task Richieste_in_sequenza_non_si_mescolano()
    {
        await using var server = new FakeAuroraServer()
            .Reply("#TRPOS;A", "#TRPOS;A;1;1;1000;100;40.0;9.0;2000;;;;;;;0;0;0;;1;;0;;")
            .Reply("#TRPOS;B", "#TRPOS;B;2;2;2000;200;41.0;10.0;2000;;;;;;;0;0;0;;1;;0;;");
        await using var client = Connect(server);

        // Lanciate insieme: il client le serializza, quindi ognuna riceve LA SUA risposta.
        var first = client.SendAsync("#TRPOS", CancellationToken.None, "A");
        var second = client.SendAsync("#TRPOS", CancellationToken.None, "B");
        await Task.WhenAll(first, second);

        var a = await first;
        var b = await second;
        Assert.True(a.Ok, $"la prima richiesta non ha avuto risposta: {a.Error}");
        Assert.True(b.Ok, $"la seconda richiesta non ha avuto risposta: {b.Error}");
        Assert.Equal("A", a.Fields[0]);
        Assert.Equal("B", b.Fields[0]);
    }

    /// <summary>
    /// Due invii concorrenti su un client <b>ancora da connettere</b> aprono <b>una</b> connessione, non due.
    ///
    /// <para>È la causa del fallimento a intermittenza inseguito dall'11 al 22 agosto 2026 come se fosse un
    /// problema di tempi: <c>SendAsync</c> si connetteva <i>prima</i> di prendere il turno, quindi due
    /// chiamate lanciate insieme trovavano entrambe «non connesso» — l'assegnazione avviene dopo il
    /// <c>ConnectAsync</c>, che cede il controllo — e aprivano un socket a testa. Il secondo, nascendo,
    /// chiudeva il primo. L'esito peggiore non era un errore ma il <b>silenzio</b>: comando scritto su un
    /// socket e risposta attesa sul canale dell'altro, fino alla scadenza del tempo — cioè esattamente
    /// «Nessuna risposta a #TRPOS entro 15000 ms», che sembrava lentezza e non lo era.</para>
    ///
    /// <para>Il test sopra vede il <i>sintomo</i> e solo quando la corsa va male; questo vede la
    /// <b>causa</b>. ⚠️ E la vede solo se i due invii partono <b>davvero insieme</b>: chiamati uno dopo
    /// l'altro sullo stesso thread, su loopback la prima connessione fa in tempo a chiudersi e il secondo
    /// invio la trova già pronta — il test passa anche col difetto. Servono due thread e un cancelletto che
    /// li rilascia insieme. Misurato così sul client di prima: <b>200 giri su 200</b> aprivano due
    /// connessioni, e la suite ci metteva 3 minuti e 10 invece di 133 ms, perché ogni giro pagava una
    /// scadenza intera.</para>
    /// </summary>
    [Fact]
    public async Task Due_invii_insieme_aprono_una_connessione_sola()
    {
        await using var server = new FakeAuroraServer()
            .Reply("#TRPOS;A", "#TRPOS;A;1;1;1000;100;40.0;9.0;2000;;;;;;;0;0;0;;1;;0;;")
            .Reply("#TRPOS;B", "#TRPOS;B;2;2;2000;200;41.0;10.0;2000;;;;;;;0;0;0;;1;;0;;");
        await using var client = Connect(server);

        // Nessuno ha ancora connesso: è il momento in cui i due invii facevano a gara.
        Assert.False(client.IsConnected);

        var pronti = new SemaphoreSlim(0, 2);
        var via = new TaskCompletionSource();
        async Task<AuroraResponse> Invia(string arg)
        {
            pronti.Release();                 // «sono sul mio thread e sono pronto»
            await via.Task;                   // ...e parto solo insieme all'altro
            return await client.SendAsync("#TRPOS", CancellationToken.None, arg);
        }

        var a = Task.Run(() => Invia("A"));
        var b = Task.Run(() => Invia("B"));
        await pronti.WaitAsync();
        await pronti.WaitAsync();
        via.SetResult();

        var risposte = await Task.WhenAll(a, b);

        Assert.Equal(1, server.Connessioni);
        Assert.True(risposte[0].Ok, $"la richiesta A non ha avuto risposta: {risposte[0].Error}");
        Assert.True(risposte[1].Ok, $"la richiesta B non ha avuto risposta: {risposte[1].Error}");
        Assert.Equal("A", risposte[0].Fields[0]);
        Assert.Equal("B", risposte[1].Fields[0]);
    }
}
