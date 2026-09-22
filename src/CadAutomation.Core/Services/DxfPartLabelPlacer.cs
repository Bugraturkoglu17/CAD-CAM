using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CadAutomation.Core.Services
{
    /// <summary>
    /// Parça etiketinin (MTEXT) DXF içindeki konumunu, kesim/büküm geometrisiyle KESİNLİKLE
    /// çakışmayacak şekilde hesaplar.
    ///
    /// Yerleşim önceliği (2026-09-16 kullanıcı isteği): önce parçanın TAM MERKEZİNE (ağırlık
    /// merkezi/bounding-box merkezi) okunur bir boyutla yerleştirmeyi dener - orası genelde en
    /// doğal/beklenen konum. Sadece o bölge kesim/büküm geometrisiyle çakışıyorsa, ızgara tabanlı
    /// "largest rectangle in histogram" algoritmasıyla başka güvenli bir alana taşır.
    ///
    /// Boyut her zaman parçanın kendi büyüklüğüne ORANTILI hesaplanır (küçük parçada küçük, büyük
    /// parçada büyük ama okunur) - hangi boş alana yerleştiği bu oranı büyütmez, sadece küçültebilir
    /// (metin o alana sığmıyorsa güvenlik için küçültülür, asla büyütülmez).
    ///
    /// Neden gerekli: Inventor'ın metni eklediği ilk konum (en büyük düz yüzeyin "PointOnFace"
    /// noktası) her zaman güvenli çıkmıyor - aynı yüzeyin üzerinden geçen bir büküm çizgisine ya da
    /// komşu bir kesim konturuna denk gelebiliyor (canlı testte gerçek bir çakışma tespit edildi:
    /// kullanıcının verdiği örnek parçada metin hem BEND_DOWN çizgisiyle hem bir CUT segmentiyle
    /// kesişiyordu).
    ///
    /// Izgara tabanlı fallback kasıtlı olarak MUHAFAZAKAR: bir segmentin bounding box'ı (güvenlik
    /// payıyla genişletilmiş) o aralıktaki TÜM hücreleri "dolu" sayar - çapraz çizgilerde gereğinden
    /// fazla alanı dolu işaretleyebilir ama bu YANLIŞ POZİTİF riski taşımaz (asla "güvenli" diye
    /// yanlış bir alan seçmez), sadece potansiyel olarak biraz daha küçük bir alan bulabilir. Merkez
    /// denemesi ise TAM (ızgara kaynaklı yaklaşıksız) segment-dikdörtgen kesişim testi kullanıyor.
    /// </summary>
    public static class DxfPartLabelPlacer
    {
        private const int GridResolution = 60;

        /// <summary>Metin yüksekliğinin parçanın küçük kenarına oranı - "okunur" hedefi.</summary>
        private const double HeightRatio = 0.06;
        private const double MinHeightMm = 3.0;
        private const double MaxHeightMm = 30.0;

        /// <summary>Tahoma benzeri fontlarda karakter genişliği ~ yükseklik * bu katsayı (kaba yaklaşım).</summary>
        private const double CharWidthFactor = 0.6;

        private static readonly HashSet<string> DefaultAvoidLayers = new HashSet<string> { "CUT", "BEND_UP", "BEND_DOWN" };

        /// <returns>Etiket başarıyla yeniden konumlandırıldıysa true; MTEXT veya geometri bulunamadıysa false.</returns>
        public static bool RepositionLabel(string dxfFilePath, double safetyMarginMm = 2.0, IReadOnlyCollection<string>? avoidLayers = null)
        {
            var lines = File.ReadAllLines(dxfFilePath);

            var mtext = FindMText(lines);
            if (mtext == null) return false;

            var rect = ComputeSafeRect(lines, safetyMarginMm, avoidLayers);
            if (rect == null) return false;

            var (newX, newY, newHeight) = ComputePlacement(rect.Value, mtext.Content);

            if (mtext.XValueLineIndex >= 0) lines[mtext.XValueLineIndex] = newX.ToString(CultureInfo.InvariantCulture);
            if (mtext.YValueLineIndex >= 0) lines[mtext.YValueLineIndex] = newY.ToString(CultureInfo.InvariantCulture);
            if (mtext.HeightValueLineIndex >= 0) lines[mtext.HeightValueLineIndex] = newHeight.ToString(CultureInfo.InvariantCulture);

            File.WriteAllLines(dxfFilePath, lines);
            return true;
        }

        /// <summary>
        /// Inventor'ın FlatPattern çevirmeni, sketch'e eklenen metni bazı parçalarda export'a hiç
        /// dahil etmiyor (canlı testte tespit edildi: aynı Inventor API çağrıları, aynı belge - ama
        /// oturumdan oturuma farklı parçalarda MTEXT export çıktısında hiç yok; Document.Update() ve
        /// Unfold() zorlaması bile değiştirmiyor - çevirmenin kendi iç durumuna/cache'ine bağlı,
        /// tarafımızdan kontrol edilemeyen bir davranış). Bu yüzden güvenilirlik için etiketi
        /// Inventor'a hiç bırakmadan DOĞRUDAN DXF dosyasına yazıyoruz.
        ///
        /// Yerleşim: önce parçanın merkezinde, boyu parçanın kendi büyüklüğüne orantılı okunur bir
        /// yükseklikle dener; orası kesim/büküm geometrisiyle çakışıyorsa en büyük boş dikdörtgene
        /// (ızgara arama) taşır - oradaki boyut da aynı okunur hedefi geçmeyecek şekilde sınırlanır.
        /// </summary>
        /// <returns>Etiket başarıyla eklendiyse true; geometri bulunamadıysa (güvenli yer hesaplanamadı) false.</returns>
        public static bool InsertLabel(string dxfFilePath, string labelText, double safetyMarginMm = 2.0, IReadOnlyCollection<string>? avoidLayers = null, string layerName = "0")
        {
            var lines = File.ReadAllLines(dxfFilePath).ToList();

            var targetLayers = avoidLayers != null ? new HashSet<string>(avoidLayers) : DefaultAvoidLayers;
            var segments = CollectSegments(lines, targetLayers);
            var bounds = ComputeBounds(segments);
            if (bounds == null) return false;

            var (minX, maxX, minY, maxY) = bounds.Value;
            double desiredHeight = ComputeReadableHeight(maxX - minX, maxY - minY);

            double x, y, height;
            var centerFit = TryFitAtCenter(minX, maxX, minY, maxY, desiredHeight, labelText, segments, safetyMarginMm);
            if (centerFit != null)
            {
                (x, y, height) = centerFit.Value;
            }
            else
            {
                var rect = ComputeSafeRect(lines, safetyMarginMm, avoidLayers);
                if (rect == null) return false;
                (x, y, height) = ComputePlacement(rect.Value, labelText, desiredHeight);
            }

            int insertAt = FindEntitiesEndSecIndex(lines);
            if (insertAt < 0) return false;

            // Kritik (2026-09-17 kullanıcı testinde ÜÇ adımda tespit edildi - AC1015+ DXF'te
            // handle ZORUNLU, atlanamaz):
            // 1) Inventor'ın kendi entity'lerinde subclass marker'ları (100 AcDbEntity/AcDbMText)
            //    var, elle eklediğimiz MTEXT'te yoktu - eklemek doğru ve zararsız.
            // 2) Handle (5) hiç yazmadan denedik ("AutoCAD entity'siz handle'ı tolere eder"
            //    varsayımıyla) - AutoCAD "Handle missing / Invalid or incomplete DXF input --
            //    drawing discarded" diyerek dosyayı TAMAMEN reddetti. AC1015+ formatında handle
            //    zorunlu, atlanamaz.
            // 3) Dosyadaki en büyük mevcut handle'ı bulup +1 vererek handle ekledik ama
            //    $HANDSEED'i GÜNCELLEMEDİK - AutoCAD "Bad handle X: already in use" hatası verdi.
            //    Kök neden: $HANDSEED, dosyadaki "bir sonraki boş handle"yi gösterir; biz max+1'i
            //    KULLANDIK ama seed hâlâ eski (daha düşük) değerdeydi, bu yüzden AutoCAD kendi
            //    audit/repair sürecinde (ör. eksik bir dictionary objesi için) seed'den başlayarak
            //    TAM OLARAK bizim kullandığımız değeri tekrar atadı - çakışma. Çözüm: handle'ı
            //    kullandıktan SONRA $HANDSEED'i bu handle'ın da üzerine çıkaracak şekilde
            //    güncellemek - artık hem AutoCAD'in kendi ataması hem bizimki çakışmıyor.
            var maxHandle = FindMaxHandle(lines);
            var newHandleValue = maxHandle + 1;
            var newHandle = newHandleValue.ToString("X", CultureInfo.InvariantCulture);
            var ownerHandle = FindOwnerHandle(lines);

            var entityLines = new List<string> { "0", "MTEXT", "5", newHandle };
            if (ownerHandle != null) entityLines.AddRange(new[] { "330", ownerHandle });
            entityLines.AddRange(new[]
            {
                "100", "AcDbEntity",
                "8", layerName,
                "100", "AcDbMText",
                "10", x.ToString(CultureInfo.InvariantCulture),
                "20", y.ToString(CultureInfo.InvariantCulture),
                "30", "0.0",
                "40", height.ToString(CultureInfo.InvariantCulture),
                "1", labelText,
            });

            lines.InsertRange(insertAt, entityLines);
            BumpHandSeed(lines, newHandleValue + 1);
            File.WriteAllLines(dxfFilePath, lines);
            return true;
        }

        /// <summary>Dosyadaki TÜM mevcut handle'ların (grup kodu 5, $HANDSEED dahil) en büyüğü.</summary>
        private static long FindMaxHandle(List<string> lines)
        {
            long maxHandle = 0;
            for (int i = 0; i + 1 < lines.Count; i += 2)
            {
                if (lines[i].Trim() != "5") continue;
                if (long.TryParse(lines[i + 1].Trim(), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var handle))
                {
                    if (handle > maxHandle) maxHandle = handle;
                }
            }
            return maxHandle;
        }

        /// <summary>
        /// ENTITIES bölümündeki ilk entity'nin owner pointer'ını (330) döndürür - yeni entity'yi
        /// aynı block record'a (model space) bağlamak için. Bulunamazsa null (330 hiç yazılmaz;
        /// bazı okuyucular bunu tolere ediyor ama ideal olan gerçek bir owner'la eşleşmesi).
        /// </summary>
        private static string? FindOwnerHandle(List<string> lines)
        {
            bool inEntities = false;
            for (int i = 0; i + 1 < lines.Count; i += 2)
            {
                var code = lines[i].Trim();
                var value = lines[i + 1].Trim();

                if (code == "2" && value == "ENTITIES") { inEntities = true; continue; }
                if (code == "0" && value == "ENDSEC") { if (inEntities) break; inEntities = false; }
                if (inEntities && code == "330") return value;
            }
            return null;
        }

        /// <summary>
        /// HEADER bölümündeki $HANDSEED'i (varsa) en az <paramref name="minimumValue"/> olacak
        /// şekilde günceller - yeni eklediğimiz handle'ı AutoCAD'in kendi otomatik atama
        /// mekanizmasının tekrar kullanmasını (çakışmasını) önlemek için. $HANDSEED bulunamazsa
        /// sessizce hiçbir şey yapmaz (Inventor'ın her sürümü bunu yazmayabilir).
        /// </summary>
        private static void BumpHandSeed(List<string> lines, long minimumValue)
        {
            for (int i = 0; i + 3 < lines.Count; i += 2)
            {
                if (lines[i].Trim() != "9" || lines[i + 1].Trim() != "$HANDSEED") continue;
                if (lines[i + 2].Trim() != "5") return;

                long.TryParse(lines[i + 3].Trim(), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var current);
                if (current < minimumValue)
                {
                    lines[i + 3] = minimumValue.ToString("X", CultureInfo.InvariantCulture);
                }
                return;
            }
        }

        /// <summary>Parçanın küçük kenarına orantılı, okunur bir metin yüksekliği (mm) - alt/üst sınırlarla.</summary>
        private static double ComputeReadableHeight(double partWidth, double partHeight)
        {
            double basis = Math.Min(partWidth, partHeight);
            double height = basis * HeightRatio;
            if (height < MinHeightMm) height = MinHeightMm;
            if (height > MaxHeightMm) height = MaxHeightMm;
            return height;
        }

        /// <summary>
        /// Parçanın tam merkezine, verilen yükseklikte (parça genişliğine sığmıyorsa küçültülerek)
        /// yerleştirmeyi dener. Kesim/büküm geometrisiyle (güvenlik payı dahil) çakışırsa veya okunur
        /// alt sınırın (MinHeightMm) altına küçülmesi gerekiyorsa null döner - çağıran taraf başka bir
        /// alana taşımalı.
        /// </summary>
        private static (double X, double Y, double Height)? TryFitAtCenter(
            double minX, double maxX, double minY, double maxY, double desiredHeight, string content,
            List<Segment> segments, double marginMm)
        {
            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;
            double partWidth = maxX - minX;

            int charCount = Math.Max(content.Length, 1);
            double height = desiredHeight;
            double width = height * CharWidthFactor * charCount;

            double maxWidth = partWidth * 0.9;
            if (width > maxWidth && maxWidth > 0)
            {
                height = maxWidth / (CharWidthFactor * charCount);
                width = maxWidth;
            }

            if (height < MinHeightMm) return null;

            double x = centerX - width / 2.0;
            double y = centerY - height / 2.0;

            if (RectCollides(x, y, x + width, y + height, segments, marginMm)) return null;

            return (x, y, height);
        }

        private sealed class Segment
        {
            public double X1, Y1, X2, Y2;
        }

        private sealed class MTextInfo
        {
            public int XValueLineIndex = -1;
            public int YValueLineIndex = -1;
            public int HeightValueLineIndex = -1;
            public string Content = string.Empty;
        }

        /// <summary>"2/ENTITIES"den sonraki ilk "0/ENDSEC" çiftinin "0" satırının index'i (yeni entity buraya eklenir).</summary>
        private static int FindEntitiesEndSecIndex(List<string> lines)
        {
            bool inEntities = false;
            for (int i = 0; i + 1 < lines.Count; i += 2)
            {
                var code = lines[i].Trim();
                var value = lines[i + 1].Trim();

                if (code == "2" && value == "ENTITIES") { inEntities = true; continue; }
                if (inEntities && code == "0" && value == "ENDSEC") return i;
            }
            return -1;
        }

        private static (double X, double Y, double Height) ComputePlacement(
            (int Row0, int Col0, int Row1, int Col1, double MinX, double MinY, double CellW, double CellH) rect,
            string content, double maxHeight = double.MaxValue)
        {
            double rectX0 = rect.MinX + rect.Col0 * rect.CellW;
            double rectY0 = rect.MinY + rect.Row0 * rect.CellH;
            double rectW = (rect.Col1 - rect.Col0 + 1) * rect.CellW;
            double rectH = (rect.Row1 - rect.Row0 + 1) * rect.CellH;

            int charCount = Math.Max(content.Length, 1);
            double heightFromH = rectH * 0.7;
            double heightFromW = rectW / (charCount * CharWidthFactor);
            double newHeight = Math.Min(Math.Min(heightFromH, heightFromW), maxHeight);
            if (newHeight < 0.5) newHeight = 0.5;

            double textWidth = newHeight * CharWidthFactor * charCount;
            double newX = rectX0 + Math.Max(0, (rectW - textWidth) / 2.0);
            double newY = rectY0 + (rectH - newHeight) / 2.0;

            return (newX, newY, newHeight);
        }

        /// <summary>CUT/BEND geometrisinden güvenli mesafede en büyük boş dikdörtgeni (ızgara koordinatlarında) hesaplar.</summary>
        private static (int Row0, int Col0, int Row1, int Col1, double MinX, double MinY, double CellW, double CellH)? ComputeSafeRect(
            IReadOnlyList<string> lines, double safetyMarginMm, IReadOnlyCollection<string>? avoidLayers)
        {
            var targetLayers = avoidLayers != null ? new HashSet<string>(avoidLayers) : DefaultAvoidLayers;
            var segments = CollectSegments(lines, targetLayers);
            var bounds = ComputeBounds(segments);
            if (bounds == null) return null;

            var (minX, maxX, minY, maxY) = bounds.Value;
            double cellW = (maxX - minX) / GridResolution;
            double cellH = (maxY - minY) / GridResolution;
            if (cellW <= 0 || cellH <= 0) return null;

            var blocked = new bool[GridResolution, GridResolution];
            foreach (var seg in segments)
            {
                MarkBlockedCells(blocked, seg, minX, minY, cellW, cellH, safetyMarginMm);
            }

            var rect = FindLargestEmptyRectangle(blocked);
            if (rect == null) return null;

            return (rect.Value.Row0, rect.Value.Col0, rect.Value.Row1, rect.Value.Col1, minX, minY, cellW, cellH);
        }

        private static (double MinX, double MaxX, double MinY, double MaxY)? ComputeBounds(List<Segment> segments)
        {
            if (segments.Count == 0) return null;

            double minX = segments.Min(s => Math.Min(s.X1, s.X2));
            double maxX = segments.Max(s => Math.Max(s.X1, s.X2));
            double minY = segments.Min(s => Math.Min(s.Y1, s.Y2));
            double maxY = segments.Max(s => Math.Max(s.Y1, s.Y2));

            if (maxX - minX <= 0 || maxY - minY <= 0) return null;
            return (minX, maxX, minY, maxY);
        }

        private static List<Segment> CollectSegments(IReadOnlyList<string> lines, HashSet<string> targetLayers)
        {
            var segments = new List<Segment>();
            bool inEntities = false;
            string? curType = null;
            string? curLayer = null;
            double? x1 = null, y1 = null, x2 = null, y2 = null;

            void FlushIfLine()
            {
                if (curType == "LINE" && curLayer != null && targetLayers.Contains(curLayer)
                    && x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
                {
                    segments.Add(new Segment { X1 = x1.Value, Y1 = y1.Value, X2 = x2.Value, Y2 = y2.Value });
                }
            }

            for (int i = 0; i + 1 < lines.Count; i += 2)
            {
                var code = lines[i].Trim();
                var value = lines[i + 1].Trim();

                if (code == "2" && value == "ENTITIES") { inEntities = true; continue; }
                if (code == "0" && value == "ENDSEC") { inEntities = false; }
                if (!inEntities) continue;

                if (code == "0")
                {
                    FlushIfLine();
                    curType = value;
                    curLayer = null;
                    x1 = y1 = x2 = y2 = null;
                    continue;
                }

                if (code == "8") curLayer = value;
                else if (code == "10") x1 = ParseDouble(value);
                else if (code == "20") y1 = ParseDouble(value);
                else if (code == "11") x2 = ParseDouble(value);
                else if (code == "21") y2 = ParseDouble(value);
            }
            FlushIfLine();

            return segments;
        }

        private static MTextInfo? FindMText(string[] lines)
        {
            bool inEntities = false;
            string? curType = null;
            MTextInfo? current = null;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                var code = lines[i].Trim();
                var value = lines[i + 1].Trim();

                if (code == "2" && value == "ENTITIES") { inEntities = true; continue; }
                if (code == "0" && value == "ENDSEC") { inEntities = false; }
                if (!inEntities) continue;

                if (code == "0")
                {
                    if (curType == "MTEXT" && current != null) return current;
                    curType = value;
                    current = value == "MTEXT" ? new MTextInfo() : null;
                    continue;
                }

                if (curType != "MTEXT" || current == null) continue;

                if (code == "10") current.XValueLineIndex = i + 1;
                else if (code == "20") current.YValueLineIndex = i + 1;
                else if (code == "40") current.HeightValueLineIndex = i + 1;
                else if (code == "1") current.Content = value;
            }

            return (curType == "MTEXT" && current != null) ? current : null;
        }

        private static void MarkBlockedCells(bool[,] blocked, Segment seg, double minX, double minY, double cellW, double cellH, double margin)
        {
            double sx0 = Math.Min(seg.X1, seg.X2) - margin;
            double sx1 = Math.Max(seg.X1, seg.X2) + margin;
            double sy0 = Math.Min(seg.Y1, seg.Y2) - margin;
            double sy1 = Math.Max(seg.Y1, seg.Y2) + margin;

            int col0 = Math.Max(0, (int)((sx0 - minX) / cellW));
            int col1 = Math.Min(GridResolution - 1, (int)((sx1 - minX) / cellW));
            int row0 = Math.Max(0, (int)((sy0 - minY) / cellH));
            int row1 = Math.Min(GridResolution - 1, (int)((sy1 - minY) / cellH));

            for (int r = row0; r <= row1; r++)
            {
                for (int c = col0; c <= col1; c++)
                {
                    blocked[r, c] = true;
                }
            }
        }

        /// <summary>Klasik "largest rectangle in histogram" algoritmasının 2D (tüm satırlarda tekrar) uygulaması.</summary>
        private static (int Row0, int Col0, int Row1, int Col1)? FindLargestEmptyRectangle(bool[,] blocked)
        {
            int rows = blocked.GetLength(0);
            int cols = blocked.GetLength(1);
            var heights = new int[cols];

            int bestArea = 0;
            (int, int, int, int)? best = null;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    heights[c] = blocked[r, c] ? 0 : heights[c] + 1;
                }

                var stack = new Stack<int>();
                for (int c = 0; c <= cols; c++)
                {
                    int h = c == cols ? 0 : heights[c];
                    while (stack.Count > 0 && heights[stack.Peek()] >= h)
                    {
                        int top = stack.Pop();
                        int rectHeight = heights[top];
                        int left = stack.Count > 0 ? stack.Peek() + 1 : 0;
                        int rectWidth = c - left;
                        int area = rectHeight * rectWidth;
                        if (area > bestArea)
                        {
                            bestArea = area;
                            best = (r - rectHeight + 1, left, r, c - 1);
                        }
                    }
                    stack.Push(c);
                }
            }

            return best;
        }

        /// <summary>Segment (güvenlik payıyla genişletilmiş) dikdörtgenle kesişiyor mu - tam geometrik test.</summary>
        private static bool RectCollides(double rx0, double ry0, double rx1, double ry1, List<Segment> segments, double marginMm)
        {
            rx0 -= marginMm; ry0 -= marginMm; rx1 += marginMm; ry1 += marginMm;
            foreach (var seg in segments)
            {
                if (SegmentIntersectsRect(seg.X1, seg.Y1, seg.X2, seg.Y2, rx0, ry0, rx1, ry1)) return true;
            }
            return false;
        }

        private static bool SegmentIntersectsRect(double sx1, double sy1, double sx2, double sy2, double rx0, double ry0, double rx1, double ry1)
        {
            if (Math.Max(sx1, sx2) < rx0 || Math.Min(sx1, sx2) > rx1) return false;
            if (Math.Max(sy1, sy2) < ry0 || Math.Min(sy1, sy2) > ry1) return false;

            if (IsInsideRect(sx1, sy1, rx0, ry0, rx1, ry1) || IsInsideRect(sx2, sy2, rx0, ry0, rx1, ry1)) return true;

            return SegSegIntersect(sx1, sy1, sx2, sy2, rx0, ry0, rx1, ry0)
                || SegSegIntersect(sx1, sy1, sx2, sy2, rx1, ry0, rx1, ry1)
                || SegSegIntersect(sx1, sy1, sx2, sy2, rx1, ry1, rx0, ry1)
                || SegSegIntersect(sx1, sy1, sx2, sy2, rx0, ry1, rx0, ry0);
        }

        private static bool IsInsideRect(double x, double y, double rx0, double ry0, double rx1, double ry1)
            => x >= rx0 && x <= rx1 && y >= ry0 && y <= ry1;

        private static bool SegSegIntersect(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
        {
            double d1 = (dx - cx) * (ay - cy) - (dy - cy) * (ax - cx);
            double d2 = (dx - cx) * (by - cy) - (dy - cy) * (bx - cx);
            double d3 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            double d4 = (bx - ax) * (dy - ay) - (by - ay) * (dx - ax);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double ParseDouble(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    }
}
