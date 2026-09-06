using Vipi.Application.Content;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// I gruppi del sommario per gli <b>altri membri</b> di una pagina unita.
///
/// <para>⚠️ Era il componente <c>UnionToc</c>, che disegnava un <c>&lt;aside class="toc"&gt;</c> <b>per
/// membro</b>, impilati. Dal 6 settembre 2026 non disegna più niente: <b>costruisce i gruppi</b> che
/// <c>DocumentToc</c> mette dentro il proprio, unico, riquadro. Il motivo non è estetico ed è stato
/// misurato: due indici impilati vanno avvolti in un <c>&lt;div&gt;</c>, quel <c>&lt;div&gt;</c> diventa il
/// genitore del <c>position:sticky</c> ed è alto <b>esattamente quanto la barra</b> (392, 359 e 890 px sui
/// tre documenti), quindi il sommario <b>se ne andava in cima appena si scorreva</b>.</para>
///
/// <para>⚠️ Un gruppo per membro, e non un elenco solo con dentro tutto: ventisei voci militari seguite da
/// dieci d'avvicinamento, senza una riga che dica dove finisce l'uno e comincia l'altro, sono un indice che
/// non aiuta a cercare — che è il suo unico mestiere.</para>
/// </summary>
public static class TocGruppiUnione
{
    /// <summary>
    /// I gruppi dei membri, uno per documento, intestati col loro titolo.
    /// </summary>
    /// <param name="membri">Gli altri membri dell'unione. Vuoto = nessun gruppo, ed è il caso normale.</param>
    /// <param name="bozza">Anteprima BOZZA: le sezioni nascoste compaiono anche nell'indice. Default dei
    /// chiamanti <c>false</c>, quello prudente.</param>
    public static IEnumerable<TocGruppo> Di(IReadOnlyList<MembroUnito> membri, bool bozza) =>
        membri.Select(m => new TocGruppo(m.Titolo, TocVoce.Da(m.Sezioni, bozza, AncoraDelCorpo(m))));

    /// <summary>
    /// L'ancora che usa il CORPO di quella famiglia. Oggi tutte e tre rispondono <c>SectionView.Id</c> nudo,
    /// ma chiederlo al componente invece di ricopiarne la formula è ciò che tiene indice e corpo d'accordo il
    /// giorno che una famiglia cambia idea.
    /// <para>⚠️ Qui c'era scritto <c>s-{Id}</c>, che non è mai stato vero: il codice era giusto e la prosa
    /// no. È la peggiore delle due da sbagliare — chi legge il commento invece del codice per costruire
    /// un'ancora a mano ottiene un link che non fa niente, e <b>senza errore</b>.</para>
    /// </summary>
    private static Func<SectionView, string> AncoraDelCorpo(MembroUnito m) =>
        m.Membro.Doc.ReleaseTarget switch
        {
            Vipi.Domain.ReleaseTargetType.App => AppDocumentBody.AnchorOf,
            Vipi.Domain.ReleaseTargetType.Airport => AirportDocumentBody.AnchorOf,
            Vipi.Domain.ReleaseTargetType.AirportMil => MilDocumentBody.AnchorOf,
            _ => s => s.Id,
        };
}
