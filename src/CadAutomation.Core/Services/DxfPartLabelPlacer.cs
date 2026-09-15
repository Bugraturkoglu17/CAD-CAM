using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CadAutomation.Core.Services
{
    /// <summary>
    /// Parça etiketinin (MTEXT) DXF içindeki konumunu, kesim/büküm geometrisiyle KESİNLİKLE
    /// çakışmayacak şekilde yeniden hesaplar.
    ///
    /// Neden gerekli: Inventor'ın metni eklediği ilk konum (en büyük düz yüzeyin "PointOnFace"
    /// noktası) her zaman güvenli çıkmıyor - aynı yüzeyin üzerinden geçen bir büküm çizgisine ya da
    /// komşu bir kesim konturuna denk gelebiliyor (canlı testte gerçek bir çakışma tespit edildi:
    /// kullanıcının verdiği örnek parçada metin hem BEND_DOWN çizgisiyle hem bir CUT segmentiyle
    /// kesişiyordu). Bu yüzden export sonrası, parçanın CUT/BEND geometrisinden güvenli mesafede
    /// en büyük boş dikdörtgen alanı ızgara tabanlı "largest rectangle in histogram" algoritmasıyla
    /// bulup metni oraya taşıyoruz - hem konum hem boyut, dikdörtgene sığacak şekilde yeniden hesaplanır.
    ///
    /// Izgara tabanlı yaklaşım kasıtlı olarak MUHAFAZAKAR: bir segmentin bounding box'ı (güvenlik
    /// payıyla genişletilmiş) o aralıktaki TÜM hücreleri "dolu" sayar - çapraz çizgilerde gereğinden
    /// fazla alanı dolu işaretleyebilir ama bu YANLIŞ POZİTİF riski taşımaz (asla "güvenli" diye
    /// yanlış bir alan seçmez), sadece potansiyel olarak biraz daha küçük bir alan bulabilir. Kullanıcı
    /// önceliği "kesinlikle çakışma olmasın" olduğu için bu doğru taraflı bir hata.
    /// </summary>
    public static class DxfPartLabelPlacer
    {
        private const int GridResolution = 60;

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
        /// Inventor'a hiç bırakmadan DOĞRUDAN DXF dosyasına (aynı bu sınıfın kullandığı güvenli-alan
        /// hesaplamasıyla) yazıyoruz - RepositionLabel ile aynı güvenli konum mantığı, ama var olan
        /// bir MTEXT'i taşımak yerine sıfırdan oluşturuyor.
        /// </summary>
        /// <returns>Etiket başarıyla eklendiyse true; geometri bulunamadıysa (güvenli yer hesaplanamadı) false.</returns>
        public static bool InsertLabel(string dxfFilePath, string labelText, double safetyMarginMm = 2.0, IReadOnlyCollection<string>? avoidLayers = null, string layerName = "0")
        {
            var lines = File.ReadAllLines(dxfFilePath).ToList();

            var rect = ComputeSafeRect(lines, safetyMarginMm, avoidLayers);
            if (rect == null) return false;

            var (x, y, height) = ComputePlacement(rect.Value, labelText);

            int insertAt = FindEntitiesEndSecIndex(lines);
            if (insertAt < 0) return false;

            var entity = new[]
            {
                "0", "MTEXT",
                "8", layerName,
                "10", x.ToString(CultureInfo.InvariantCulture),
                "20", y.ToString(CultureInfo.InvariantCulture),
                "30", "0.0",
                "40", height.ToString(CultureInfo.InvariantCulture),
                "1", labelText,
            };

            lines.InsertRange(insertAt, entity);
            File.WriteAllLines(dxfFilePath, lines);
            return true;
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

        private static (double X, double Y, double Height) ComputePlacement((int Row0, int Col0, int Row1, int Col1, double MinX, double MinY, double CellW, double CellH) rect, string content)
        {
            double rectX0 = rect.MinX + rect.Col0 * rect.CellW;
            double rectY0 = rect.MinY + rect.Row0 * rect.CellH;
            double rectW = (rect.Col1 - rect.Col0 + 1) * rect.CellW;
            double rectH = (rect.Row1 - rect.Row0 + 1) * rect.CellH;

            int charCount = Math.Max(content.Length, 1);
            double heightFromH = rectH * 0.7;
            double heightFromW = rectW / (charCount * 0.6);
            double newHeight = Math.Min(heightFromH, heightFromW);
            if (newHeight < 0.5) newHeight = 0.5;

            double textWidth = newHeight * 0.6 * charCount;
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
            if (segments.Count == 0) return null;

            double minX = segments.Min(s => Math.Min(s.X1, s.X2));
            double maxX = segments.Max(s => Math.Max(s.X1, s.X2));
            double minY = segments.Min(s => Math.Min(s.Y1, s.Y2));
            double maxY = segments.Max(s => Math.Max(s.Y1, s.Y2));

            double width = maxX - minX;
            double height = maxY - minY;
            if (width <= 0 || height <= 0) return null;

            double cellW = width / GridResolution;
            double cellH = height / GridResolution;
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

        private static double ParseDouble(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    }
}
