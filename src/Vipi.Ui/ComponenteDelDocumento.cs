using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Vipi.Ui;

/// <summary>
/// La base dei componenti che stanno <b>dentro il corpo di un documento</b>: il loro <c>L</c> parla la lingua
/// di QUEL documento anche quando la pagina ne mostra più d'uno (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §3, «ogni documento la sua lingua»).
///
/// <para>
/// 🔴 <b>Perché una cascata e non il contesto della richiesta.</b> Su una pagina con un documento solo basta
/// <c>ReadingLanguageContext.Fissa</c>, che vale per tutta la richiesta. Su una pagina unita i documenti sono
/// due, con due regole — la vIPI di LIRP bilingue, il suo vSOP solo in inglese — e una lingua «della
/// richiesta» ne può onorare una sola: fino al 1° ottobre 2026 vinceva la porta, e il membro si prendeva le
/// etichette e le intestazioni della porta in mezzo al proprio testo. Cambiare la lingua del contesto mentre
/// si disegna un membro non è una strada: in Blazor i figli si disegnano <b>dopo</b> i fratelli del padre (la
/// coda di render va in larghezza), e la lingua sarebbe già cambiata di nuovo — è la pagina a chiazze che
/// <see cref="LocalizzatoreDiLingua"/> racconta. La cascata invece arriva a ogni componente <b>prima</b> del
/// suo render, e vale solo sotto il membro che la mette.
/// </para>
///
/// <para>
/// ⚠️ <b>Senza cascata non cambia niente</b>: <c>L</c> è il localizzatore iniettato di sempre, che segue chi
/// legge o la lingua bloccata della pagina. La cascata la mette solo <c>UnionLoader</c>, attorno al corpo di
/// un membro.
/// </para>
///
/// <para>
/// ⚠️ <b>Le isole interattive non la usano</b> (<c>AirportSids</c>, <c>AirportWeather</c>): una cascata non
/// attraversa il confine SSR→circuito. Hanno già il parametro <c>Lingua</c>, che il corpo passa a mano.
/// </para>
/// </summary>
public abstract class ComponenteDelDocumento : ComponentBase
{
    /// <summary>Il nome della cascata: una stringa da sola è un tipo troppo comune per cascare senza nome.</summary>
    public const string Cascata = "LinguaDelMembro";

    [Inject] private IStringLocalizer<SharedResource> Iniettato { get; set; } = default!;

    /// <summary>La lingua del documento di cui questo componente fa parte, se è un membro di un'unione.</summary>
    [CascadingParameter(Name = Cascata)] public string? LinguaDelMembro { get; set; }

    /// <summary>Le stringhe d'interfaccia nella lingua del documento.</summary>
    protected IStringLocalizer<SharedResource> L
    {
        get
        {
            // Uno per lingua, non uno per etichetta: un corpo ne legge a centinaia.
            if (_per != LinguaDelMembro || _scelto is null)
            {
                _scelto = Scegli(Iniettato, LinguaDelMembro);
                _per = LinguaDelMembro;
            }
            return _scelto;
        }
    }

    private IStringLocalizer<SharedResource>? _scelto;
    private string? _per;

    /// <summary>
    /// La regola, per chi non può ereditare da qui perché ha già una base (<c>ValidityStamp</c>).
    /// <para>⚠️ Una sola regola in un posto solo: la copia scritta a mano in un componente sarebbe quella che
    /// un giorno risponde diverso.</para>
    /// </summary>
    public static IStringLocalizer<SharedResource> Scegli(IStringLocalizer<SharedResource> iniettato, string? linguaDelMembro) =>
        linguaDelMembro is { Length: > 0 } l ? LocalizzatoreDiLingua.DelMembro(iniettato, l) : iniettato;
}
