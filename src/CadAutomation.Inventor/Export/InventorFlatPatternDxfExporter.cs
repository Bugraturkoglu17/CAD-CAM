using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
                return new PartProcessResult(identifier, PartProcessStatus.Success, "Flat Pattern hazır (DWG/DXF export kapalı).");
            }

            try
            {
                var targetFolder = ResolveTargetFolder(options, part.Document);
                Directory.CreateDirectory(targetFolder);

                var tokens = BuildTokens(part.Document);
                var extension = options.Format == ExportFormat.Dwg ? "dwg" : "dxf";
                var fileName = _namingEngine.BuildFileName(options.NamingTemplate, tokens, extension);
                var fullPath = Path.Combine(targetFolder, fileName);
                var labelText = tokens["PartNumber"] ?? identifier;
                var annotations = BuildTextAnnotations(flatPattern, labelText, options);

                var translatorOptions = BuildTranslatorOptions(options);
                if (options.Format == ExportFormat.Dwg && annotations.Count > 0)
                {
                    WriteDwgDirectlyFromInventor(flatPattern, annotations, translatorOptions, fullPath);
                }
                else
                {
                    flatPattern.DataIO.WriteDataToFile(translatorOptions, fullPath);
                }

                if (options.Format == ExportFormat.Dwg && options.ShowBendLines && options.LayerMapping.BendLineTypeScale > 0)
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

                    if (annotations.Count > 0)
                    {
                        DxfPartLabelPlacer.InsertAnnotations(fullPath, annotations);
                    }
                }

                return new PartProcessResult(identifier, PartProcessStatus.Success, fullPath);
            }
            catch (Exception ex)
            {
                return new PartProcessResult(identifier, PartProcessStatus.Failed, "DWG/DXF dışa aktarımı başarısız: " + ex.Message);
            }
        }

        public PartProcessResult? CompleteBatch(BatchExportOptions options)
        {
            if (_pendingDwgScaleFiles.Count == 0) return null;
            try
            {
                DwgLineTypeScaleAdjuster.Apply(_pendingDwgScaleFiles, options.LayerMapping.BendLineTypeScale);
                return null;
            }
            catch (Exception ex)
            {
                return new PartProcessResult("DWG çizgi ölçeği", PartProcessStatus.Warning, "DWG dosyaları oluşturuldu ancak kesikli çizgi ölçeği uygulanamadı: " + ex.Message);
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

            // Inventor 2025+ düz açınım çevirmeni, görünür ve export işaretli geçici sketch
            // metinlerini doğrudan DWG/DXF metin nesnesi olarak aktarır. Böylece DWG'yi sonradan
            // AutoCAD ile açmaya veya script çalıştırmaya gerek kalmaz.
            sb.Append("&UnconsumedSketchesLayer=").Append(options.LayerMapping.MarkingLayer);
            AppendLayerColorOption(sb, "UnconsumedSketchesLayer", options.LayerMapping.MarkingColorAci);

            return sb.ToString();
        }

        private static void WriteDwgDirectlyFromInventor(
            Inv.FlatPattern flatPattern,
            IReadOnlyList<CadTextAnnotation> annotations,
            string translatorOptions,
            string fullPath)
        {
            var application = (Inv.Application)flatPattern.Application;
            var partDocument = (Inv.PartDocument)flatPattern.Document;
            var transaction = application.TransactionManager.StartTransaction(
                (Inv._Document)partDocument,
                "CAD Automation DWG markalama");
            var stage = "geçici sketch oluşturma";

            try
            {
                for (var index = 0; index < annotations.Count; index++)
                {
                    stage = "markalama metni " + (index + 1).ToString(CultureInfo.InvariantCulture);
                    var placement = ResolveAnnotationPlacement(application, flatPattern, annotations[index]);
                    var sketch = flatPattern.Sketches.Add(placement.Face, false);
                    sketch.Name = "__CAD_AUTOMATION_DWG_EXPORT_" + index.ToString(CultureInfo.InvariantCulture) + "__";
                    sketch.Visible = true;
                    sketch.Exported = true;
                    AddAnnotationToInventorSketch(
                        application,
                        partDocument,
                        sketch,
                        placement.Face,
                        placement.Point,
                        annotations[index],
                        index);
                }

                stage = "Inventor DWG çevirisi";
                flatPattern.DataIO.WriteDataToFile(translatorOptions, fullPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(stage + " başarısız: " + ex.Message, ex);
            }
            finally
            {
                // Geçici sketch ve metin stilleri yalnızca export sırasında yaşar. Abort ile
                // kaynak IPT'ye hiçbir markalama nesnesi kaydedilmez.
                transaction.Abort();
            }
        }

        private static void AddAnnotationToInventorSketch(
            Inv.Application application,
            Inv.PartDocument partDocument,
            Inv.PlanarSketch sketch,
            Inv.Face placementFace,
            Inv.Point modelPoint,
            CadTextAnnotation annotation,
            int index)
        {
            const double millimetersToCentimeters = 0.1;
            var detail = "koordinat dönüşümü";
            try
            {
                var geometry = application.TransientGeometry;
                var sketchPoint = sketch.ModelToSketchSpace(modelPoint);

                // Sketch'in Y yönü, seçilen düz açınım yüzüne göre ters dönebilir. Metnin DWG'deki
                // açısını korumak için yön vektörünü de model uzayından sketch uzayına dönüştürüyoruz.
                var directionPoint = geometry.CreatePoint(
                    modelPoint.X + Math.Cos(annotation.RotationRadians),
                    modelPoint.Y + Math.Sin(annotation.RotationRadians),
                    placementFace.PointOnFace.Z);
                var sketchDirectionPoint = sketch.ModelToSketchSpace(directionPoint);
                var sketchRotation = Math.Atan2(
                    sketchDirectionPoint.Y - sketchPoint.Y,
                    sketchDirectionPoint.X - sketchPoint.X);
                if (sketchRotation < 0) sketchRotation += Math.PI * 2.0;
                // Inventor sketch TextBox rotasyonu yalnızca 90° adımları kabul eder. Eğik
                // bükümlerde okunabilir en yakın yatay/dikey yönü kullan.
                var quarterTurn = Math.PI / 2.0;
                sketchRotation = Math.Round(sketchRotation / quarterTurn) * quarterTurn;
                if (sketchRotation >= Math.PI * 2.0) sketchRotation = 0;

                detail = "metin stili";
                var baseStyle = partDocument.TextStyles[1];
                var styleName = "__CAD_AUTOMATION_TEXT_" + index.ToString(CultureInfo.InvariantCulture);
                var textStyle = (Inv.TextStyle)baseStyle.Copy(styleName);
                textStyle.FontSize = annotation.HeightMillimeters * millimetersToCentimeters;
                textStyle.HorizontalJustification = Inv.HorizontalTextAlignmentEnum.kAlignTextCenter;
                textStyle.VerticalJustification = Inv.VerticalTextAlignmentEnum.kAlignTextMiddle;

                detail = "metin oluşturma";
                var safeSketchPoint = sketch.ModelToSketchSpace(placementFace.PointOnFace);
                var textBox = sketch.TextBoxes.AddFitted(
                    safeSketchPoint,
                    EscapeInventorText(annotation.Text),
                    textStyle);
                textBox.HorizontalJustification = Inv.HorizontalTextAlignmentEnum.kAlignTextCenter;
                textBox.VerticalJustification = Inv.VerticalTextAlignmentEnum.kAlignTextMiddle;
                detail = "metin konumu";
                SetTextOriginInsideFace(textBox, sketchPoint, safeSketchPoint, geometry);
                detail = "metin rotasyonu";
                textBox.Rotation = sketchRotation;

                detail = "metin rengi";
                var rgb = ConvertAciToRgb(annotation.LayerColorAci);
                textBox.SetColor((byte)rgb.R, (byte)rgb.G, (byte)rgb.B);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    detail + " (" + annotation.Text + ", X=" +
                    annotation.XMillimeters.ToString("0.###", CultureInfo.InvariantCulture) + ", Y=" +
                    annotation.YMillimeters.ToString("0.###", CultureInfo.InvariantCulture) + ", açı=" +
                    annotation.RotationRadians.ToString("0.###", CultureInfo.InvariantCulture) + ", h=" +
                    annotation.HeightMillimeters.ToString("0.###", CultureInfo.InvariantCulture) + "): " + ex.Message,
                    ex);
            }
        }

        private static void SetTextOriginInsideFace(
            Inv.TextBox textBox,
            Inv.Point2d requestedPoint,
            Inv.Point2d knownInteriorPoint,
            Inv.TransientGeometry geometry)
        {
            try
            {
                textBox.Origin = requestedPoint;
                return;
            }
            catch
            {
                // Metin kutusunun bir bölümü yüzey dışına taşıyorsa Inventor E_INVALIDARG döndürür.
                // Etiketi 2,5 mm'lik adımlarla yüzün bilinen iç noktasına yaklaştırıp ilk geçerli
                // konumu kullan. Bu yalnızca dar/üçgen flanşların kenarındaki radius yazılarında olur.
            }

            const double stepCm = 0.25;
            var deltaX = knownInteriorPoint.X - requestedPoint.X;
            var deltaY = knownInteriorPoint.Y - requestedPoint.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var stepCount = Math.Max(1, (int)Math.Ceiling(distance / stepCm));

            for (var step = 1; step <= stepCount; step++)
            {
                var fraction = (double)step / stepCount;
                var candidate = geometry.CreatePoint2d(
                    requestedPoint.X + deltaX * fraction,
                    requestedPoint.Y + deltaY * fraction);
                try
                {
                    textBox.Origin = candidate;
                    return;
                }
                catch when (step < stepCount)
                {
                    // Bir sonraki, biraz daha içerideki konumu dene.
                }
            }
        }

        private static AnnotationPlacement ResolveAnnotationPlacement(
            Inv.Application application,
            Inv.FlatPattern flatPattern,
            CadTextAnnotation annotation)
        {
            const double millimetersToCentimeters = 0.1;
            const double boundaryNudgeCm = 0.01;
            var geometry = application.TransientGeometry;
            Inv.Face? bestFace = null;
            Inv.Point? bestPoint = null;
            var bestDistance = double.MaxValue;

            for (var index = 1; index <= flatPattern.TopFaces.Count; index++)
            {
                var face = flatPattern.TopFaces[index];
                var facePoint = face.PointOnFace;
                var requestedPoint = geometry.CreatePoint(
                    annotation.XMillimeters * millimetersToCentimeters,
                    annotation.YMillimeters * millimetersToCentimeters,
                    facePoint.Z);
                var closestPoint = face.GetClosestPointTo(requestedPoint);
                var deltaX = closestPoint.X - requestedPoint.X;
                var deltaY = closestPoint.Y - requestedPoint.Y;
                var deltaZ = closestPoint.Z - requestedPoint.Z;
                var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestFace = face;
                bestPoint = distance <= 0.000001 ? requestedPoint : closestPoint;
            }

            if (bestFace == null || bestPoint == null)
            {
                throw new InvalidOperationException("Düz açınım üst yüzeyi bulunamadı.");
            }

            // İstenen nokta çok az da olsa yüzey sınırının dışındaysa, en yakın sınır noktasını
            // yüzün içine 0,1 mm taşı. Inventor sınırın tam üzerindeki TextBox origin'ini reddedebilir.
            if (bestDistance > 0.000001)
            {
                var interior = bestFace.PointOnFace;
                var deltaX = interior.X - bestPoint.X;
                var deltaY = interior.Y - bestPoint.Y;
                var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (length > 0.000001)
                {
                    bestPoint = geometry.CreatePoint(
                        bestPoint.X + deltaX / length * boundaryNudgeCm,
                        bestPoint.Y + deltaY / length * boundaryNudgeCm,
                        bestPoint.Z);
                }
            }

            return new AnnotationPlacement(bestFace, bestPoint);
        }

        private sealed class AnnotationPlacement
        {
            public AnnotationPlacement(Inv.Face face, Inv.Point point)
            {
                Face = face;
                Point = point;
            }

            public Inv.Face Face { get; }
            public Inv.Point Point { get; }
        }

        private static string EscapeInventorText(string value) =>
            value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

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

        private static IReadOnlyList<CadTextAnnotation> BuildTextAnnotations(
            Inv.FlatPattern flatPattern,
            string partLabel,
            BatchExportOptions options)
        {
            const double centimetersToMillimeters = 10.0;
            var annotations = new List<CadTextAnnotation>();
            var rangeBox = flatPattern.RangeBox;
            var partWidthMm = (rangeBox.MaxPoint.X - rangeBox.MinPoint.X) * centimetersToMillimeters;
            var partHeightMm = (rangeBox.MaxPoint.Y - rangeBox.MinPoint.Y) * centimetersToMillimeters;
            var smallerSideMm = Math.Min(partWidthMm, partHeightMm);
            var massCenter = flatPattern.MassProperties.CenterOfMass;
            var centerXmm = massCenter.X * centimetersToMillimeters;
            var centerYmm = massCenter.Y * centimetersToMillimeters;

            if (options.AddPartLabel && !string.IsNullOrWhiteSpace(partLabel))
            {
                annotations.Add(new CadTextAnnotation(
                    partLabel,
                    centerXmm,
                    centerYmm,
                    Clamp(smallerSideMm * 0.06, 3.0, 30.0),
                    0,
                    options.LayerMapping.MarkingLayer,
                    options.LayerMapping.MarkingColorAci));
            }

            if (!options.ShowBendLines) return annotations;

            foreach (var bend in CollectUniqueBends(flatPattern))
            {
                var radiusMm = bend.InnerRadiusCm * centimetersToMillimeters;
                var directionLabel = bend.IsDirectionUp ? "UP" : "DOWN";
                var angleDegrees = bend.AngleRadians * 180.0 / Math.PI;
                // Kullanıcı isteği (2026-09-22): "UP, DOWN yazılmalı ve kaç derece büküldüğü de
                // yazılmalıdır" - radius metnine büküm yönü ve açısı da ekleniyor. Inventor'ın
                // çevirmeni bunu native desteklemiyor (sadece radius otomatik ekleniyordu); FlatPattern
                // zaten Angle/IsDirectionUp veriyor, aynı (zaten doğrulanmış) yerleşim/rotasyon
                // mantığını kullanan TEK metne eklemek, ayrı çakışabilecek bir ikinci etiket
                // oluşturmaktan daha güvenli.
                var radiusText = "R" + radiusMm.ToString("0.###", CultureInfo.InvariantCulture) +
                    " " + directionLabel + " " + angleDegrees.ToString("0", CultureInfo.InvariantCulture) + "°";
                var lineLengthMm = bend.LengthCm * centimetersToMillimeters;
                var desiredHeight = Clamp(smallerSideMm * 0.06, 3.0, 30.0) * 0.5; // parça adı yüksekliğinin yarısı
                var maximumFittingHeight = lineLengthMm / (Math.Max(radiusText.Length, 1) * 0.6 * 2.0);
                var textHeight = desiredHeight;

                // Kullanıcı isteği (2026-09-22): metin kırmızı büküm çizgisinin TAM ORTASINDA
                // olmalı (çizginin başına yakın değil) ve HER ZAMAN yatay/okunur olmalı - dikey
                // bir büküm çizgisinde metni çizgiyle birlikte döndürmek (önceki davranış) okumayı
                // zorlaştırıyordu. Parçanın ağırlık merkezine bakan normal yönüne küçük bir payla
                // kaydırıyoruz ki kesikli çizgi harflerin tam içinden geçmesin.
                const double alongFraction = 0.5;
                var xCm = bend.StartXcm + (bend.StopXcm - bend.StartXcm) * alongFraction;
                var yCm = bend.StartYcm + (bend.StopYcm - bend.StartYcm) * alongFraction;
                var middleXcm = (bend.StartXcm + bend.StopXcm) / 2.0;
                var middleYcm = (bend.StartYcm + bend.StopYcm) / 2.0;
                var normalX = -bend.DirectionY;
                var normalY = bend.DirectionX;
                var towardCenter = normalX * (massCenter.X - middleXcm) + normalY * (massCenter.Y - middleYcm);
                var normalSign = towardCenter >= 0 ? 1.0 : -1.0;
                var offsetMm = textHeight * 1.5; // çizgiyle kesişmesin

                annotations.Add(new CadTextAnnotation(
                    radiusText,
                    xCm * centimetersToMillimeters + normalX * normalSign * offsetMm,
                    yCm * centimetersToMillimeters + normalY * normalSign * offsetMm,
                    textHeight,
                    Math.Atan2(bend.DirectionY, bend.DirectionX), // Daima büküm çizgisine paralel.
                    bend.IsDirectionUp ? options.LayerMapping.BendUpLayer : options.LayerMapping.BendDownLayer,
                    options.LayerMapping.BendLineColorAci ?? 1));
            }

            return annotations;
        }

        private static IReadOnlyList<BendCandidate> CollectUniqueBends(Inv.FlatPattern flatPattern)
        {
            var bends = new List<BendCandidate>();
            var results = flatPattern.FlatBendResults;

            for (var index = 1; index <= results.Count; index++)
            {
                var result = results[index];
                var edge = result.Edge;
                var startPoint = edge.StartVertex?.Point;
                var stopPoint = edge.StopVertex?.Point;
                if (startPoint == null || stopPoint == null) continue;

                var candidate = BendCandidate.TryCreate(
                    startPoint.X,
                    startPoint.Y,
                    stopPoint.X,
                    stopPoint.Y,
                    result.InnerRadius,
                    result.IsDirectionUp,
                    result.IsOnBottomFace,
                    result.Angle);
                if (candidate == null) continue;

                var duplicateIndex = bends.FindIndex(existing => existing.IsSameProjectedLine(candidate));
                if (duplicateIndex < 0)
                {
                    bends.Add(candidate);
                }
                else if (bends[duplicateIndex].IsOnBottomFace && !candidate.IsOnBottomFace)
                {
                    // DataIO'nun export ettiği merkez çizgisi üst yüzey sonucunun XY geometrisiyle
                    // birebir eşleşir; aynı fiziksel bükümün alt yüzey kopyasını bununla değiştir.
                    bends[duplicateIndex] = candidate;
                }
            }

            return bends;
        }

        private static double Clamp(double value, double minimum, double maximum) =>
            Math.Max(minimum, Math.Min(maximum, value));

        private sealed class BendCandidate
        {
            private BendCandidate(
                double startXcm,
                double startYcm,
                double stopXcm,
                double stopYcm,
                double innerRadiusCm,
                bool isDirectionUp,
                bool isOnBottomFace,
                double angleRadians,
                double directionX,
                double directionY,
                double lineOffset,
                double minimumProjection,
                double maximumProjection,
                double lengthCm)
            {
                StartXcm = startXcm;
                StartYcm = startYcm;
                StopXcm = stopXcm;
                StopYcm = stopYcm;
                InnerRadiusCm = innerRadiusCm;
                IsDirectionUp = isDirectionUp;
                IsOnBottomFace = isOnBottomFace;
                AngleRadians = angleRadians;
                DirectionX = directionX;
                DirectionY = directionY;
                LineOffset = lineOffset;
                MinimumProjection = minimumProjection;
                MaximumProjection = maximumProjection;
                LengthCm = lengthCm;
            }

            public double StartXcm { get; }
            public double StartYcm { get; }
            public double StopXcm { get; }
            public double StopYcm { get; }
            public double InnerRadiusCm { get; }
            public bool IsDirectionUp { get; }
            public bool IsOnBottomFace { get; }
            public double AngleRadians { get; }
            public double DirectionX { get; }
            public double DirectionY { get; }
            public double LineOffset { get; }
            public double MinimumProjection { get; }
            public double MaximumProjection { get; }
            public double LengthCm { get; }

            public static BendCandidate? TryCreate(
                double startXcm,
                double startYcm,
                double stopXcm,
                double stopYcm,
                double innerRadiusCm,
                bool isDirectionUp,
                bool isOnBottomFace,
                double angleRadians)
            {
                var deltaX = stopXcm - startXcm;
                var deltaY = stopYcm - startYcm;
                var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (length <= 0.000001) return null;

                var directionX = deltaX / length;
                var directionY = deltaY / length;
                if (directionX < -0.000001 || (Math.Abs(directionX) <= 0.000001 && directionY < 0))
                {
                    Swap(ref startXcm, ref stopXcm);
                    Swap(ref startYcm, ref stopYcm);
                    directionX = -directionX;
                    directionY = -directionY;
                }

                var normalX = -directionY;
                var normalY = directionX;
                var lineOffset = normalX * startXcm + normalY * startYcm;
                var startProjection = directionX * startXcm + directionY * startYcm;
                var stopProjection = directionX * stopXcm + directionY * stopYcm;

                return new BendCandidate(
                    startXcm,
                    startYcm,
                    stopXcm,
                    stopYcm,
                    innerRadiusCm,
                    isDirectionUp,
                    isOnBottomFace,
                    angleRadians,
                    directionX,
                    directionY,
                    lineOffset,
                    Math.Min(startProjection, stopProjection),
                    Math.Max(startProjection, stopProjection),
                    length);
            }

            public bool IsSameProjectedLine(BendCandidate other)
            {
                const double directionTolerance = 0.0001;
                const double distanceToleranceCm = 0.02;
                const double intervalToleranceCm = 0.05;
                var directionDot = DirectionX * other.DirectionX + DirectionY * other.DirectionY;
                if (directionDot < 1.0 - directionTolerance) return false;
                if (Math.Abs(LineOffset - other.LineOffset) > distanceToleranceCm) return false;

                return Math.Min(MaximumProjection, other.MaximumProjection) >=
                       Math.Max(MinimumProjection, other.MinimumProjection) - intervalToleranceCm;
            }

            private static void Swap(ref double first, ref double second)
            {
                var temporary = first;
                first = second;
                second = temporary;
            }
        }

        private static string ResolveTargetFolder(BatchExportOptions options, ICadDocument document)
        {
            if (document is InventorCadDocument suspect && suspect.IsFirstFeatureExtrude)
            {
                return Path.Combine(options.OutputFolder, "HATALI");
            }

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
