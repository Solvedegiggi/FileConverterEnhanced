// <copyright file="ConversionJobFactory.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;

    public static class ConversionJobFactory
    {
        public static ConversionJob Create(ConversionPreset conversionPreset, string inputFilePath)
        {
            string inputFileExtension = System.IO.Path.GetExtension(inputFilePath);
            if (!string.IsNullOrEmpty(inputFileExtension))
            {
                inputFileExtension = inputFileExtension.ToLowerInvariant().Substring(1, inputFileExtension.Length - 1);
            }
            else
            {
                inputFileExtension = string.Empty;
            }

            if (inputFileExtension == "cda")
            {
                return new ConversionJob_ExtractCDA(conversionPreset, inputFilePath);
            }

            if (inputFileExtension == "docx" || inputFileExtension == "odt" || inputFileExtension == "doc")
            {
                return new ConversionJob_Word(conversionPreset, inputFilePath);
            }

            if (inputFileExtension == "xlsx" || inputFileExtension == "ods" || inputFileExtension == "xls")
            {
                return new ConversionJob_Excel(conversionPreset, inputFilePath);
            }

            if (inputFileExtension == "pptx" || inputFileExtension == "odp" || inputFileExtension == "ppt")
            {
                return new ConversionJob_PowerPoint(conversionPreset, inputFilePath);
            }

            if (inputFileExtension == "html" || inputFileExtension == "htm")
            {
                return new ConversionJob_HtmlToPdf(conversionPreset, inputFilePath);
            }

            if (conversionPreset.OutputType == OutputType.Md)
            {
                return new ConversionJob_PdfToMarkdown(conversionPreset, inputFilePath);
            }

            if (conversionPreset.OutputType == OutputType.Docx)
            {
                return new ConversionJob_PdfToWord(conversionPreset, inputFilePath);
            }

            if (conversionPreset.OutputType == OutputType.Pptx)
            {
                return new ConversionJob_PdfToPowerPoint(conversionPreset, inputFilePath);
            }

            if (conversionPreset.OutputType == OutputType.Xlsx)
            {
                return new ConversionJob_PdfToExcel(conversionPreset, inputFilePath);
            }

            if (conversionPreset.OutputType == OutputType.Ico)
            {
                return new ConversionJob_Ico(conversionPreset, inputFilePath);
            }

            if (conversionPreset.OutputType == OutputType.Gif)
            {
                return new ConversionJob_Gif(conversionPreset, inputFilePath);
            }

            if (inputFileExtension == "pdf" &&
                (conversionPreset.OutputType == OutputType.Jpg || conversionPreset.OutputType == OutputType.Png))
            {
                return new ConversionJob_PdfToImage(conversionPreset, inputFilePath);
            }

            if (conversionPreset.OutputType == OutputType.Pdf)
            {
                if (inputFileExtension == "pdf")
                {
                    string pdfMode = conversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode) ?? "None";
                    if (string.Equals(pdfMode, "Compress", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ConversionJob_PdfCompress(conversionPreset, inputFilePath);
                    }

                    if (string.Equals(pdfMode, "Rotate", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ConversionJob_PdfRotate(conversionPreset, inputFilePath);
                    }

                    if (string.Equals(pdfMode, "SplitAllPages", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(pdfMode, "SplitEachPage", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(pdfMode, "ExtractRange", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(pdfMode, "SplitEveryN", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(pdfMode, "SplitByRanges", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ConversionJob_PdfSplit(conversionPreset, inputFilePath);
                    }

                    if (string.Equals(pdfMode, "PageNumbers", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ConversionJob_PdfPageNumbers(conversionPreset, inputFilePath);
                    }

                    if (string.Equals(pdfMode, "Ocr", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ConversionJob_PdfOcr(conversionPreset, inputFilePath);
                    }

                    if (string.Equals(pdfMode, "Merge", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ConversionJob_PdfMerge(conversionPreset, inputFilePath);
                    }
                }

                if (Helpers.GetExtensionCategory(inputFileExtension) == Helpers.InputCategoryNames.Image)
                {
                    return new ConversionJob_ImageToPdf(conversionPreset, inputFilePath);
                }

                return new ConversionJob_ImageMagick(conversionPreset, inputFilePath);
            }

            if (conversionPreset.OutputType == OutputType.Avif ||
                conversionPreset.OutputType == OutputType.Jpg ||
                conversionPreset.OutputType == OutputType.Png ||
                conversionPreset.OutputType == OutputType.Webp)
            {
                return new ConversionJob_ImageMagick(conversionPreset, inputFilePath);
            }

            return new ConversionJob_FFMPEG(conversionPreset, inputFilePath);
        }

        public static IReadOnlyList<ConversionJob> Create(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
        {
            if (conversionPreset == null)
            {
                throw new ArgumentNullException(nameof(conversionPreset));
            }

            if (inputFilePaths == null || inputFilePaths.Count == 0)
            {
                return Array.Empty<ConversionJob>();
            }

            if (conversionPreset.OutputType == OutputType.Pdf)
            {
                string pdfMode = conversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.PdfOperationMode) ?? "None";
                string imageMerge = conversionPreset.GetSettingsValue(ConversionPreset.ConversionSettingKeys.ImageToPdfMerge) ?? "False";

                bool allPdfs = true;
                bool allImages = true;
                for (int i = 0; i < inputFilePaths.Count; i++)
                {
                    string ext = System.IO.Path.GetExtension(inputFilePaths[i]).TrimStart('.').ToLowerInvariant();
                    if (ext != "pdf")
                    {
                        allPdfs = false;
                    }

                    if (Helpers.GetExtensionCategory(ext) != Helpers.InputCategoryNames.Image)
                    {
                        allImages = false;
                    }
                }

                if (allPdfs && string.Equals(pdfMode, "Merge", StringComparison.OrdinalIgnoreCase))
                {
                    return new ConversionJob[] { new ConversionJob_PdfMerge(conversionPreset, inputFilePaths) };
                }

                if (allImages && string.Equals(imageMerge, "True", StringComparison.OrdinalIgnoreCase))
                {
                    return new ConversionJob[] { new ConversionJob_ImageToPdf(conversionPreset, inputFilePaths) };
                }
            }

            List<ConversionJob> jobs = new List<ConversionJob>(inputFilePaths.Count);
            for (int index = 0; index < inputFilePaths.Count; index++)
            {
                jobs.Add(Create(conversionPreset, inputFilePaths[index]));
            }

            return jobs;
        }
    }
}
