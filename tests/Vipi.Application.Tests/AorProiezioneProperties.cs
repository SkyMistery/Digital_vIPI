using System.Globalization;
using System.Text.RegularExpressions;
using CsCheck;
using Vipi.Application.Aor;

namespace Vipi.Application.Tests;

/// <summary>
/// Proprietà della proiezione AoR, provate su poligoni <b>generati</b> invece che scelti a mano.
///
/// <para><b>Perché proprio qui.</b> È l'unico punto del prodotto dove il dominio è continuo: un poligono è
/// una lista di coppie di numeri reali, e qualunque elenco di esempi ne copre una fetta arbitraria — di
/// solito quella a cui pensava chi ha scritto il codice. Le regole che seguono valgono per <b>ogni</b>
/// poligono valido, e CsCheck le prova su centinaia di forme diverse a ogni giro; quando ne trova una che
/// rompe, la <b>rimpicciolisce</b> fino al minimo controesempio, che è la parte che fa risparmiare tempo.</para>
///
/// <para>⚠️ <b>Questi test non sono deterministici, ed è voluto</b>: i casi cambiano a ogni giro, quindi un
/// rosso può comparire domani su un codice fermo da settimane. Non è un test ballerino da rilanciare finché
/// passa — è un controesempio che prima non era stato pescato. Il messaggio riporta il <c>seed</c>: rimetterlo
/// (<c>-e CsCheck_Seed=…</c>) riproduce esattamente quel caso, che va poi congelato in un test a esempio
/// accanto agli altri in <c>AorPolygonProjectorTests</c>.</para>
///
/// <para>⚠️ Le tolleranze non sono generiche: il proiettore arrotonda a un decimale
/// (<c>Math.Round(v, 1)</c>), quindi due valori che devono coincidere possono distare fino a 0,1 per
/// estremo. Dove si confrontano differenze fra due coordinate la tolleranza è 0,2.</para>
/// </summary>
public class AorProiezioneProperties
{
    private const double Canvas = 400.0;   // gli stessi due numeri di AorPolygonProjector, che sono privati
    private const double Pad = 8.0;
    private const double Arrotondamento = 0.1;

    /// <summary>Poligoni plausibili per il prodotto: latitudini e longitudini da spazio aereo italiano.</summary>
    private static readonly Gen<(double Lat, double Lon)[]> PoligoniItaliani = Gen.Select(
            Gen.Double[35.0, 48.0], Gen.Double[6.0, 19.0], (lat, lon) => (Lat: lat, Lon: lon))
        .Array[3, 24];

    /// <summary>Poligoni ovunque nel mondo, poli esclusi: la stessa regola non deve dipendere dall'Italia.</summary>
    private static readonly Gen<(double Lat, double Lon)[]> PoligoniOvunque = Gen.Select(
            Gen.Double[-80.0, 80.0], Gen.Double[-170.0, 170.0], (lat, lon) => (Lat: lat, Lon: lon))
        .Array[3, 24];

    /// <summary>
    /// Ogni punto disegnato sta <b>dentro</b> il riquadro, margine compreso. È la proprietà che tiene in piedi
    /// la resa: un punto fuori dal viewBox non si vede e basta — nessuna eccezione, nessun test rosso, un
    /// pezzo di settore che sparisce.
    /// </summary>
    [Fact]
    public void Nessun_punto_esce_dal_riquadro()
    {
        PoligoniOvunque.Sample(punti =>
        {
            var p = AorPolygonProjector.Project(Json(punti));
            if (p is null) return;   // degenere: tutti i punti coincidenti, ed è un esito legittimo

            var (w, h) = ViewBox(p.ViewBox);
            foreach (var (x, y) in PuntiDelPath(p.Path))
            {
                Assert.InRange(x, Pad - Arrotondamento, w - Pad + Arrotondamento);
                Assert.InRange(y, Pad - Arrotondamento, h - Pad + Arrotondamento);
            }
        });
    }

    /// <summary>
    /// Il lato lungo del riquadro è sempre <b>400</b>: la scala è uniforme e satura la dimensione maggiore.
    /// Se un giorno diventasse «quasi 400», vorrebbe dire che la scala non è più uniforme — cioè che le forme
    /// si deformano, che è il difetto che nessuno nota guardando un poligono solo.
    /// </summary>
    [Fact]
    public void Il_lato_lungo_e_sempre_quello_del_riquadro()
    {
        PoligoniOvunque.Sample(punti =>
        {
            var p = AorPolygonProjector.Project(Json(punti));
            if (p is null) return;

            var (w, h) = ViewBox(p.ViewBox);
            Assert.Equal(Canvas, Math.Max(w, h), 1);   // 1 cifra decimale: è l'arrotondamento del proiettore
        });
    }

    /// <summary>
    /// Spostare <b>tutte</b> le longitudini della stessa quantità non cambia il disegno. La proiezione scala la
    /// longitudine per il coseno della latitudine <i>media</i>, che una traslazione in longitudine non tocca:
    /// il disegno dipende dalla forma, non da dove sta sul meridiano. Se un giorno smettesse di valere, vorrebbe
    /// dire che l'origine si è infilata nel calcolo.
    /// </summary>
    [Fact]
    public void Spostare_la_longitudine_non_cambia_il_disegno()
    {
        Gen.Select(PoligoniItaliani, Gen.Double[-25.0, 25.0]).Sample(caso =>
        {
            var (punti, delta) = caso;
            var originale = AorPolygonProjector.Project(Json(punti));
            var spostato = AorPolygonProjector.Project(Json(punti.Select(p => (p.Lat, Lon: p.Lon + delta)).ToArray()));

            if (originale is null) { Assert.Null(spostato); return; }
            Assert.NotNull(spostato);
            Assert.Equal(originale.ViewBox, spostato!.ViewBox);

            // Il path si confronta a numeri, non a stringhe: l'arrotondamento può far cadere una coordinata
            // dall'altra parte del mezzo decimale, e un confronto testuale lo chiamerebbe difetto.
            var a = PuntiDelPath(originale.Path);
            var b = PuntiDelPath(spostato.Path);
            Assert.Equal(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].X, b[i].X, 0);
                Assert.Equal(a[i].Y, b[i].Y, 0);
            }
        });
    }

    /// <summary>
    /// La proiezione <b>non deforma</b>: il rapporto fra i lati del disegno è quello fra i lati dell'estensione
    /// proiettata. È la stessa cosa che si vede a schermo come «il settore ha la forma giusta», e l'unico modo
    /// di provarla senza guardare è confrontare i due rapporti.
    /// </summary>
    [Fact]
    public void Il_rapporto_fra_i_lati_e_quello_vero()
    {
        PoligoniItaliani.Sample(punti =>
        {
            var p = AorPolygonProjector.Project(Json(punti));
            if (p is null) return;

            // Estensione nello spazio proiettato: x = lon·cos(lat medio), y = -lat.
            var k = Math.Cos(punti.Average(q => q.Lat) * Math.PI / 180.0);
            var spanX = punti.Max(q => q.Lon * k) - punti.Min(q => q.Lon * k);
            var spanY = punti.Max(q => q.Lat) - punti.Min(q => q.Lat);
            var span = Math.Max(spanX, spanY);
            if (span <= 0) return;

            var (w, h) = ViewBox(p.ViewBox);
            var scala = (Canvas - 2 * Pad) / span;
            Assert.Equal(spanX * scala + 2 * Pad, w, 1);
            Assert.Equal(spanY * scala + 2 * Pad, h, 1);
        });
    }

    /// <summary>
    /// Il riquadro condiviso di <b>un</b> poligono solo è quello che quel poligono avrebbe da solo. Le due
    /// proiezioni sono due funzioni diverse che devono accordarsi sul caso in comune: è lì che una modifica a
    /// una sola delle due si fa vedere — e l'effetto sarebbe due carte della stessa AoR con scale diverse.
    /// </summary>
    [Fact]
    public void Con_un_poligono_solo_le_due_proiezioni_coincidono()
    {
        PoligoniItaliani.Sample(punti =>
        {
            var json = Json(punti);
            var singola = AorPolygonProjector.Project(json);
            var condivisa = AorPolygonProjector.ProjectShared(new[] { json });

            if (singola is null) return;   // il degenere lo trattano diversamente, ed è dichiarato
            Assert.NotNull(condivisa);
            Assert.Equal(singola.ViewBox, condivisa!.ViewBox);

            var a = PuntiDelPath(singola.Path);
            var b = PuntiDelPath(condivisa.Polygons[0]!.Path);
            Assert.Equal(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].X, b[i].X, 0);
                Assert.Equal(a[i].Y, b[i].Y, 0);
            }
        });
    }

    /// <summary>
    /// Meno di tre punti non è un poligono: torna <c>null</c>, sempre. La UI ci conta per mostrare il
    /// segnaposto invece di un disegno vuoto.
    /// </summary>
    [Fact]
    public void Con_meno_di_tre_punti_non_c_e_poligono()
    {
        Gen.Select(Gen.Double[-80.0, 80.0], Gen.Double[-170.0, 170.0], (lat, lon) => (Lat: lat, Lon: lon))
            .Array[0, 2]
            .Sample(punti => Assert.Null(AorPolygonProjector.Project(Json(punti))));
    }

    /// <summary>
    /// Formato IVAO <c>regionMapPolygon</c>: coppie <c>[lng, lat]</c>, <b>longitudine prima</b>. Scritto al
    /// contrario, il poligono entra ruotato e le proprietà falliscono per il motivo sbagliato — è successo
    /// scrivendo questi test, fidandosi di un commento del proiettore che era rimasto indietro.
    /// </summary>
    private static string Json((double Lat, double Lon)[] punti) =>
        "[" + string.Join(",", punti.Select(p =>
            $"[{p.Lon.ToString("0.######", CultureInfo.InvariantCulture)},{p.Lat.ToString("0.######", CultureInfo.InvariantCulture)}]")) + "]";

    private static (double W, double H) ViewBox(string viewBox)
    {
        var parti = viewBox.Split(' ');
        return (double.Parse(parti[2], CultureInfo.InvariantCulture), double.Parse(parti[3], CultureInfo.InvariantCulture));
    }

    private static readonly Regex Coppia = new(@"[ML]\s*(-?[\d.]+)\s+(-?[\d.]+)", RegexOptions.Compiled);

    private static List<(double X, double Y)> PuntiDelPath(string path) => Coppia.Matches(path)
        .Select(m => (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                      double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
        .ToList();
}
