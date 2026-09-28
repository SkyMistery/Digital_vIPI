using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// «Sto mostrando l'anteprima della release N di questo bersaglio»: dentro il blocco, le sezioni congelate di quel
/// bersaglio si leggono dallo snapshot di <b>quella</b> release, non da quella in vigore.
///
/// <para>🔴 <b>U-053 (revisione totale 3).</b> In anteprima di release (<c>?as=rel:N</c>) i loader derivavano piste,
/// regole, LVP, quote, frequenze, SID/STAR, radioassistenze e alternati dai dati <b>di oggi</b>, e
/// <see cref="FrozenSectionReader"/> sapeva leggere solo la release in vigore. L'editor programmava LIRF al 2611,
/// correggeva una TORA, e l'anteprima della 2611 mostrava il valore nuovo: al rollover il pubblico avrebbe visto
/// quello congelato alla programmazione.</para>
///
/// <para><b>Perché un contesto e non un parametro.</b> La release la conosce il loader della pagina; lo snapshot lo
/// legge <see cref="IFrozenSectionReader"/>, e in mezzo ci sono cinque servizi di derivazione, il documento
/// militare (radioassistenze, alternati) e il risolutore delle procedure — tutti già capaci di chiedere «il
/// congelato di questo bersaglio». È la stessa ragione di <see cref="ShapeReleaseContext"/>.</para>
///
/// <para>⚠️ <b>Asincrono per chiamata</b> (<see cref="AsyncLocal{T}"/>), non per circuito: vale solo per le attese
/// dentro il blocco che lo apre. Un circuito Blazor è uno solo per più componenti, e un valore di scope si
/// vedrebbe anche dal membro accanto di una pagina unita. Vale solo per il bersaglio indicato: un altro bersaglio
/// letto nello stesso blocco risponde con la sua release in vigore.</para>
///
/// <para>⚠️ <b>Lo apre solo chi ha già autorizzato l'anteprima</b> (<c>IReleaseService.GetPreviewAsync</c> non
/// nullo): il lettore non rifà il controllo.</para>
/// </summary>
public static class AnteprimaDiRelease
{
    private static readonly AsyncLocal<Voce?> _corrente = new();

    /// <summary>La release in anteprima e il suo bersaglio, o null fuori da un'anteprima.</summary>
    public static Voce? Corrente => _corrente.Value;

    /// <summary>L'anteprima aperta: tipo e chiave del bersaglio, e la release.</summary>
    public sealed record Voce(ReleaseTargetType Type, string Key, int ReleaseId)
    {
        /// <summary>È l'anteprima di questo bersaglio?</summary>
        public bool Di(ReleaseTargetType type, string key) =>
            Type == type && string.Equals(Key, key, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Apre l'anteprima per la durata del blocco; alla chiusura torna quel che c'era prima.</summary>
    public static IDisposable Apri(ReleaseTargetType type, string key, int releaseId)
    {
        var prima = _corrente.Value;
        _corrente.Value = new Voce(type, key, releaseId);
        return new Chiusura(prima);
    }

    private sealed class Chiusura : IDisposable
    {
        private readonly Voce? _prima;
        private bool _chiusa;
        public Chiusura(Voce? prima) => _prima = prima;

        public void Dispose()
        {
            if (_chiusa) return;
            _chiusa = true;
            _corrente.Value = _prima;
        }
    }
}
