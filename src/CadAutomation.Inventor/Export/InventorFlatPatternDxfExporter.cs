using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CadAutomation.Core.Abstractions;
using CadAutomation.Core.Models;
using CadAutomation.Core.Services;
using CadAutomation.Inventor.Adapters;
using Inv = Inventor;

namespace CadAutomation.Inventor.Export
{
    /// <summary>
    /// IFlatPatternDxfExporter'ın gerçek Inventor implementasyonu (madde 4-5).
    /// Var olan Flat Pattern kullanılır; yoksa ve CreateFlatPattern açıksa Unfold() ile oluşturulur.
    /// DXF export, FlatPattern.DataIO.WriteDataToFile ile yapılıyor - Inventor'ın generic
    /// TranslatorAddIn.SaveCopyAs mekanizması yerine flat pattern'e özel, daha basit API.
    /// </summary>
    public sealed class InventorFlatPatternDxfExporter : IFlatPatternDxfExporter, IBatchExportFinalizer
    {
        private readonly NamingTemplateEngine _namingEngine = new NamingTemplateEngine();
        private readonly List<string> _pendingDwgScaleFiles = new List<string>();

        /// <summary>
        /// Inventor'ın flat pattern DXF çıktısına varsayılan olarak eklediği, büküm merkez çizgisinin
        /// (BEND_UP/BEND_DOWN) hemen yanında görünen referans amaçlı tanjant/feature-profile çizgileri.
        /// Kullanıcı isteği (2026-09-15): bunlar kesim yoluna karışıp hatalı kesime yol açabildiği için
        /// üretim çıktısından tamamen kaldırılmalı - sadece tek bir büküm merkez çizgisi kalmalı.
        /// </summary>
        private static readonly string[] BendReferenceLayersToStrip =
        {
            "IV_TANGENT",
            "IV_FEATURE_PROFILES",
            "IV_FEATURE_PROFILES_DOWN",
            "IV_ROLL_TANGENT",
        };

        public PartProcessResult ExportPart(UniquePart part, BatchExportOptions options)
        {
            var identifier = part.Document.DisplayName;

            if (!(part.Document is InventorCadDocument inventorDocument))
            {
                return new PartProcessResult(identifier, PartProcessStatus.Failed, "Beklenmeyen belge tipi (Inventor değil).");
            }

            var sheetMetalDefinition = inventorDocument.NativeSheetMetalDefinition;
            if (sheetMetalDefinition == null)
            {
                return new PartProcessResult(identifier, PartProcessStatus.Skipped, "Sheet Metal tanımı bulunamadı.");
            }

            Inv.FlatPattern flatPattern;
            try
            {
                if (sheetMetalDefinition.HasFlatPattern)
                {
                    flatPattern = sheetMetalDefinition.FlatPattern;
                }
                else if (options.CreateFlatPattern)
                {
                    sheetMetalDefinition.Unfold();
                    flatPattern = sheetMetalDefinition.FlatPattern;
                }
                else
                {
                    return new PartProcessResult(identifier, PartProcessStatus.Skipped, "Flat Pattern yok ve otomatik oluşturma kapalı.");
                }
            }
            catch (Exception ex)
            {
                return new PartProcessResult(identifier, PartProcessStatus.Failed, "Flat Pattern oluşturulamadı: " + ex.Message);
            }

            if (!options.ExportDxf)
            {
                return new PartProcessResult(identifier, PartProcessStatus.Success, "Flat Pattern hazır (DXF export kapalı).");
            }

            try
            {
                var targetFolder = ResolveTargetFolder(options, part.Document);
                Directory.CreateDirectory(targetFolder);

                var tokens = BuildTokens(part.Document);
                var extension = options.Format == ExportFormat.Dwg ? "dwg" : "dxf";
                var fileName = _namingEngine.BuildFileName(options.NamingTemplate, tokens, extension);
                var fullPath = Path.Combine(targetFolder, fileName);

                var translatorOptions = BuildTranslatorOptions(options);
                flatPattern.DataIO.WriteDataToFile(translatorOptions, fullPath);

                if (options.Format == ExportFormat.Dwg &&
                    options.ShowBendLines &&
                    options.LayerMapping.BendLineTypeScale > 0)
                {
                    _pendingDwgScaleFiles.Add(fullPath);
                }

                if (options.Format == ExportFormat.Dxf)
                {
                    if (options.ShowBendLines)
                    {
                        DxfLayerRemover.RemoveLayerGeometry(fullPath, BendReferenceLayersToStrip);
                    }

                    DxfLayerColorAdjuster.ApplyLayerColors(fullPath, BuildLayerColors(options));
                    DxfLayerColorAdjuster.ApplyLayerLineTypes(fullPath, BuildLayerLineTypes(options));

                    if (options.ShowBendLines)
                    {
                        DxfHeaderAdjuster.SetLineTypeScale(fullPath, options.LayerMapping.BendLineTypeScale);
                    }

                    if (options.AddPartLabel)
                    {
                        var avoidLayers = new List<string> { options.LayerMapping.OutlineLayer };
                        if (options.ShowBendLines)
                        {
                            avoidLayers.Add(options.LayerMapping.BendUpLayer);
                            avoidLayers.Add(options.LayerMapping.BendDownLayer);
                        }

                        var labelText = BuildTokens(part.Document)["PartNumber"] ?? identifier;

                        // Etiketi Inventor'ın sketch+çevirici zincirine bırakmak yerine DOĞRUDAN DXF'e
                        // yazıyoruz (bkz. DxfPartLabelPlacer.InsertLabel dokümantasyonu) - canlı testte
                        // Inventor'ın FlatPattern çevirmeninin, sketch'e eklenen metni bazı parçalarda
                        // (aynı kod, aynı belge - sadece Inventor oturumdan oturuma farklı) export'a hiç
                        // dahil etmediği tespit edildi. Bu adım hem daha güvenilir hem de RepositionLabel
                        // ile aynı "kesinlikle çakışma olmasın" garantisini taşıyor.
                        DxfPartLabelPlacer.InsertLabel(fullPath, labelText, safetyMarginMm: 2.0, avoidLayers: avoidLayers);
                    }
                }

                return new PartProcessResult(identifier, PartProcessStatus.Success, fullPath);
            }
            catch (Exception ex)
            {
                return new PartProcessResult(identifier, PartProcessStatus.Failed, "DXF export başarısız: " + ex.Message);
            }
        }

        public PartProcessResult? CompleteBatch(BatchExportOptions options)
        {
            if (_pendingDwgScaleFiles.Count == 0) return null;

            try
            {
                DwgLineTypeScaleAdjuster.Apply(
                    _pendingDwgScaleFiles,
                    options.LayerMapping.BendLineTypeScale);
                return null;
            }
            catch (Exception ex)
            {
                return new PartProcessResult(
                    "DWG çizgi ölçeği",
                    PartProcessStatus.Warning,
                    "DWG dosyaları oluşturuldu ancak kesikli çizgi ölçeği uygulanamadı: " + ex.Message);
            }
            finally
            {
                _pendingDwgScaleFiles.Clear();
            }
        }

        /// <summary>
        /// Inventor'ın FlatPattern DXF/DWG çevirmeni için "isim=değer" query-string formatında
        /// options. Layer isimleri madde 7-8'deki gereksinime göre kullanıcı tanımlı (LayerMapping);
        /// renk/preset yönetimi (Durma/Bystronic/Trumpf) sonraki fazda Settings ekranıyla eklenecek.
        /// </summary>
        private static string BuildTranslatorOptions(BatchExportOptions options)
        {
            var formatKeyword = options.Format == ExportFormat.Dwg ? "DWG" : "DXF";
            var sb = new StringBuilder("FLAT PATTERN ").Append(formatKeyword).Append('?');

            sb.Append("OuterProfileLayer=").Append(options.LayerMapping.OutlineLayer);
            sb.Append("&InteriorProfilesLayer=").Append(options.LayerMapping.OutlineLayer);
            AppendLayerColorOption(sb, "OuterProfileLayer", options.LayerMapping.OutlineColorAci);
            AppendLayerColorOption(sb, "InteriorProfilesLayer", options.LayerMapping.OutlineColorAci);

            if (options.ShowBendLines)
            {
                sb.Append("&BendUpLayer=").Append(options.LayerMapping.BendUpLayer);
                sb.Append("&BendDownLayer=").Append(options.LayerMapping.BendDownLayer);

                if (options.LayerMapping.BendLineColorAci.HasValue)
                {
                    AppendLayerColorOption(sb, "BendUpLayer", options.LayerMapping.BendLineColorAci.Value);
                    AppendLayerColorOption(sb, "BendDownLayer", options.LayerMapping.BendLineColorAci.Value);
                }

                var bendLineType = ResolveInventorLineType(options.LayerMapping.BendLineType);
                sb.Append("&BendUpLayerLineType=").Append(bendLineType);
                sb.Append("&BendDownLayerLineType=").Append(bendLineType);
            }

            // Büküm merkez çizgisinin iki yanındaki radius/tanjant sınırları lazer kesim yolu
            // değildir. DataIO'nun belgelenmiş InvisibleLayers seçeneği bunları DXF ve DWG'de
            // kaynaktan gizler. ShowBendLines kapalıysa merkez çizgileri de gizlenir.
            var invisibleLayers = new List<string>(BendReferenceLayersToStrip);
            if (!options.ShowBendLines)
            {
                invisibleLayers.Add("IV_BEND");
                invisibleLayers.Add("IV_BEND_DOWN");
            }
            sb.Append("&InvisibleLayers=").Append(string.Join(";", invisibleLayers));

            if (options.CreateMarking)
            {
                sb.Append("&EngraveLayer=").Append(options.LayerMapping.MarkingLayer);
                AppendLayerColorOption(sb, "EngraveLayer", options.LayerMapping.MarkingColorAci);
            }

            return sb.ToString();
        }

        private static void AppendLayerColorOption(StringBuilder sb, string optionName, int aciColor)
        {
            var rgb = ConvertAciToRgb(aciColor);
            sb.Append('&').Append(optionName).Append("Color=")
                .Append(rgb.R).Append(';').Append(rgb.G).Append(';').Append(rgb.B);
        }

        private static (int R, int G, int B) ConvertAciToRgb(int aciColor)
        {
            switch (aciColor)
            {
                case 1: return (255, 0, 0);       // Kırmızı
                case 2: return (255, 255, 0);     // Sarı
                case 3: return (0, 255, 0);       // Yeşil
                case 5: return (0, 0, 255);       // Mavi
                case 30: return (255, 127, 0);    // Turuncu
                case 7: return (255, 255, 255);   // Beyaz/siyah (arka plana bağlı)
                default: return (255, 255, 255);
            }
        }

        private static long ResolveInventorLineType(string lineType)
        {
            if (string.Equals(lineType, "DASHDOT", StringComparison.OrdinalIgnoreCase))
                return (long)Inv.LineTypeEnum.kDashDottedLineType;
            if (string.Equals(lineType, "DOTTED", StringComparison.OrdinalIgnoreCase))
                return (long)Inv.LineTypeEnum.kDottedLineType;

            return (long)Inv.LineTypeEnum.kDashedLineType;
        }

        /// <summary>
        /// DXF export'undan sonra doğrulama/fallback olarak uygulanacak layer->renk (ACI) eşlemesi.
        /// Renk çeviriciye de verilir; bu adım metin tabanlı DXF çıktısını kesinleştirir.
        /// </summary>
        private static IReadOnlyDictionary<string, int> BuildLayerColors(BatchExportOptions options)
        {
            var colors = new Dictionary<string, int>
            {
                [options.LayerMapping.OutlineLayer] = options.LayerMapping.OutlineColorAci,
            };

            // "Renksiz" (null) seçiliyse layer'ı sözlüğe hiç eklemiyoruz - post-process adımı
            // o layer'a dokunmuyor, Inventor'ın çevirmeninin kendi varsayılan rengi kalıyor.
            if (options.ShowBendLines && options.LayerMapping.BendLineColorAci.HasValue)
            {
                colors[options.LayerMapping.BendUpLayer] = options.LayerMapping.BendLineColorAci.Value;
                colors[options.LayerMapping.BendDownLayer] = options.LayerMapping.BendLineColorAci.Value;
            }

            if (options.CreateMarking)
            {
                colors[options.LayerMapping.MarkingLayer] = options.LayerMapping.MarkingColorAci;
            }

            return colors;
        }

        /// <summary>Büküm çizgilerinin çizgi tipini (kesikli/noktalı) ayarlamak için layer->linetype eşlemesi.</summary>
        private static IReadOnlyDictionary<string, string> BuildLayerLineTypes(BatchExportOptions options)
        {
            var lineTypes = new Dictionary<string, string>();

            if (options.ShowBendLines)
            {
                lineTypes[options.LayerMapping.BendUpLayer] = options.LayerMapping.BendLineType;
                lineTypes[options.LayerMapping.BendDownLayer] = options.LayerMapping.BendLineType;
            }

            return lineTypes;
        }

        private static string ResolveTargetFolder(BatchExportOptions options, ICadDocument document)
        {
            if (!options.FolderByThickness || !document.ThicknessCm.HasValue)
            {
                return options.OutputFolder;
            }

            var thicknessFolder = ThicknessNormalizer.ToFolderName(document.ThicknessCm.Value);
            return Path.Combine(options.OutputFolder, thicknessFolder);
        }

        private static IReadOnlyDictionary<string, string?> BuildTokens(ICadDocument document)
        {
            var thicknessLabel = document.ThicknessCm.HasValue
                ? ThicknessNormalizer.ToFolderName(document.ThicknessCm.Value)
                : "bilinmiyor";

            return new Dictionary<string, string?>
            {
                ["PartNumber"] = string.IsNullOrWhiteSpace(document.PartNumber) ? document.DisplayName : document.PartNumber,
                ["Description"] = document.Description,
                ["Revision"] = document.Revision,
                ["Thickness"] = thicknessLabel,
            };
        }
    }
}
