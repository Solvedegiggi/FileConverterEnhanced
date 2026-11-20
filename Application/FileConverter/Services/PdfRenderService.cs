// <copyright file="PdfRenderService.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    using Docnet.Core;
    using Docnet.Core.Models;

    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;

    /// <summary>
    /// Thread-safe PDF rendering service backed by Docnet.Core (PDFium) with bounded LRU preview cache.
    /// </summary>
    public static class PdfRenderService
    {
        private const int MaxCacheEntries = 80;
        private static readonly object RenderLock = new object();
        private static readonly Dictionary<string, LinkedListNode<CacheEntry>> CacheMap =
            new Dictionary<string, LinkedListNode<CacheEntry>>(StringComparer.Ordinal);
        private static readonly LinkedList<CacheEntry> CacheLru = new LinkedList<CacheEntry>();

        public static int GetPageCount(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                return 0;
            }

            lock (RenderLock)
            {
                using (var reader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(100, 100)))
                {
                    return reader.GetPageCount();
                }
            }
        }

        public static (double WidthPt, double HeightPt) GetPageSizePoints(string pdfPath, int pageIndex)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                return (595.0, 842.0);
            }

            try
            {
                using (PdfDocument doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import))
                {
                    if (pageIndex < 0 || pageIndex >= doc.PageCount)
                    {
                        return (595.0, 842.0);
                    }

                    PdfPage page = doc.Pages[pageIndex];
                    PdfRectangle crop = page.CropBox;
                    double w = crop.Width > 1 ? crop.Width : page.Width.Point;
                    double h = crop.Height > 1 ? crop.Height : page.Height.Point;

                    int rot = ((page.Rotate % 360) + 360) % 360;
                    if (rot == 90 || rot == 270)
                    {
                        return (h, w);
                    }

                    return (w, h);
                }
            }
            catch
            {
                return (595.0, 842.0);
            }
        }

        public static BitmapSource RenderPageToBitmapSource(string pdfPath, int pageIndex, int maxWidthPx, int maxHeightPx)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath) || pageIndex < 0)
            {
                return null;
            }

            maxWidthPx = Math.Max(16, maxWidthPx);
            maxHeightPx = Math.Max(16, maxHeightPx);

            long ticks = File.GetLastWriteTimeUtc(pdfPath).Ticks;
            string cacheKey = $"{pdfPath}|{ticks}|{pageIndex}|{maxWidthPx}x{maxHeightPx}";

            lock (CacheMap)
            {
                if (CacheMap.TryGetValue(cacheKey, out LinkedListNode<CacheEntry> existing))
                {
                    CacheLru.Remove(existing);
                    CacheLru.AddFirst(existing);
                    return existing.Value.Image;
                }
            }

            byte[] bgra;
            int width;
            int height;

            int dimOne = Math.Min(maxWidthPx, maxHeightPx);
            int dimTwo = Math.Max(maxWidthPx, maxHeightPx);

            lock (RenderLock)
            {
                using (var reader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(dimOne, dimTwo)))
                {
                    if (pageIndex >= reader.GetPageCount())
                    {
                        return null;
                    }

                    using (var pageReader = reader.GetPageReader(pageIndex))
                    {
                        width = pageReader.GetPageWidth();
                        height = pageReader.GetPageHeight();
                        bgra = pageReader.GetImage();
                    }
                }
            }

            if (bgra == null || width <= 0 || height <= 0)
            {
                return null;
            }

            CompositeOverWhiteInPlace(bgra);

            int stride = width * 4;
            BitmapSource bitmapSource = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                bgra,
                stride);
            bitmapSource.Freeze();

            lock (CacheMap)
            {
                if (!CacheMap.ContainsKey(cacheKey))
                {
                    var node = new LinkedListNode<CacheEntry>(new CacheEntry
                    {
                        Key = cacheKey,
                        PdfPath = pdfPath,
                        Image = bitmapSource,
                    });
                    CacheLru.AddFirst(node);
                    CacheMap[cacheKey] = node;

                    while (CacheMap.Count > MaxCacheEntries && CacheLru.Last != null)
                    {
                        LinkedListNode<CacheEntry> oldest = CacheLru.Last;
                        CacheLru.RemoveLast();
                        CacheMap.Remove(oldest.Value.Key);
                    }
                }
            }

            return bitmapSource;
        }

        public static Bitmap RenderPageToDrawingBitmap(string pdfPath, int pageIndex, int dpi)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath) || pageIndex < 0)
            {
                return null;
            }

            dpi = Math.Max(36, Math.Min(600, dpi));
            var (wPt, hPt) = GetPageSizePoints(pdfPath, pageIndex);
            int targetW = Math.Max(32, Math.Min(4500, (int)Math.Round(wPt * dpi / 72.0)));
            int targetH = Math.Max(32, Math.Min(4500, (int)Math.Round(hPt * dpi / 72.0)));

            byte[] bgra;
            int width;
            int height;

            int dimOne = Math.Min(targetW, targetH);
            int dimTwo = Math.Max(targetW, targetH);

            lock (RenderLock)
            {
                using (var reader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(dimOne, dimTwo)))
                {
                    if (pageIndex >= reader.GetPageCount())
                    {
                        return null;
                    }

                    using (var pageReader = reader.GetPageReader(pageIndex))
                    {
                        width = pageReader.GetPageWidth();
                        height = pageReader.GetPageHeight();
                        bgra = pageReader.GetImage();
                    }
                }
            }

            if (bgra == null || width <= 0 || height <= 0)
            {
                return null;
            }

            CompositeOverWhiteInPlace(bgra);

            Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            bmp.SetResolution(dpi, dpi);

            BitmapData bmpData = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int srcStride = width * 4;
                for (int y = 0; y < height; y++)
                {
                    IntPtr destRow = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);
                    Marshal.Copy(bgra, y * srcStride, destRow, srcStride);
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

        public static byte[] RenderPageToPngBytes(string pdfPath, int pageIndex, int dpi)
        {
            using (Bitmap bmp = RenderPageToDrawingBitmap(pdfPath, pageIndex, dpi))
            {
                if (bmp == null)
                {
                    return null;
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        public static void ClearCache(string pdfPath = null)
        {
            lock (CacheMap)
            {
                if (string.IsNullOrEmpty(pdfPath))
                {
                    CacheMap.Clear();
                    CacheLru.Clear();
                    return;
                }

                LinkedListNode<CacheEntry> current = CacheLru.First;
                while (current != null)
                {
                    LinkedListNode<CacheEntry> next = current.Next;
                    if (string.Equals(current.Value.PdfPath, pdfPath, StringComparison.OrdinalIgnoreCase))
                    {
                        CacheLru.Remove(current);
                        CacheMap.Remove(current.Value.Key);
                    }

                    current = next;
                }
            }
        }

        private static void CompositeOverWhiteInPlace(byte[] bgra)
        {
            for (int i = 0; i + 3 < bgra.Length; i += 4)
            {
                byte a = bgra[i + 3];
                if (a == 255)
                {
                    continue;
                }

                if (a == 0)
                {
                    bgra[i] = 255;
                    bgra[i + 1] = 255;
                    bgra[i + 2] = 255;
                    bgra[i + 3] = 255;
                    continue;
                }

                int invA = 255 - a;
                bgra[i] = (byte)Math.Min(255, ((bgra[i] * a) + (255 * invA)) / 255);
                bgra[i + 1] = (byte)Math.Min(255, ((bgra[i + 1] * a) + (255 * invA)) / 255);
                bgra[i + 2] = (byte)Math.Min(255, ((bgra[i + 2] * a) + (255 * invA)) / 255);
                bgra[i + 3] = 255;
            }
        }

        private sealed class CacheEntry
        {
            public string Key { get; set; }

            public string PdfPath { get; set; }

            public BitmapSource Image { get; set; }
        }
    }
}
