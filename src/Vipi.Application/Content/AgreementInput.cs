using System;
using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// L'intestazione di un accordo in scrittura: <b>chi sta ai due capi</b>, e nient'altro.
/// <para>Creare un accordo è dire CHI. Il traffico — tipo, verso, aeroporti — sta nelle sezioni, che si
/// aggiungono dentro l'accordo con <see cref="AgreementSectionInput"/>.</para>
/// <para>⚠️ <b>Entrambi i capi, sempre.</b> Un lato vuoto non ha mai voluto dire «a UNICOM», anche se
/// l'interfaccia lo insegnava: UNICOM è ciò che <c>TransferOnlineResolver</c> <b>calcola a runtime</b> quando
/// il ricevente è offline, risalendo la gerarchia di copertura — non è un capo che si scrive. Da qui in poi la
/// regola è anche di schema: le due colonne sono NOT NULL.</para>
/// </summary>
public sealed record AgreementInput
{
    /// <summary>Il settore a un capo. L'ordine fra i due lati <b>non</b> è una scelta editoriale: il repository
    /// li mette in forma canonica (id minore = A) perché l'unicità della coppia è un indice. Il verso lo porta
    /// la sezione, quindi girare i lati non cambia il significato di niente.</summary>
    public required int SideASectorId { get; init; }

    /// <inheritdoc cref="SideASectorId"/>
    public required int SideBSectorId { get; init; }

    /// <summary>Nota libera sull'accordo; la prosa che introduce una tabella sta sulla sezione.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Una sezione in scrittura: il traffico, il verso, gli aeroporti, la prosa.
/// <para><b>Ordine e posizione non stanno qui</b>: li decide il repository, come per le clausole. L'ordine
/// delle sezioni è imposto (aeroporto ▸ tipo ▸ verso) e non si digita.</para>
/// </summary>
public sealed record AgreementSectionInput
{
    public required TransferFlowKind Kind { get; init; }

    /// <summary>Il verso della sezione. Per arrivi e partenze lo <b>propone</b>
    /// <see cref="SectionDirection.Propose"/> dall'aeroporto, e resta correggibile: è una proposta, non un
    /// calcolo che si impone.</summary>
    public required AgreementDirection Direction { get; init; }

    public string? Description { get; init; }

    /// <summary>Gli aeroporti della sezione, nell'ordine voluto. Obbligatori per arrivi e partenze, vietati sui
    /// sorvoli, facoltativi su VFR/Altro.</summary>
    public IReadOnlyList<AgreementAirportInput> Airports { get; init; } = Array.Empty<AgreementAirportInput>();
}

/// <summary>Un aeroporto della sezione. <paramref name="Name"/> serve solo agli scali fuori catalogo: per gli
/// altri il nome viene dal catalogo, e una copia qui sarebbe una seconda verità.</summary>
public sealed record AgreementAirportInput(string Icao, string? Name = null);
