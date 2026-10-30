using System.Runtime.InteropServices;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Una pagina di un PDF fatta immagine: i pixel in RGB, riga per riga, e la misura della pagina in punti (1/72").</summary>
public sealed record ImmagineDiUnaPagina(int Larghezza, int Altezza, byte[] Rgb, double LarghezzaInPunti, double AltezzaInPunti);

/// <summary>
/// L'immagine di una pagina dell'AIP (carta «Gli aeroporti nel Lab», S3, D3): la fa PDFium, il motore dei PDF di Chrome,
/// coi binari di <c>bblanchon.PDFium</c> (<c>pdfium.dll</c> su Windows, <c>libpdfium.so</c> per la CI). Il collegamento è
/// nostro e sta tutto qui: le poche funzioni che servono per disegnare una pagina su fondo bianco.
/// <para>🔴 PDFium non regge due fili insieme: ogni chiamata passa da un lucchetto solo.</para>
/// <para>Il PDF si apre dalla memoria (<c>FPDF_LoadMemDocument64</c>), non dal percorso: PDFium su Windows apre i file con
/// <c>fopen</c>, e un percorso con lettere accentate non si aprirebbe.</para>
/// </summary>
public static class Pdfium
{
    /// <summary>Il lato più lungo di un'immagine, in pixel: una carta A4 a 200 dpi ne fa 2 339; oltre, la memoria scappa.</summary>
    public const int LatoMassimo = 6000;

    private static readonly Lock Lucchetto = new();
    private static bool _avviata;

    /// <summary>
    /// Disegna la pagina <paramref name="posizione"/> (da 1) del PDF a <paramref name="dpi"/> punti per pollice, su fondo
    /// bianco e con le annotazioni (i timbri di emendamento). Eccezione col perché se il PDF non si apre o la pagina non c'è.
    /// </summary>
    public static ImmagineDiUnaPagina Disegna(byte[] pdf, int posizione, int dpi = 200)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentOutOfRangeException.ThrowIfLessThan(posizione, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(dpi, 1);

        lock (Lucchetto)
        {
            if (!_avviata)
            {
                FPDF_InitLibrary();
                _avviata = true;
            }

            var fermo = GCHandle.Alloc(pdf, GCHandleType.Pinned);
            var documento = IntPtr.Zero;
            var pagina = IntPtr.Zero;
            var tela = IntPtr.Zero;
            try
            {
                documento = FPDF_LoadMemDocument64(fermo.AddrOfPinnedObject(), (nuint)pdf.Length, IntPtr.Zero);
                if (documento == IntPtr.Zero)
                    throw new InvalidDataException($"PDFium non apre il PDF (errore {FPDF_GetLastError().Value}).");
                var pagine = FPDF_GetPageCount(documento);
                if (posizione > pagine)
                    throw new InvalidDataException($"Il PDF ha {pagine} pagine: la {posizione} non c'è.");
                pagina = FPDF_LoadPage(documento, posizione - 1);
                if (pagina == IntPtr.Zero)
                    throw new InvalidDataException($"PDFium non apre la pagina {posizione}.");

                // La misura della pagina com'è vista (già girata, se il PDF la gira).
                double larghezzaInPunti = FPDF_GetPageWidthF(pagina), altezzaInPunti = FPDF_GetPageHeightF(pagina);
                var scala = Math.Min(dpi / 72.0, LatoMassimo / Math.Max(larghezzaInPunti, altezzaInPunti));
                int larghezza = Math.Max(1, (int)Math.Round(larghezzaInPunti * scala));
                int altezza = Math.Max(1, (int)Math.Round(altezzaInPunti * scala));

                tela = FPDFBitmap_Create(larghezza, altezza, 0);
                if (tela == IntPtr.Zero)
                    throw new InvalidDataException($"PDFium non fa una tela di {larghezza}×{altezza} pixel.");
                FPDFBitmap_FillRect(tela, 0, 0, larghezza, altezza, 0xFFFFFFFF);
                FPDF_RenderPageBitmap(tela, pagina, 0, 0, larghezza, altezza, 0, FpdfAnnot);

                // La tela è BGRx, quattro byte a pixel, righe di «passo» byte: si porta a RGB.
                var passo = FPDFBitmap_GetStride(tela);
                var riga = new byte[passo];
                var rgb = new byte[larghezza * altezza * 3];
                var inizio = FPDFBitmap_GetBuffer(tela);
                for (int y = 0; y < altezza; y++)
                {
                    Marshal.Copy(inizio + y * passo, riga, 0, passo);
                    for (int x = 0, d = y * larghezza * 3; x < larghezza; x++, d += 3)
                    {
                        rgb[d] = riga[x * 4 + 2];
                        rgb[d + 1] = riga[x * 4 + 1];
                        rgb[d + 2] = riga[x * 4];
                    }
                }
                return new ImmagineDiUnaPagina(larghezza, altezza, rgb, larghezzaInPunti, altezzaInPunti);
            }
            finally
            {
                if (tela != IntPtr.Zero) FPDFBitmap_Destroy(tela);
                if (pagina != IntPtr.Zero) FPDF_ClosePage(pagina);
                if (documento != IntPtr.Zero) FPDF_CloseDocument(documento);
                fermo.Free();
            }
        }
    }

    /// <summary>FPDF_ANNOT: disegna anche le annotazioni della pagina.</summary>
    private const int FpdfAnnot = 0x01;

    private const string Libreria = "pdfium";

    [DllImport(Libreria)] private static extern void FPDF_InitLibrary();
    [DllImport(Libreria)] private static extern IntPtr FPDF_LoadMemDocument64(IntPtr dati, nuint misura, IntPtr password);
    [DllImport(Libreria)] private static extern CULong FPDF_GetLastError();
    [DllImport(Libreria)] private static extern int FPDF_GetPageCount(IntPtr documento);
    [DllImport(Libreria)] private static extern IntPtr FPDF_LoadPage(IntPtr documento, int indice);
    [DllImport(Libreria)] private static extern float FPDF_GetPageWidthF(IntPtr pagina);
    [DllImport(Libreria)] private static extern float FPDF_GetPageHeightF(IntPtr pagina);
    [DllImport(Libreria)] private static extern IntPtr FPDFBitmap_Create(int larghezza, int altezza, int alfa);
    [DllImport(Libreria)] private static extern int FPDFBitmap_FillRect(IntPtr tela, int sinistra, int alto, int larghezza, int altezza, uint colore);
    [DllImport(Libreria)] private static extern void FPDF_RenderPageBitmap(IntPtr tela, IntPtr pagina, int x, int y, int larghezza, int altezza, int giro, int bandiere);
    [DllImport(Libreria)] private static extern IntPtr FPDFBitmap_GetBuffer(IntPtr tela);
    [DllImport(Libreria)] private static extern int FPDFBitmap_GetStride(IntPtr tela);
    [DllImport(Libreria)] private static extern void FPDFBitmap_Destroy(IntPtr tela);
    [DllImport(Libreria)] private static extern void FPDF_ClosePage(IntPtr pagina);
    [DllImport(Libreria)] private static extern void FPDF_CloseDocument(IntPtr documento);
}
