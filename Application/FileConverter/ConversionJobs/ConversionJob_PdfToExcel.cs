// <copyright file="ConversionJob_PdfToExcel.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;

    using UglyToad.PdfPig.Content;

    using PdfPigDoc = UglyToad.PdfPig.PdfDocument;

    public class ConversionJob_PdfToExcel : ConversionJob
    {
        public ConversionJob_PdfToExcel(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected override bool ValidateInputExtension => false;

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateConversion;

            if (!File.Exists(this.InputFilePath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindOutputFiles);
                return;
            }

            using (PdfPigDoc pdf = PdfPigDoc.Open(this.InputFilePath))
            using (SpreadsheetDocument spreadsheet = SpreadsheetDocument.Create(this.OutputFilePath, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart wbPart = spreadsheet.AddWorkbookPart();
                wbPart.Workbook = new Workbook();
                Sheets sheets = wbPart.Workbook.AppendChild(new Sheets());

                int pageCount = pdf.NumberOfPages;
                for (int pageNum = 1; pageNum <= pageCount; pageNum++)
                {
                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    UglyToad.PdfPig.Content.Page page = pdf.GetPage(pageNum);
                    IReadOnlyList<Word> words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();

                    WorksheetPart wsPart = wbPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    List<List<string>> tableGrid = BuildTabularGrid(words);

                    uint rowIndex = 1;
                    int maxCol = 0;
                    foreach (List<string> rowCells in tableGrid)
                    {
                        maxCol = Math.Max(maxCol, rowCells.Count);
                        var row = new Row { RowIndex = rowIndex };
                        for (int colIdx = 0; colIdx < rowCells.Count; colIdx++)
                        {
                            string val = rowCells[colIdx];
                            if (string.IsNullOrEmpty(val))
                            {
                                continue;
                            }

                            string cellRef = GetExcelColumnName(colIdx) + rowIndex.ToString(CultureInfo.InvariantCulture);
                            Cell cell;
                            if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                            {
                                cell = new Cell
                                {
                                    CellReference = cellRef,
                                    DataType = CellValues.Number,
                                    CellValue = new CellValue(num.ToString(CultureInfo.InvariantCulture)),
                                };
                            }
                            else
                            {
                                cell = new Cell
                                {
                                    CellReference = cellRef,
                                    DataType = CellValues.InlineString,
                                    InlineString = new InlineString(new Text(val)),
                                };
                            }

                            row.AppendChild(cell);
                        }

                        sheetData.AppendChild(row);
                        rowIndex++;
                    }

                    var worksheet = new Worksheet();
                    if (maxCol > 0)
                    {
                        var columns = new Columns();
                        for (uint c = 1; c <= (uint)maxCol; c++)
                        {
                            columns.AppendChild(new Column
                            {
                                Min = c,
                                Max = c,
                                Width = 20,
                                CustomWidth = true,
                            });
                        }

                        worksheet.AppendChild(columns);
                    }

                    worksheet.AppendChild(sheetData);
                    wsPart.Worksheet = worksheet;
                    wsPart.Worksheet.Save();

                    string sheetName = $"Page {pageNum}";
                    sheets.AppendChild(new Sheet
                    {
                        Id = wbPart.GetIdOfPart(wsPart),
                        SheetId = (uint)pageNum,
                        Name = sheetName,
                    });

                    this.Progress = (float)pageNum / pageCount;
                }

                wbPart.Workbook.Save();
            }
        }

        private static List<List<string>> BuildTabularGrid(IReadOnlyList<Word> words)
        {
            if (words.Count == 0)
            {
                return new List<List<string>>();
            }

            var sorted = words
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            double medianSize = sorted
                .Where(w => w.Letters.Count > 0)
                .Select(w => w.Letters[0].PointSize)
                .OrderBy(s => s)
                .DefaultIfEmpty(10.0)
                .ElementAtOrDefault(sorted.Count / 2);

            double yTol = Math.Max(3.0, medianSize * 0.45);
            var rowBuckets = new List<List<Word>>();

            foreach (Word w in sorted)
            {
                List<Word> target = rowBuckets.FirstOrDefault(r => Math.Abs(r[0].BoundingBox.Bottom - w.BoundingBox.Bottom) <= yTol);
                if (target == null)
                {
                    target = new List<Word>();
                    rowBuckets.Add(target);
                }

                target.Add(w);
            }

            // Build horizontal segments inside each row
            var segmentedRows = new List<List<(double Left, string Text)>>(rowBuckets.Count);
            var allSegmentLefts = new List<double>();

            foreach (List<Word> bucket in rowBuckets)
            {
                bucket.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

                double gapTol = Math.Max(14.0, medianSize * 1.4);
                var rowSegs = new List<(double Left, string Text)>();
                double segLeft = bucket[0].BoundingBox.Left;
                var segText = new StringBuilder(bucket[0].Text);

                for (int i = 1; i < bucket.Count; i++)
                {
                    double gap = bucket[i].BoundingBox.Left - bucket[i - 1].BoundingBox.Right;
                    if (gap > gapTol)
                    {
                        rowSegs.Add((segLeft, segText.ToString().Trim()));
                        allSegmentLefts.Add(segLeft);
                        segLeft = bucket[i].BoundingBox.Left;
                        segText.Clear();
                        segText.Append(bucket[i].Text);
                    }
                    else
                    {
                        segText.Append(' ').Append(bucket[i].Text);
                    }
                }

                rowSegs.Add((segLeft, segText.ToString().Trim()));
                allSegmentLefts.Add(segLeft);
                segmentedRows.Add(rowSegs);
            }

            // Cluster segment Left positions globally into stable X column anchors
            allSegmentLefts.Sort();
            var colAnchors = new List<double>();
            foreach (double x in allSegmentLefts)
            {
                if (colAnchors.Count == 0 || (x - colAnchors[colAnchors.Count - 1]) > 18.0)
                {
                    colAnchors.Add(x);
                }
            }

            int numCols = Math.Max(1, colAnchors.Count);
            var grid = new List<List<string>>(segmentedRows.Count);

            foreach (var rowSegs in segmentedRows)
            {
                var rowArr = new string[numCols];
                foreach (var seg in rowSegs)
                {
                    int bestCol = 0;
                    double bestDist = double.MaxValue;
                    for (int c = 0; c < colAnchors.Count; c++)
                    {
                        double d = Math.Abs(seg.Left - colAnchors[c]);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestCol = c;
                        }
                    }

                    rowArr[bestCol] = string.IsNullOrEmpty(rowArr[bestCol])
                        ? seg.Text
                        : rowArr[bestCol] + " " + seg.Text;
                }

                grid.Add(rowArr.ToList());
            }

            return grid;
        }

        private static string GetExcelColumnName(int zeroBasedColumnIndex)
        {
            int dividend = zeroBasedColumnIndex + 1;
            string colName = string.Empty;
            while (dividend > 0)
            {
                int modulo = (dividend - 1) % 26;
                colName = System.Convert.ToChar(65 + modulo) + colName;
                dividend = (dividend - modulo) / 26;
            }

            return colName;
        }
    }
}
