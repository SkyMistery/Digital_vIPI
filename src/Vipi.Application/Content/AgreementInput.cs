using System;
using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// L'intestazione di un accordo in scrittura: chi sta ai due capi, che traffico riguarda, quali aeroporti.
/// <para>Le parti arrivano come <b>elenchi di id settore per lato</b> e non come righe con un ordine: l'ordine
/// dentro un lato è quello in cui l'editore li ha messi, e ricostruirlo dall'elenco è meno di quanto costi
/// tenerlo allineato a mano.</para>
/// </summary>
public sealed record AgreementInput
{
    public required TransferFlowKind TrafficKind { get; init; }
    public string? Description { get; init; }

    /// <summary>Gli enti del lato A, nell'ordine voluto. <b>Almeno uno</b>: un accordo ha due capi.</summary>
    public IReadOnlyList<int> SideA { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gli enti del lato B, nell'ordine voluto. <b>Almeno uno.</b>
    /// <para>⚠️ Vuoto <b>non</b> significa «a UNICOM», e prima l'interfaccia lo insegnava. UNICOM è ciò che
    /// <c>TransferOnlineResolver</c> <b>calcola a runtime</b> quando il ricevente è offline, risalendo la
    /// gerarchia di copertura: non è un capo che si scrive. Un lato B vuoto significava «non finito», e un
    /// accordo così non produce <b>niente</b> — la derivazione scarta la riga (è la policy che la rete di
    /// caratterizzazione ha fotografato: delle 78 righe vere ne derivano 77).</para>
    /// <para>Restano <b>due righe in archivio</b> che lo violano, e la regola vale su crea e modifica ma non sul
    /// ripristino: un annulla che rifiutasse di rimettere l'accordo appena cancellato sarebbe peggio della
    /// regola. Le trova la voce «senza ricevente» del cruscotto, che da qui in poi è un rilevatore di eredità.</para>
    /// </summary>
    public IReadOnlyList<int> SideB { get; init; } = Array.Empty<int>();

    /// <summary>Gli aeroporti a cui l'accordo si applica, nell'ordine voluto. Vuoto = accordo senza aeroporto
    /// (sorvolo/VFR/altro).</summary>
    public IReadOnlyList<AgreementAirportInput> Airports { get; init; } = Array.Empty<AgreementAirportInput>();
}

/// <summary>Un aeroporto dell'accordo. <paramref name="Name"/> serve solo agli scali fuori catalogo: per gli
/// altri il nome viene dal catalogo, e una copia qui sarebbe una seconda verità.</summary>
public sealed record AgreementAirportInput(string Icao, string? Name = null);
