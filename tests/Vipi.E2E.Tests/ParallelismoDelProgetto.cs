// Un test alla volta, in TUTTO questo progetto.
//
// ⚠️ Non è una preferenza di stile: è la correzione di lavori-aperti Q5, il rosso intermittente di
// `CronometroAvvioTests.Lavvio_vero_lascia_il_riepilogo_nel_file_di_diagnostica`.
//
// PERCHÉ. Il file di diagnostica dell'avvio è UNO SOLO per processo — «diagnostica/avvio-diagnostica.txt»
// accanto all'eseguibile, che qui è la cartella bin del progetto di test — e per disegno ogni avvio lo
// RISCRIVE da capo (`StartupDiagnostics.WriteConfigurationSummary` fa `File.WriteAllText`) prima di
// aggiungerci in coda il riepilogo delle fasi (`CronometroAvvio.Scrivi`). In produzione è giusto così:
// c'è un host per processo, e chi scarica quel file vuole l'avvio corrente, non una pila di avvii vecchi.
//
// Qui invece di host se ne avviano DIECI classi, e xUnit fa girare le classi in parallelo. La finestra
// fra «il mio avvio ha scritto» e «io rileggo» è quindi aperta a chiunque: basta che un'altra classe
// costruisca il suo host in mezzo perché la sua `WriteAllText` porti via il riepilogo appena scritto, e
// il test rilegga un file che non è il suo. Da solo il test passava sempre, col progetto intero cadeva:
// il segnale era la contesa, non il test.
//
// PERCHÉ COSÌ E NON PIÙ STRETTO. Serializzare le sole classi che avviano un host (una `[Collection]`
// condivisa) costerebbe uguale — sono quelle lente — e lascerebbe una trappola silenziosa: l'undicesima
// classe che avvia un host senza mettersi l'attributo rimetterebbe il rosso, senza che nulla lo dica.
// L'attributo di assembly copre anche chi arriva domani.
//
// QUANTO COSTA. Misurato il 28 agosto 2026, due corse per parte a build ferma: 227 test, 43 s in
// parallelo, 78 s in fila. Sono ~35 secondi pagati una volta per corsa, in cambio di un cancello di cui
// ci si può fidare — che è esattamente ciò che un rosso intermittente toglie: finché c'era, «tutto verde»
// andava letto «tutto verde salvo uno che non c'entra», ed è il modo in cui un rosso vero passa
// inosservato. In cambio spariscono anche i picchi di memoria di dieci host insieme.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
