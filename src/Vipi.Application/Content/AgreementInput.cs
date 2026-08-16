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

    /// <summary>Gli enti del lato A, nell'ordine voluto. Vuoto = accordo senza un capo, che è incompleto ma
    /// scrivibile: un'intestazione a metà è lavoro in corso, non un errore da rifiutare.</summary>
    public IReadOnlyList<int> SideA { get; init; } = Array.Empty<int>();

    /// <summary>Gli enti del lato B. Vuoto = il traffico va rilasciato a UNICOM, ed è il caso che il filtro
    /// «senza ricevente» deve poter trovare.</summary>
    public IReadOnlyList<int> SideB { get; init; } = Array.Empty<int>();

    /// <summary>Gli aeroporti a cui l'accordo si applica, nell'ordine voluto. Vuoto = accordo senza aeroporto
    /// (sorvolo/VFR/altro).</summary>
    public IReadOnlyList<AgreementAirportInput> Airports { get; init; } = Array.Empty<AgreementAirportInput>();
}

/// <summary>Un aeroporto dell'accordo. <paramref name="Name"/> serve solo agli scali fuori catalogo: per gli
/// altri il nome viene dal catalogo, e una copia qui sarebbe una seconda verità.</summary>
public sealed record AgreementAirportInput(string Icao, string? Name = null);
