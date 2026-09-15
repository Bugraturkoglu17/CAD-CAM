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
    public sealed class InventorFlatPatternDxfExporter : IFlatPatternDxfExporter
    {
        private readonly NamingTemplateEngine _namingEngine = new NamingTemplateEngine();

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

            if (options.ShowBendLines)
            {
                sb.Append("&BendUpLayer=").Append(options.LayerMapping.BendUpLayer);
                sb.Append("&BendDownLayer=").Append(options.LayerMapping.BendDownLayer);
            }

            // Kullanıcı isteği (2026-09-15): büküm çizgisinin sağında/solunda görünen, kesime
            // karışabilecek tanjant/geçiş referans çizgileri (EK2'deki "oval/radius" görünümlü
            // uçlar) hiç export'a dahil edilmemeli. Önceki yaklaşım bunları DXF export SONRASI
            // metin post-process ile (DxfLayerRemover) siliyordu - ama bu sadece DXF için işliyordu,
            // DWG binary olduğu için hiç temizlenmiyordu (kullanıcı DWG ile test edince fark etti).
            // Çevirmenin kendi "ShowTangentLines=False" seçeneğiyle bunları KAYNAKTA, her iki
            // formatta da devre dışı bırakmak hem DWG'yi de düzeltiyor hem de artık post-process'e
            // gerek bırakmıyor (translator hiç üretmiyor, silinecek bir şey kalmıyor).
            sb.Append("&ShowTangentLines=False");

            if (options.CreateMarking)
            {
                sb.Append("&EngraveLayer=").Append(options.LayerMapping.MarkingLayer);
            }

            return sb.ToString();
        }

        /// <summary>
        /// DXF export'undan SONRA post-process ile uygulanacak layer->renk (ACI) eşlemesi.
        /// WriteDataToFile'ın options string'i renk ayarlamaya izin vermiyor, bu yüzden ayrı adım.
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
