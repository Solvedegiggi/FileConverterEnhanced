// <copyright file="NativeOfficeToPdfConverter.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;

    using PdfSharp.Drawing;
    using PdfSharp.Pdf;

    using Drawing2D = DocumentFormat.OpenXml.Drawing;
    using Pres = DocumentFormat.OpenXml.Presentation;
    using Spread = DocumentFormat.OpenXml.Spreadsheet;
    using Word = DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// Pure C# OpenXML -> PDF converter for .docx, .xlsx, and .pptx without requiring MS Office or LibreOffice.
    /// </summary>
    public static class NativeOfficeToPdfConverter
    {
        private const double MarginPt = 45.0;

        public static void ConvertDocxToPdf(string docxPath, string outputPdfPath)
        {
            WindowsFontResolver.EnsureInitialized();

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(docxPath, false))
            using (PdfDocument pdfDoc = new PdfDocument())
            {
                MainDocumentPart mainPart = wordDoc.MainDocumentPart;
                Word.Body body = mainPart?.Document?.Body;

                double pageW = 595.28;
                double pageH = 841.89;
                double contentW = pageW - (MarginPt * 2);

                PdfPage currentPage = pdfDoc.AddPage();
                currentPage.Width = XUnit.FromPoint(pageW);
                currentPage.Height = XUnit.FromPoint(pageH);
                XGraphics gfx = XGraphics.FromPdfPage(currentPage);
                double y = MarginPt;

                void EnsureSpace(double neededHeight)
                {
                    if (y + neededHeight > pageH - MarginPt)
                    {
                        gfx.Dispose();
                        currentPage = pdfDoc.AddPage();
                        currentPage.Width = XUnit.FromPoint(pageW);
                        currentPage.Height = XUnit.FromPoint(pageH);
                        gfx = XGraphics.FromPdfPage(currentPage);
                        y = MarginPt;
                    }
                }

                try
                {
                    if (body != null)
                    {
                        foreach (OpenXmlElement element in body.Elements())
                        {
                            if (element is Word.Paragraph para)
                            {
                                string styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
                                double fontSize = 11.0;
                                XFontStyleEx fontStyle = XFontStyleEx.Regular;
                                double spaceBefore = 0;
                                double spaceAfter = 6;

                                if (styleId.StartsWith("Heading1", StringComparison.OrdinalIgnoreCase) || styleId == "1")
                                {
                                    fontSize = 18.0;
                                    fontStyle = XFontStyleEx.Bold;
                                    spaceBefore = 8;
                                    spaceAfter = 8;
                                }
                                else if (styleId.StartsWith("Heading2", StringComparison.OrdinalIgnoreCase) || styleId == "2")
                                {
                                    fontSize = 14.5;
                                    fontStyle = XFontStyleEx.Bold;
                                    spaceBefore = 6;
                                    spaceAfter = 6;
                                }
                                else if (styleId.StartsWith("Heading3", StringComparison.OrdinalIgnoreCase) || styleId == "3")
                                {
                                    fontSize = 12.5;
                                    fontStyle = XFontStyleEx.Bold;
                                    spaceBefore = 4;
                                    spaceAfter = 4;
                                }
                                else
                                {
                                    bool allBold = para.Elements<Word.Run>().Any() &&
                                                   para.Elements<Word.Run>().All(r => r.RunProperties?.Bold != null);
                                    bool allItalic = para.Elements<Word.Run>().Any() &&
                                                     para.Elements<Word.Run>().All(r => r.RunProperties?.Italic != null);
                                    if (allBold && allItalic)
                                    {
                                        fontStyle = XFontStyleEx.BoldItalic;
                                    }
                                    else if (allBold)
                                    {
                                        fontStyle = XFontStyleEx.Bold;
                                    }
                                    else if (allItalic)
                                    {
                                        fontStyle = XFontStyleEx.Italic;
                                    }
                                }

                                // Draw inline images in paragraph
                                foreach (Drawing2D.Blip blip in para.Descendants<Drawing2D.Blip>())
                                {
                                    string relId = blip.Embed?.Value;
                                    if (!string.IsNullOrEmpty(relId))
                                    {
                                        DrawOpenXmlImagePart(mainPart.GetPartById(relId) as ImagePart, ref y, contentW, EnsureSpace, () => gfx);
                                    }
                                }

                                string text = ExtractParagraphText(para);
                                if (para.ParagraphProperties?.NumberingProperties != null && !string.IsNullOrEmpty(text))
                                {
                                    text = "• " + text;
                                }

                                if (string.IsNullOrEmpty(text))
                                {
                                    y += 8;
                                    continue;
                                }

                                y += spaceBefore;
                                var font = new XFont("Calibri", fontSize, fontStyle);
                                double lineHeight = fontSize * 1.38;
                                List<string> wrapped = WrapText(gfx, text, font, contentW);

                                foreach (string line in wrapped)
                                {
                                    EnsureSpace(lineHeight);
                                    gfx.DrawString(line, font, XBrushes.Black, new XPoint(MarginPt, y), XStringFormats.TopLeft);
                                    y += lineHeight;
                                }

                                y += spaceAfter;
                            }
                            else if (element is Word.Table table)
                            {
                                y += 4;
                                var cellFont = new XFont("Calibri", 10.0, XFontStyleEx.Regular);
                                var borderPen = new XPen(XColor.FromArgb(180, 180, 180), 0.6);

                                foreach (Word.TableRow row in table.Elements<Word.TableRow>())
                                {
                                    List<Word.TableCell> cells = row.Elements<Word.TableCell>().ToList();
                                    if (cells.Count == 0)
                                    {
                                        continue;
                                    }

                                    double colW = contentW / cells.Count;
                                    double rowH = 20.0;
                                    var cellLines = new List<List<string>>(cells.Count);

                                    foreach (Word.TableCell cell in cells)
                                    {
                                        string cellText = string.Join(" ", cell.Elements<Word.Paragraph>().Select(ExtractParagraphText)).Trim();
                                        List<string> wrapped = WrapText(gfx, cellText, cellFont, Math.Max(20, colW - 8));
                                        cellLines.Add(wrapped);
                                        rowH = Math.Max(rowH, (wrapped.Count * 14.0) + 8.0);
                                    }

                                    EnsureSpace(rowH);

                                    for (int c = 0; c < cells.Count; c++)
                                    {
                                        double x = MarginPt + (c * colW);
                                        gfx.DrawRectangle(borderPen, x, y, colW, rowH);

                                        double cy = y + 4;
                                        foreach (string line in cellLines[c])
                                        {
                                            gfx.DrawString(line, cellFont, XBrushes.Black, new XPoint(x + 4, cy), XStringFormats.TopLeft);
                                            cy += 14.0;
                                        }
                                    }

                                    y += rowH;
                                }

                                y += 8;
                            }
                        }
                    }
                }
                finally
                {
                    gfx?.Dispose();
                }

                pdfDoc.Save(outputPdfPath);
            }
        }

        public static void ConvertXlsxToPdf(string xlsxPath, string outputPdfPath, string orientation, bool fitColumnsToPage)
        {
            WindowsFontResolver.EnsureInitialized();

            using (SpreadsheetDocument spreadsheet = SpreadsheetDocument.Open(xlsxPath, false))
            using (PdfDocument pdfDoc = new PdfDocument())
            {
                WorkbookPart wbPart = spreadsheet.WorkbookPart;
                Spread.SharedStringTable sst = wbPart?.SharedStringTablePart?.SharedStringTable;
                IEnumerable<Spread.Sheet> sheets = wbPart?.Workbook?.Sheets?.Elements<Spread.Sheet>() ?? Enumerable.Empty<Spread.Sheet>();

                foreach (Spread.Sheet sheet in sheets)
                {
                    WorksheetPart wsPart = wbPart.GetPartById(sheet.Id) as WorksheetPart;
                    if (wsPart?.Worksheet == null)
                    {
                        continue;
                    }

                    List<List<string>> grid = ExtractSpreadsheetGrid(wsPart.Worksheet, sst, out int maxCols);
                    if (maxCols == 0)
                    {
                        maxCols = 1;
                    }

                    bool landscape;
                    if (string.Equals(orientation, "Landscape", StringComparison.OrdinalIgnoreCase))
                    {
                        landscape = true;
                    }
                    else if (string.Equals(orientation, "Portrait", StringComparison.OrdinalIgnoreCase))
                    {
                        landscape = false;
                    }
                    else
                    {
                        landscape = maxCols >= 6 || fitColumnsToPage;
                    }

                    double pageW = landscape ? 841.89 : 595.28;
                    double pageH = landscape ? 595.28 : 841.89;
                    double printableW = pageW - (MarginPt * 2);

                    double[] colWidths = new double[maxCols];
                    for (int c = 0; c < maxCols; c++)
                    {
                        int maxLen = 6;
                        for (int r = 0; r < grid.Count && r < 200; r++)
                        {
                            if (c < grid[r].Count && grid[r][c] != null)
                            {
                                maxLen = Math.Max(maxLen, Math.Min(40, grid[r][c].Length));
                            }
                        }

                        colWidths[c] = Math.Max(42.0, (maxLen * 5.6) + 12.0);
                    }

                    double fontSize = 9.0;
                    double totalNaturalWidth = colWidths.Sum();
                    if (fitColumnsToPage && totalNaturalWidth > printableW)
                    {
                        double ratio = printableW / totalNaturalWidth;
                        for (int c = 0; c < maxCols; c++)
                        {
                            colWidths[c] = Math.Max(18.0, colWidths[c] * ratio);
                        }

                        double sumAfter = colWidths.Sum();
                        if (sumAfter > printableW)
                        {
                            double fix = printableW / sumAfter;
                            for (int c = 0; c < maxCols; c++)
                            {
                                colWidths[c] *= fix;
                            }
                        }

                        fontSize = Math.Max(6.0, Math.Min(9.0, 9.0 * ratio * 1.15));
                    }

                    var titleFont = new XFont("Calibri", 12.0, XFontStyleEx.Bold);
                    var headerFont = new XFont("Calibri", fontSize, XFontStyleEx.Bold);
                    var cellFont = new XFont("Calibri", fontSize, XFontStyleEx.Regular);
                    var borderPen = new XPen(XColor.FromArgb(200, 200, 200), 0.5);
                    var headerBrush = new XSolidBrush(XColor.FromArgb(238, 242, 248));

                    double rowH = Math.Max(15.0, fontSize * 1.9);

                    PdfPage page = pdfDoc.AddPage();
                    page.Width = XUnit.FromPoint(pageW);
                    page.Height = XUnit.FromPoint(pageH);
                    XGraphics gfx = XGraphics.FromPdfPage(page);
                    double y = MarginPt;

                    try
                    {
                        string sheetName = sheet.Name?.Value ?? "Sheet";
                        gfx.DrawString(sheetName, titleFont, XBrushes.Black, new XPoint(MarginPt, y), XStringFormats.TopLeft);
                        y += 22.0;

                        for (int r = 0; r < grid.Count; r++)
                        {
                            if (y + rowH > pageH - MarginPt)
                            {
                                gfx.Dispose();
                                page = pdfDoc.AddPage();
                                page.Width = XUnit.FromPoint(pageW);
                                page.Height = XUnit.FromPoint(pageH);
                                gfx = XGraphics.FromPdfPage(page);
                                y = MarginPt;
                            }

                            List<string> rowVals = grid[r];
                            double x = MarginPt;
                            XFont rowFont = r == 0 ? headerFont : cellFont;

                            for (int c = 0; c < maxCols; c++)
                            {
                                double cw = colWidths[c];
                                if (!fitColumnsToPage && x + cw > pageW - (MarginPt * 0.5))
                                {
                                    break;
                                }

                                if (r == 0)
                                {
                                    gfx.DrawRectangle(borderPen, headerBrush, x, y, cw, rowH);
                                }
                                else
                                {
                                    gfx.DrawRectangle(borderPen, x, y, cw, rowH);
                                }

                                string val = c < rowVals.Count ? (rowVals[c] ?? string.Empty) : string.Empty;
                                string fitted = EllipsizeToWidth(gfx, val, rowFont, Math.Max(4, cw - 6));
                                gfx.DrawString(fitted, rowFont, XBrushes.Black, new XRect(x + 3, y + 2, Math.Max(4, cw - 6), rowH - 2), XStringFormats.CenterLeft);

                                x += cw;
                            }

                            y += rowH;
                        }
                    }
                    finally
                    {
                        gfx?.Dispose();
                    }
                }

                if (pdfDoc.PageCount == 0)
                {
                    PdfPage blank = pdfDoc.AddPage();
                    blank.Width = XUnit.FromPoint(595.28);
                    blank.Height = XUnit.FromPoint(841.89);
                }

                pdfDoc.Save(outputPdfPath);
            }
        }

        public static void ConvertPptxToPdf(string pptxPath, string outputPdfPath)
        {
            WindowsFontResolver.EnsureInitialized();

            using (PresentationDocument presDoc = PresentationDocument.Open(pptxPath, false))
            using (PdfDocument pdfDoc = new PdfDocument())
            {
                PresentationPart presPart = presDoc.PresentationPart;
                Pres.SlideSize slideSize = presPart?.Presentation?.SlideSize;

                // 1 EMU = 1 / 12700 pt
                double slideW = slideSize?.Cx != null ? slideSize.Cx.Value / 12700.0 : 960.0;
                double slideH = slideSize?.Cy != null ? slideSize.Cy.Value / 12700.0 : 540.0;
                if (slideW < 100 || slideH < 100)
                {
                    slideW = 960.0;
                    slideH = 540.0;
                }

                IEnumerable<Pres.SlideId> slideIds =
                    presPart?.Presentation?.SlideIdList?.Elements<Pres.SlideId>() ?? Enumerable.Empty<Pres.SlideId>();

                foreach (Pres.SlideId slideId in slideIds)
                {
                    SlidePart slidePart = presPart.GetPartById(slideId.RelationshipId) as SlidePart;
                    if (slidePart == null)
                    {
                        continue;
                    }

                    PdfPage page = pdfDoc.AddPage();
                    page.Width = XUnit.FromPoint(slideW);
                    page.Height = XUnit.FromPoint(slideH);

                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    {
                        gfx.DrawRectangle(XBrushes.White, 0, 0, slideW, slideH);

                        Pres.ShapeTree tree = slidePart.Slide?.CommonSlideData?.ShapeTree;
                        if (tree != null)
                        {
                            foreach (OpenXmlElement child in tree.Elements())
                            {
                                if (child is Pres.Picture pic)
                                {
                                    Drawing2D.Transform2D xfrm = pic.ShapeProperties?.Transform2D;
                                    string embedId = pic.BlipFill?.Blip?.Embed?.Value;
                                    if (xfrm?.Offset != null && xfrm?.Extents != null && !string.IsNullOrEmpty(embedId))
                                    {
                                        ImagePart imgPart = slidePart.GetPartById(embedId) as ImagePart;
                                        if (imgPart != null)
                                        {
                                            double px = (xfrm.Offset.X ?? 0) / 12700.0;
                                            double py = (xfrm.Offset.Y ?? 0) / 12700.0;
                                            double pw = (xfrm.Extents.Cx ?? 0) / 12700.0;
                                            double ph = (xfrm.Extents.Cy ?? 0) / 12700.0;

                                            if (pw > 1 && ph > 1)
                                            {
                                                using (Stream s = imgPart.GetStream())
                                                using (var ms = new MemoryStream())
                                                {
                                                    s.CopyTo(ms);
                                                    ms.Position = 0;
                                                    using (XImage xImg = XImage.FromStream(ms))
                                                    {
                                                        gfx.DrawImage(xImg, px, py, pw, ph);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (child is Pres.Shape shape && shape.TextBody != null)
                                {
                                    Drawing2D.Transform2D xfrm = shape.ShapeProperties?.Transform2D;
                                    double sx = xfrm?.Offset?.X != null ? xfrm.Offset.X.Value / 12700.0 : 48.0;
                                    double sy = xfrm?.Offset?.Y != null ? xfrm.Offset.Y.Value / 12700.0 : 48.0;
                                    double sw = xfrm?.Extents?.Cx != null ? xfrm.Extents.Cx.Value / 12700.0 : slideW - 96.0;

                                    bool isTitle = string.Equals(
                                        shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value.ToString(),
                                        "Title",
                                        StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(
                                        shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value.ToString(),
                                        "CenteredTitle",
                                        StringComparison.OrdinalIgnoreCase);

                                    double curY = sy;
                                    foreach (Drawing2D.Paragraph dp in shape.TextBody.Elements<Drawing2D.Paragraph>())
                                    {
                                        string lineText = string.Concat(dp.Descendants<Drawing2D.Text>().Select(t => t.Text)).Trim();
                                        if (string.IsNullOrEmpty(lineText))
                                        {
                                            curY += 10;
                                            continue;
                                        }

                                        double ptSize = isTitle ? 28.0 : 16.0;
                                        Drawing2D.Run firstRun = dp.Elements<Drawing2D.Run>().FirstOrDefault();
                                        if (firstRun?.RunProperties?.FontSize != null)
                                        {
                                            ptSize = firstRun.RunProperties.FontSize.Value / 100.0;
                                        }

                                        ptSize = Math.Max(9.0, Math.Min(54.0, ptSize));
                                        bool bold = isTitle || (firstRun?.RunProperties?.Bold?.Value ?? false);
                                        var font = new XFont("Calibri", ptSize, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);

                                        List<string> wrapped = WrapText(gfx, lineText, font, Math.Max(40, sw));
                                        foreach (string wLine in wrapped)
                                        {
                                            gfx.DrawString(wLine, font, XBrushes.Black, new XPoint(sx, curY), XStringFormats.TopLeft);
                                            curY += ptSize * 1.35;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (pdfDoc.PageCount == 0)
                {
                    PdfPage blank = pdfDoc.AddPage();
                    blank.Width = XUnit.FromPoint(slideW);
                    blank.Height = XUnit.FromPoint(slideH);
                }

                pdfDoc.Save(outputPdfPath);
            }
        }

        private static string ExtractParagraphText(Word.Paragraph para)
        {
            var sb = new StringBuilder();
            foreach (OpenXmlElement child in para.Descendants())
            {
                if (child is Word.Text t)
                {
                    sb.Append(t.Text);
                }
                else if (child is Word.TabChar)
                {
                    sb.Append("    ");
                }
            }

            return sb.ToString();
        }

        private static void DrawOpenXmlImagePart(
            ImagePart imagePart,
            ref double y,
            double maxWidth,
            Action<double> ensureSpace,
            Func<XGraphics> getGfx)
        {
            if (imagePart == null)
            {
                return;
            }

            try
            {
                using (Stream s = imagePart.GetStream())
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    ms.Position = 0;
                    using (XImage xImg = XImage.FromStream(ms))
                    {
                        double iw = xImg.PointWidth > 0 ? xImg.PointWidth : 200;
                        double ih = xImg.PointHeight > 0 ? xImg.PointHeight : 150;
                        double scale = Math.Min(1.0, maxWidth / iw);
                        double dw = iw * scale;
                        double dh = ih * scale;
                        if (dh > 500)
                        {
                            double hScale = 500.0 / dh;
                            dw *= hScale;
                            dh *= hScale;
                        }

                        ensureSpace(dh + 8);
                        getGfx().DrawImage(xImg, MarginPt, y, dw, dh);
                        y += dh + 8;
                    }
                }
            }
            catch
            {
            }
        }

        private static List<List<string>> ExtractSpreadsheetGrid(Spread.Worksheet ws, Spread.SharedStringTable sst, out int maxCols)
        {
            var rows = new List<List<string>>();
            maxCols = 0;

            Spread.SheetData sheetData = ws.GetFirstChild<Spread.SheetData>();
            if (sheetData == null)
            {
                return rows;
            }

            foreach (Spread.Row row in sheetData.Elements<Spread.Row>())
            {
                var rowCells = new List<string>();
                int colCursor = 0;

                foreach (Spread.Cell cell in row.Elements<Spread.Cell>())
                {
                    int targetCol = GetColumnIndexFromRef(cell.CellReference?.Value, colCursor);
                    while (rowCells.Count < targetCol)
                    {
                        rowCells.Add(string.Empty);
                    }

                    rowCells.Add(GetCellStringValue(cell, sst));
                    colCursor = rowCells.Count;
                }

                maxCols = Math.Max(maxCols, rowCells.Count);
                rows.Add(rowCells);
            }

            return rows;
        }

        private static int GetColumnIndexFromRef(string cellRef, int fallback)
        {
            if (string.IsNullOrEmpty(cellRef))
            {
                return fallback;
            }

            int col = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char ch = char.ToUpperInvariant(cellRef[i]);
                if (ch < 'A' || ch > 'Z')
                {
                    break;
                }

                col = (col * 26) + (ch - 'A' + 1);
            }

            return col > 0 ? col - 1 : fallback;
        }

        private static string GetCellStringValue(Spread.Cell cell, Spread.SharedStringTable sst)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            if (cell.DataType != null && cell.DataType.Value == Spread.CellValues.InlineString)
            {
                return cell.InlineString?.InnerText ?? cell.InnerText ?? string.Empty;
            }

            string raw = cell.CellValue?.InnerText ?? string.Empty;
            if (cell.DataType != null && cell.DataType.Value == Spread.CellValues.SharedString &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sstIndex) &&
                sst != null)
            {
                OpenXmlElement item = sst.ElementAtOrDefault(sstIndex);
                if (item != null)
                {
                    return item.InnerText ?? string.Empty;
                }
            }

            return raw;
        }

        private static List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                result.Add(string.Empty);
                return result;
            }

            var current = new StringBuilder();
            foreach (string word in words)
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (gfx.MeasureString(candidate, font).Width <= maxWidth || current.Length == 0)
                {
                    current.Clear();
                    current.Append(candidate);
                }
                else
                {
                    result.Add(current.ToString());
                    current.Clear();
                    current.Append(word);
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }

        private static string EllipsizeToWidth(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            if (string.IsNullOrEmpty(text) || gfx.MeasureString(text, font).Width <= maxWidth)
            {
                return text ?? string.Empty;
            }

            string trimmed = text;
            while (trimmed.Length > 1 && gfx.MeasureString(trimmed + "…", font).Width > maxWidth)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            return trimmed + "…";
        }
    }
}
