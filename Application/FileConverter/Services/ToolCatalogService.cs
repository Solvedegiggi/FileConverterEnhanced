namespace FileConverter.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FileConverter.Models;
    using FileConverter.Properties;

    public interface IToolCatalogService
    {
        IReadOnlyList<ToolDefinition> GetBuiltInPdfTools();

        IReadOnlyList<ToolDefinition> GetAllToolsAndPresets();

        IReadOnlyList<ToolDefinition> GetPinnedQuickTools();

        ToolDefinition FindById(string id);

        void PinTool(string id);

        void UnpinTool(string id);

        void MovePinnedTool(string id, int delta);

        bool IsPinned(string id);
    }

    public class ToolCatalogService : IToolCatalogService
    {
        private readonly ISettingsService settingsService;

        public ToolCatalogService(ISettingsService settingsService)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public IReadOnlyList<ToolDefinition> GetBuiltInPdfTools()
        {
            var pinned = this.GetPinnedSet();
            var list = new List<ToolDefinition>
            {
                CreateTool(ToolIds.MergePdf, Resources.ToolMergePdfTitle, Resources.ToolMergePdfDesc, ToolCategory.Organize, Resources.ToolCategoryOrganize, "#E5533D", "#FDECE9", "⇄", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.SplitPdf, Resources.ToolSplitPdfTitle, Resources.ToolSplitPdfDesc, ToolCategory.Organize, Resources.ToolCategoryOrganize, "#E5533D", "#FDECE9", "✂", new[] { ".pdf" }, false, false, pinned),
                CreateTool(ToolIds.CompressPdf, Resources.ToolCompressPdfTitle, Resources.ToolCompressPdfDesc, ToolCategory.Optimize, Resources.ToolCategoryOptimize, "#4F9E3C", "#EAF6E7", "⤓", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.PdfToWord, Resources.ToolPdfToWordTitle, Resources.ToolPdfToWordDesc, ToolCategory.ConvertFromPdf, Resources.ToolCategoryConvertFromPdf, "#2D73D2", "#E8F1FC", "W", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.PdfToPowerPoint, Resources.ToolPdfToPowerPointTitle, Resources.ToolPdfToPowerPointDesc, ToolCategory.ConvertFromPdf, Resources.ToolCategoryConvertFromPdf, "#E66039", "#FDEEE9", "P", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.PdfToExcel, Resources.ToolPdfToExcelTitle, Resources.ToolPdfToExcelDesc, ToolCategory.ConvertFromPdf, Resources.ToolCategoryConvertFromPdf, "#2E8B57", "#E6F4EC", "X", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.WordToPdf, Resources.ToolWordToPdfTitle, Resources.ToolWordToPdfDesc, ToolCategory.ConvertToPdf, Resources.ToolCategoryConvertToPdf, "#2D73D2", "#E8F1FC", "W→", new[] { ".doc", ".docx", ".odt" }, true, false, pinned),
                CreateTool(ToolIds.PowerPointToPdf, Resources.ToolPowerPointToPdfTitle, Resources.ToolPowerPointToPdfDesc, ToolCategory.ConvertToPdf, Resources.ToolCategoryConvertToPdf, "#E66039", "#FDEEE9", "P→", new[] { ".ppt", ".pptx", ".odp" }, true, false, pinned),
                CreateTool(ToolIds.ExcelToPdf, Resources.ToolExcelToPdfTitle, Resources.ToolExcelToPdfDesc, ToolCategory.ConvertToPdf, Resources.ToolCategoryConvertToPdf, "#2E8B57", "#E6F4EC", "X→", new[] { ".xls", ".xlsx", ".ods" }, true, false, pinned),
                CreateTool(ToolIds.EditPdf, Resources.ToolEditPdfTitle, Resources.ToolEditPdfDesc, ToolCategory.EditCompare, Resources.ToolCategoryEditCompare, "#8E44AD", "#F3EBF7", "✎", new[] { ".pdf" }, false, false, pinned),
                CreateTool(ToolIds.PdfToJpg, Resources.ToolPdfToJpgTitle, Resources.ToolPdfToJpgDesc, ToolCategory.ConvertFromPdf, Resources.ToolCategoryConvertFromPdf, "#D6A11E", "#FCF5E1", "🖼", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.JpgToPdf, Resources.ToolJpgToPdfTitle, Resources.ToolJpgToPdfDesc, ToolCategory.ConvertToPdf, Resources.ToolCategoryConvertToPdf, "#D6A11E", "#FCF5E1", "📄", new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff" }, true, false, pinned),
                CreateTool(ToolIds.RotatePdf, Resources.ToolRotatePdfTitle, Resources.ToolRotatePdfDesc, ToolCategory.Organize, Resources.ToolCategoryOrganize, "#8E44AD", "#F3EBF7", "↻", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.HtmlToPdf, Resources.ToolHtmlToPdfTitle, Resources.ToolHtmlToPdfDesc, ToolCategory.ConvertToPdf, Resources.ToolCategoryConvertToPdf, "#C99612", "#FAF3DF", "</>", new[] { ".html", ".htm" }, false, false, pinned),
                CreateTool(ToolIds.OrganizePdf, Resources.ToolOrganizePdfTitle, Resources.ToolOrganizePdfDesc, ToolCategory.Organize, Resources.ToolCategoryOrganize, "#E5533D", "#FDECE9", "▤", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.PageNumbersPdf, Resources.ToolPageNumbersPdfTitle, Resources.ToolPageNumbersPdfDesc, ToolCategory.EditCompare, Resources.ToolCategoryEditCompare, "#8E44AD", "#F3EBF7", "123", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.OcrPdf, Resources.ToolOcrPdfTitle, Resources.ToolOcrPdfDesc, ToolCategory.Optimize, Resources.ToolCategoryOptimize, "#4F9E3C", "#EAF6E7", "OCR", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.ComparePdf, Resources.ToolComparePdfTitle, Resources.ToolComparePdfDesc, ToolCategory.EditCompare, Resources.ToolCategoryEditCompare, "#2D73D2", "#E8F1FC", "◫", new[] { ".pdf" }, true, false, pinned),
                CreateTool(ToolIds.CropPdf, Resources.ToolCropPdfTitle, Resources.ToolCropPdfDesc, ToolCategory.EditCompare, Resources.ToolCategoryEditCompare, "#9B4D8E", "#F5EBF3", "⌗", new[] { ".pdf" }, false, false, pinned),
                CreateTool(ToolIds.PdfToMarkdown, Resources.ToolPdfToMarkdownTitle, Resources.ToolPdfToMarkdownDesc, ToolCategory.ConvertFromPdf, Resources.ToolCategoryConvertFromPdf, "#5E4FD6", "#ECEAFD", "{#}", new[] { ".pdf" }, true, true, pinned),
            };

            return list;
        }

        public IReadOnlyList<ToolDefinition> GetAllToolsAndPresets()
        {
            var result = new List<ToolDefinition>(this.GetBuiltInPdfTools());
            var pinned = this.GetPinnedSet();

            if (this.settingsService?.Settings?.ConversionPresets != null)
            {
                foreach (ConversionPreset preset in this.settingsService.Settings.ConversionPresets)
                {
                    string id = "preset:" + preset.FullName;
                    string category = Helpers.GetExtensionCategory(preset.OutputType.ToString().ToLowerInvariant());
                    string accent = "#0171BB";
                    string bg = "#E6F2FC";
                    string glyph = preset.OutputType.ToString().ToUpperInvariant();
                    if (category == Helpers.InputCategoryNames.Video)
                    {
                        accent = "#D94862";
                        bg = "#FDEBEF";
                    }
                    else if (category == Helpers.InputCategoryNames.Audio)
                    {
                        accent = "#0F9D58";
                        bg = "#E7F7EE";
                    }
                    else if (category == Helpers.InputCategoryNames.Image || category == Helpers.InputCategoryNames.AnimatedImage)
                    {
                        accent = "#D9822B";
                        bg = "#FDF2E6";
                    }

                    string[] accepted = preset.InputTypes != null
                        ? preset.InputTypes.Select(t => "." + t.TrimStart('.')).ToArray()
                        : Array.Empty<string>();

                    result.Add(new ToolDefinition
                    {
                        Id = id,
                        Title = preset.FullName,
                        Description = $"{string.Join(", ", preset.InputTypes ?? new List<string>())} → {preset.OutputType}",
                        Category = ToolCategory.Presets,
                        CategoryName = Resources.ToolCategoryPresets,
                        AccentColorHex = accent,
                        BadgeBgHex = bg,
                        BadgeGlyph = glyph.Length > 4 ? glyph.Substring(0, 4) : glyph,
                        IsNew = false,
                        IsPresetShortcut = true,
                        PresetFullName = preset.FullName,
                        AcceptedExtensions = accepted,
                        AllowsMultipleFiles = true,
                        IsPinned = pinned.Contains(id),
                    });
                }
            }

            return result;
        }

        public IReadOnlyList<ToolDefinition> GetPinnedQuickTools()
        {
            var all = this.GetAllToolsAndPresets().ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
            var pinnedIds = this.settingsService?.Settings?.PinnedQuickToolIds;
            var list = new List<ToolDefinition>();

            if (pinnedIds != null)
            {
                foreach (string id in pinnedIds)
                {
                    if (!string.IsNullOrEmpty(id) && all.TryGetValue(id, out ToolDefinition tool))
                    {
                        list.Add(tool);
                    }
                }
            }

            return list;
        }

        public ToolDefinition FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return this.GetAllToolsAndPresets().FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsPinned(string id)
        {
            return this.GetPinnedSet().Contains(id);
        }

        public void PinTool(string id)
        {
            if (string.IsNullOrEmpty(id) || this.settingsService?.Settings == null)
            {
                return;
            }

            var pinned = this.settingsService.Settings.PinnedQuickToolIds;
            if (!pinned.Contains(id))
            {
                pinned.Add(id);
                this.settingsService.SaveSettings();
            }
        }

        public void UnpinTool(string id)
        {
            if (string.IsNullOrEmpty(id) || this.settingsService?.Settings == null)
            {
                return;
            }

            var pinned = this.settingsService.Settings.PinnedQuickToolIds;
            if (pinned.Remove(id))
            {
                this.settingsService.SaveSettings();
            }
        }

        public void MovePinnedTool(string id, int delta)
        {
            if (string.IsNullOrEmpty(id) || this.settingsService?.Settings == null || delta == 0)
            {
                return;
            }

            var pinned = this.settingsService.Settings.PinnedQuickToolIds;
            int idx = pinned.IndexOf(id);
            if (idx < 0)
            {
                return;
            }

            int target = idx + delta;
            if (target < 0 || target >= pinned.Count)
            {
                return;
            }

            pinned.Move(idx, target);
            this.settingsService.SaveSettings();
        }

        private HashSet<string> GetPinnedSet()
        {
            var ids = this.settingsService?.Settings?.PinnedQuickToolIds;
            return ids != null
                ? new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private static ToolDefinition CreateTool(
            string id,
            string title,
            string desc,
            ToolCategory category,
            string categoryName,
            string accentColorHex,
            string badgeBgHex,
            string badgeGlyph,
            string[] acceptedExtensions,
            bool allowsMultipleFiles,
            bool isNew,
            HashSet<string> pinned)
        {
            return new ToolDefinition
            {
                Id = id,
                Title = title,
                Description = desc,
                Category = category,
                CategoryName = categoryName,
                AccentColorHex = accentColorHex,
                BadgeBgHex = badgeBgHex,
                BadgeGlyph = badgeGlyph,
                AcceptedExtensions = acceptedExtensions,
                AllowsMultipleFiles = allowsMultipleFiles,
                IsNew = isNew,
                IsPresetShortcut = false,
                IsPinned = pinned.Contains(id),
            };
        }
    }
}
