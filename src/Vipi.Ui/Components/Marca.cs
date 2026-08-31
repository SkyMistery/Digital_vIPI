using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Vipi.Ui.Components;

/// <summary>
/// Il testo con la parte cercata <b>marcata</b>.
///
/// <para>
/// ⚠️ Si costruisce a mano con <c>AddContent</c> invece di comporre HTML: qui dentro passano frasi scritte
/// da persone — formule di fraseologia, rese di traduzione, codici battuti a mano — e una
/// <c>MarkupString</c> le renderebbe eseguibili. <c>AddContent</c> le tratta per quello che sono: testo.
/// </para>
///
/// <para>
/// ⚠️ È un componente e non un metodo per pagina perché lo usano in due (Fraseologia e traduzioni,
/// Radioassistenze), e la seconda copia sarebbe il posto in cui un giorno qualcuno userebbe una
/// <c>MarkupString</c> «perché lì funzionava». È in C# e non in Razor perché non ha markup: tutto quel che
/// fa è decidere dove aprire un <c>&lt;mark&gt;</c>.
/// </para>
/// </summary>
public sealed class Marca : ComponentBase
{
    /// <summary>Il testo da mostrare.</summary>
    [Parameter, EditorRequired] public string? Testo { get; set; }

    /// <summary>Quel che si sta cercando. Vuoto = niente da marcare, e il testo passa così com'è.</summary>
    [Parameter] public string? Cerca { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var testo = Testo ?? "";
        var ago = (Cerca ?? "").Trim();

        if (ago.Length == 0 || testo.Length == 0)
        {
            builder.AddContent(0, testo);
            return;
        }

        var seq = 0;
        var da = 0;
        while (true)
        {
            // ⚠️ CurrentCultureIgnoreCase: si cerca come cerca una persona, non come confronta un ordinale.
            var trovato = testo.IndexOf(ago, da, StringComparison.CurrentCultureIgnoreCase);
            if (trovato < 0) break;

            if (trovato > da) builder.AddContent(seq++, testo[da..trovato]);
            builder.OpenElement(seq++, "mark");
            builder.AddContent(seq++, testo.Substring(trovato, ago.Length));
            builder.CloseElement();
            da = trovato + ago.Length;
        }

        if (da < testo.Length) builder.AddContent(seq, testo[da..]);
    }
}
