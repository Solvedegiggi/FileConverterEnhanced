// <copyright file="PdfEditModels.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Models
{
    using System;
    using System.Collections.Generic;

    public enum PdfEditToolMode
    {
        SelectAndEditText,
        AddText,
        AddImage,
        Rectangle,
        Ellipse,
        Line,
        Arrow,
        Highlight,
        Freehand,
    }

    public enum PdfEditOperationKind
    {
        AddText,
        AddImage,
        Rectangle,
        Ellipse,
        Line,
        Arrow,
        Highlight,
        Freehand,
        RedactAndReplaceText,
    }

    public sealed class PdfTextBlockInfo
    {
        public int PageIndex { get; set; }

        public string Text { get; set; }

        public string FontName { get; set; }

        public double FontSizePt { get; set; }

        // Normalized top-left coordinates [0..1] for WPF Canvas display
        public double NormX { get; set; }

        public double NormY { get; set; }

        public double NormWidth { get; set; }

        public double NormHeight { get; set; }

        // PDF page user-space coordinates (bottom-left origin, in points)
        public double PdfLeft { get; set; }

        public double PdfBottom { get; set; }

        public double PdfRight { get; set; }

        public double PdfTop { get; set; }
    }

    public sealed class PdfEditPoint
    {
        public double NormX { get; set; }

        public double NormY { get; set; }
    }

    public sealed class PdfEditOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public int PageIndex { get; set; }

        public PdfEditOperationKind Kind { get; set; }

        public double NormX { get; set; }

        public double NormY { get; set; }

        public double NormWidth { get; set; }

        public double NormHeight { get; set; }

        public double NormX2 { get; set; }

        public double NormY2 { get; set; }

        public string Text { get; set; }

        public string OriginalText { get; set; }

        public string ImagePath { get; set; }

        public string FontFamily { get; set; } = "Arial";

        public double FontSizePt { get; set; } = 12.0;

        public string ColorHex { get; set; } = "#111827";

        public string BackgroundColorHex { get; set; } = "#FFFFFF";

        public double StrokeWidth { get; set; } = 2.0;

        public List<PdfEditPoint> Points { get; set; } = new List<PdfEditPoint>();

        public double PdfLeft { get; set; }

        public double PdfBottom { get; set; }

        public double PdfRight { get; set; }

        public double PdfTop { get; set; }
    }
}
