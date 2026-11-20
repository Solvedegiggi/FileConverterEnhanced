# File Converter Enhanced

**File Converter Enhanced** (`v2.3.0`) is an enhanced fork of [Tichau/FileConverter](https://github.com/Tichau/FileConverter) that combines the classic **1-click Windows Explorer context menu** for audio, video, image, and document conversions with a **modern WPF Desktop Hub** and a **20-tool offline PDF & Document Suite** built in **100% C# (.NET Framework 4.8)**.

![File Converter Usage](Resources/FileConverterUsage.gif)

- **Download latest MSI installer**: [GitHub Releases (`Solvedegiggi/FileConverterEnhanced`)](https://github.com/Solvedegiggi/FileConverterEnhanced/releases)
- **Original upstream project & wiki**: [Tichau/FileConverter](https://github.com/Tichau/FileConverter) • [file-converter.io](https://file-converter.io/?from=readme.md)

---

## ✨ What's New in File Converter Enhanced (v2.3.0)

### 1. Hybrid Desktop Hub (`HubWindow`) & Light / Dark Themes
Launching `FileConverter.exe` directly (or via `--hub`) opens the new **Desktop Hub**:
- **Configurable Quick Home Grid (*Home Rapida*)**: Pin, unpin, and reorder your favorite PDF tools and custom conversion presets (`Configura griglia`). Drag and drop files directly onto any tool card to launch or pre-load it immediately.
- **Universal Converter (*Convertitore Universale*)**: Drag and drop one or more files (audio, video, images, PDFs, Office documents, HTML) or paste a web URL. Automatically filters compatible output formats and PDF operations, provides inline format settings, supports multi-file merging into a single PDF, and lets you output to the source folder or a custom folder.
- **All Tools Catalog (*Tutti gli Strumenti*)**: Categorized view of all 20 PDF & Document tools with instant search and 1-click pinning to the Home grid.
- **Dynamic Conversion Queue (*Coda Conversioni*)**: Live progress tracking, cancellation, and re-entrant background queuing.
- **Live Light & Dark Mode + Localization**: Instant theme switching (`Colors.Light.xaml` / `Colors.Dark.xaml`) and complete **English** and **Italian** UI localization (alongside 20+ existing community translations).

---

### 2. Complete 20-Tool PDF & Document Suite (100% Offline Ready)
All 20 tools work out-of-the-box using bundled C# and native x64 libraries—**no external Python or LibreOffice installation is required** (with automatic MS Office Interop or LibreOffice fallback when installed).

| # | Tool | ID (`--tool`) | Capabilities & Engine |
|---|---|---|---|
| 1 | **Merge PDF** (*Unisci PDF*) | `MergePdf` | Reorder and combine multiple PDFs losslessly (`PDFsharp`). |
| 2 | **Split PDF** (*Dividere PDF*) | `SplitPdf` | Split every page into separate PDFs, extract custom page ranges (`1-3, 5, 8-end`), or split every *N* pages. |
| 3 | **Compress PDF** (*Comprimi PDF*) | `CompressPdf` | 3 optimization profiles (*Extreme* 72 DPI, *Recommended* 150 DPI, *Low* 300 DPI) powered by bundled Ghostscript (`gswin64c.exe`). |
| 4 | **PDF to Word** (*PDF in Word*) | `PdfToWord` | Converts PDFs into editable `.docx` documents (headings, bold/italic, tables, embedded images) via `PdfPig` + `DocumentFormat.OpenXml`, plus optional MS Word Interop reflow. |
| 5 | **PDF to PowerPoint** (*PDF in PowerPoint*) | `PdfToPowerPoint` | Generates schema-valid 16:9 `.pptx` presentations with positioned editable text shapes, embedded images, and rasterized fallback for scanned slides. |
| 6 | **PDF to Excel** (*PDF in Excel*) | `PdfToExcel` | Detects tabular X/Y column alignments across PDF pages and exports typed numeric/text cells to `.xlsx` worksheets. |
| 7 | **Word to PDF** (*Word in PDF*) | `WordToPdf` | Converts `.doc`, `.docx`, `.odt` to PDF using a 3-tier cascade: MS Word Interop → LibreOffice headless → built-in pure C# OpenXML engine (`NativeOfficeToPdfConverter`). |
| 8 | **PowerPoint to PDF** (*PowerPoint in PDF*) | `PowerPointToPdf` | Converts `.ppt`, `.pptx`, `.odp` to PDF via MS PowerPoint Interop → LibreOffice → built-in OpenXML 16:9 PDF renderer. |
| 9 | **Excel to PDF** (*Excel in PDF*) | `ExcelToPdf` | Converts `.xls`, `.xlsx`, `.ods` to PDF with **Portrait / Landscape** orientation selection and **Fit all columns to page width** (`FitToPagesWide = 1`), supported in both MS Excel Interop and the built-in OpenXML engine. |
| 10 | **Edit PDF** (*Modifica PDF*) | `EditPdf` | Interactive canvas editor with **in-place editing & content-stream redaction of existing PDF text** (`PdfPig` hit-testing + `PDFsharp` `CSequence` `Tj`/`TJ` operator stripping & background patch), plus free text, images/signatures, rectangles, ellipses, lines/arrows, highlighter, freehand pen, and Undo/Redo. |
| 11 | **PDF to JPG** (*PDF in JPG*) | `PdfToJpg` | Dual mode: (1) render each PDF page to high-DPI JPG/PNG via `Docnet.Core` (PDFium), or (2) extract all embedded images inside the PDF. |
| 12 | **JPG to PDF** (*JPG in PDF*) | `JpgToPdf` | Convert JPG/PNG/WebP/BMP/TIFF/AVIF images into a single merged PDF or separate PDFs with configurable Page Size (`A4`, `Letter`, `Fit`), Orientation (`Auto`, `Portrait`, `Landscape`), and Margins (`None`, `Small`, `Big`). |
| 13 | **Rotate PDF** (*Ruotare PDF*) | `RotatePdf` | Batch lossless page rotation (`90° CW`, `180°`, `90° CCW`) on entire PDFs or custom page ranges. |
| 14 | **HTML to PDF** (*HTML in PDF*) | `HtmlToPdf` | Convert live web URLs (`https://...`) or local `.html`/`.htm` files to PDF using `Microsoft.Web.WebView2` (`PrintToPdfAsync`) with page size, orientation, background graphics, and JS render delay options. |
| 15 | **Organize PDF** (*Organizza PDF*) | `OrganizePdf` | Visual page thumbnail grid to reorder, rotate, duplicate, delete, or interleave pages from multiple PDFs and save losslessly. |
| 16 | **Page Numbers** (*Numero di pagine*) | `PageNumbersPdf` | Stamp customizable page numbers across a 3×2 position grid (`Top/Bottom` × `Left/Center/Right`) with custom format (`{n} / {total}`), font family, size, color, start page, and margins. |
| 17 | **OCR PDF** (*OCR PDF*) | `OcrPdf` | Converts scanned PDFs into searchable/selectable PDFs using `Tesseract` 5.2 with bundled offline **Italian (`ita`)** and **English (`eng`)** `tessdata_fast` models, on-demand downloader for 10+ extra languages (`%LocalAppData%\FileConverter\tessdata`), and invisible `3 Tr` PDF text layer injection. |
| 18 | **Compare PDF** (*Confronta PDF*) | `ComparePdf` | Load Document A & Document B side-by-side with synchronized zoom & scrolling, **Visual Pixel Diff** overlay (changed pixel %), **Text Diff** (LCS added/removed lines), and Markdown diff report export. |
| 19 | **Crop PDF** (*Ritaglia PDF*) | `CropPdf` | Interactive visual crop box overlay + precision millimeter margin controls (`Top`, `Bottom`, `Left`, `Right`) applied losslessly to `CropBox`/`TrimBox` on current page, custom range, or all pages. |
| 20 | **PDF to Markdown** (*PDF a Markdown*) | `PdfToMarkdown` | Converts PDFs into clean `.md` files (ideal for notes, docs, and LLMs), preserving headings (`#`, `##`, `###`), bold/italic, bullet/numbered lists, GFM Markdown tables, hyperlinks, and optional extracted images (`_assets/`). |

---

### 3. Smart Windows Explorer Context Menu (`FileConverterExtension`)
Right-clicking files in Windows Explorer gives you:
1. **Classic 1-Click Batch Presets**: Instant background conversion for audio, video, images, and documents (including new batch presets: *To Word (DOCX)*, *To PowerPoint (PPTX)*, *To Excel (XLSX)*, *To Markdown*, *Compress PDF*, *OCR PDF*, *Rotate PDF 90*, *To Pdf (HTML)*, *To Pdf (Excel Fit Columns Landscape)*).
2. **Contextual `"PDF & Document Tools"` Submenu**: Dynamically adapts to selected file extensions (e.g. *Merge PDF* when 2+ PDFs are selected, *Compare PDF* when 2 PDFs are selected, *Edit / Organize / Split / Compress / Crop / Rotate / Page Numbers / OCR / PDF to Word-PPT-Excel-JPG-Markdown* on PDFs, *Images to PDF* on images, *Word/Excel/PowerPoint/HTML to PDF* on documents) and opens the corresponding interactive workspace pre-loaded with your files.

---

### 4. Command-Line Usage (CLI)
```powershell
# Open the Desktop Hub
FileConverter.exe
FileConverter.exe --hub

# Open a specific interactive tool workspace pre-loaded with file(s)
FileConverter.exe --tool MergePdf "C:\Docs\part1.pdf" "C:\Docs\part2.pdf"
FileConverter.exe --tool EditPdf "C:\Docs\contract.pdf"
FileConverter.exe --tool ComparePdf "C:\Docs\v1.pdf" "C:\Docs\v2.pdf"
FileConverter.exe --tool UniversalConverter "C:\Docs\sheet.xlsx"

# Run a 1-click batch conversion preset
FileConverter.exe --conversion-preset "Compress PDF" "C:\Docs\large.pdf"

# Open Settings or Help window
FileConverter.exe --settings
FileConverter.exe --help
```

---

## 🛠️ Build, Test & Installer Guide

### Requirements
- **Windows 10 / 11 (x64)**
- **Visual Studio 2022** (or VS 2022 Build Tools) + **.NET SDK (8.0+ / 9.0+)** targeting `.NET Framework 4.8`
- **WiX Toolset v5** (restored automatically via NuGet during installer build)

### 1. Build the Shell Extension (`Release x64`)
```powershell
msbuild Application\FileConverterExtension\FileConverterExtension.csproj /t:Restore /p:Configuration=Release /p:Platform=x64 /v:minimal
msbuild Application\FileConverterExtension\FileConverterExtension.csproj /t:Build /p:Configuration=Release /p:Platform=x64 /v:minimal
```

### 2. Build the Main Application (`Release x64`)
```powershell
dotnet build Application\FileConverter\FileConverter.csproj -c Release -p:Platform=x64 --no-dependencies -p:NuGetAudit=false -m:1 -v:minimal
```

### 3. Run the Automated Unit Test Suite (`FileConverter.Tests`)
The solution includes an xUnit test project ([Application/FileConverter.Tests/FileConverter.Tests.csproj](Application/FileConverter.Tests/FileConverter.Tests.csproj)) with 20 end-to-end tests covering all PDF engines, Ghostscript compression, offline Tesseract OCR, in-place PDF text redaction, visual/text PDF comparison, bidirectional Office conversions, and settings v4→v5 migration:
```powershell
dotnet build Application\FileConverter.Tests\FileConverter.Tests.csproj -c Release -p:Platform=x64 --no-dependencies -p:NuGetAudit=false -m:1 -v:minimal
dotnet test Application\FileConverter.Tests\FileConverter.Tests.csproj -c Release -p:Platform=x64 --no-build --verbosity normal
```

### 4. Build the WiX 5 MSI Installer
```powershell
dotnet build Installer\Installer.wixproj -c Release -p:Platform=x64 --no-dependencies -p:NuGetAudit=false -m:1 -v:minimal
```
Output MSI: `Installer\bin\x64\Release\FileConverter-setup.msi`

### 5. Automated GitHub Releases
Pushing a version tag matching `v*` triggers [.github/workflows/release.yml](.github/workflows/release.yml), which builds the extension and app, runs the 20 automated tests, compiles the WiX 5 MSI installer, and publishes `FileConverter-<version>-x64-setup.msi` and `version.xml` to GitHub Releases:
```powershell
git tag v2.3.0
git push origin v2.3.0
```
The built-in auto-updater ([UpgradeService.cs](Application/FileConverter/Services/UpgradeService.cs)) checks `Solvedegiggi/FileConverterEnhanced` (`version.xml`) so existing fork installations can update seamlessly without being overwritten by upstream builds.

---

## 📦 Middlewares & Open-Source Libraries

File Converter Enhanced relies on the following open-source middlewares and libraries:

- **FFmpeg** (v8.0.1) — audio and video conversion engine ([ffmpeg.org](https://ffmpeg.org))
- **ImageMagick / Magick.NET** (v14.10.2) — image editing and conversion suite ([imagemagick.net](http://imagemagick.net) • [Magick.NET](https://github.com/dlemstra/Magick.NET))
- **Ghostscript** (v10.02.1) — PDF rendering and compression engine ([ghostscript.com](https://www.ghostscript.com/))
- **PDFsharp** (v6.1.1) — PDF creation, page manipulation, content-stream parsing, and vector drawing ([empira/PDFsharp](https://github.com/empira/PDFsharp))
- **UglyToad.PdfPig** (v0.1.9) — PDF text extraction, layout/table clustering, hyperlink & embedded image extraction ([UglyToad/PdfPig](https://github.com/UglyToad/PdfPig))
- **Docnet.Core / PDFium** (v2.6.0) — high-speed native PDF page rendering ([GowenGit/docnet](https://github.com/GowenGit/docnet))
- **DocumentFormat.OpenXml** (v3.1.1) — native `.docx`, `.pptx`, and `.xlsx` read/write SDK ([dotnet/Open-XML-SDK](https://github.com/dotnet/Open-XML-SDK))
- **Tesseract OCR** (v5.2.0) + `tessdata_fast` (`ita`, `eng`) — optical character recognition engine ([charlesw/tesseract](https://github.com/charlesw/tesseract) • [tesseract-ocr/tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast))
- **Microsoft.Web.WebView2** (v1.0.2792.45) — Chromium-based HTML/URL to PDF rendering ([WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/))
- **NetOffice** (v1.7.4.11) — Microsoft Office Word, Excel, and PowerPoint COM Interop ([NetOfficeFw/NetOffice](https://github.com/NetOfficeFw/NetOffice))
- **SharpShell** (v2.7.2) — Windows Explorer context menu shell extensions ([dwmkerr/sharpshell](https://github.com/dwmkerr/sharpshell))
- **Ripper** & **yeti.mmedia** — CD Audio extraction ([C-Sharp-Ripper](https://www.codeproject.com/Articles/5458/C-Sharp-Ripper))
- **Markdown.XAML** — WPF Markdown rendering ([theunrepentantgeek/Markdown.XAML](https://github.com/theunrepentantgeek/Markdown.XAML))
- **WpfAnimatedGif** — WPF animated GIF rendering ([XamlAnimatedGif/WpfAnimatedGif](https://github.com/XamlAnimatedGif/WpfAnimatedGif))

---

## 🙏 Credits & Upstream Project

This repository is an enhanced fork of [**File Converter**](https://github.com/Tichau/FileConverter) originally created and maintained by **Adrien Allard (`Tichau`)**.
- Support the original author: [PayPal Donation](https://www.paypal.com/donate/?cmd=_donations&business=3BDWQTYTTA3D8&item_name=File+Converter+Donations&currency_code=EUR&Z3JncnB0=) • [Say Thanks to Tichau](https://saythanks.io/to/Tichau)
- Report issues on this fork: [Solvedegiggi/FileConverterEnhanced Issues](https://github.com/Solvedegiggi/FileConverterEnhanced/issues)

### Localization Contributors
- **Khidreal** and **hugok79** (Portuguese)
- **Marhc** (Brazilian Portuguese)
- **Chachak** (Spanish)
- **Davide** (Italian)
- **nikotschierske** (German)
- **Snoopy1866** (Simplified Chinese)
- **MayaC0re** (Turkish)
- **vishveshjain** (Hindi)
- **Mahmoud0Sultan** (Arabic)
- **Sedimentary-Rock**, **NeKoOuO**, and **PeterDaveHello** (Traditional Chinese)
- **CrisBalGreece** (Greek)
- **AshiVered** (Hebrew)
- **MrHero118** and **Mehrdad32** (Persian)
- **crnobog69** (Serbian)
- **oogamiyuta** (Japanese)
- **AidyTheWeird** (Czech)
- **Alanimdeo** (Korean)
- **vrykolakas166** and **thaovd** (Vietnamese)
- **iliamak** (Russian)
- **itsmefdil** (Indonesian)
- **hamzaharoon1314** (Urdu)
- **Zyvrec7** and **stohlferenc** (Hungarian)
- **Maerek** and **MrPrince419** (Polish)
- **rkalitta** (Swedish)

---

## 📄 License

File Converter Enhanced is licensed under the **GNU General Public License v3.0 (GPLv3)**.
See [LICENSE.md](LICENSE.md) or [gnu.org/licenses/gpl.html](https://www.gnu.org/licenses/gpl.html) for full details.
