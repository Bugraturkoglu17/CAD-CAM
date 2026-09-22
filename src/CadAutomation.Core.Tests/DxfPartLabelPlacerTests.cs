using System;
using System.Globalization;
using System.IO;
using CadAutomation.Core.Models;
using CadAutomation.Core.Services;
using Xunit;

namespace CadAutomation.Core.Tests
{
    public class DxfPartLabelPlacerTests
    {
        // 100x100'lük kare bir parça (4 kenar CUT), ortada dikey bir BEND_DOWN çizgisi (x=50).
        // Metin kasıtlı olarak bu çizginin tam üzerine (x=45..57 civarı) yerleştirilmiş - gerçek
        // canlı testte tespit edilen çakışma senaryosunun basitleştirilmiş simülasyonu.
        private const string OverlappingDxf =
            "0\nSECTION\n2\nENTITIES\n" +
            "0\nLINE\n8\nCUT\n10\n0.0\n20\n0.0\n11\n100.0\n21\n0.0\n" +
            "0\nLINE\n8\nCUT\n10\n100.0\n20\n0.0\n11\n100.0\n21\n100.0\n" +
            "0\nLINE\n8\nCUT\n10\n100.0\n20\n100.0\n11\n0.0\n21\n100.0\n" +
            "0\nLINE\n8\nCUT\n10\n0.0\n20\n100.0\n11\n0.0\n21\n0.0\n" +
            "0\nLINE\n8\nBEND_DOWN\n10\n50.0\n20\n0.0\n11\n50.0\n21\n100.0\n" +
            "0\nMTEXT\n10\n45.0\n20\n45.0\n40\n5.0\n1\nTEST\n" +
            "0\nENDSEC\n0\nEOF\n";

        [Fact]
        public void Cakisan_etiket_bend_cizgisinden_uzaga_tasinir()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, OverlappingDxf);

                var moved = DxfPartLabelPlacer.RepositionLabel(path, safetyMarginMm: 2.0);

                Assert.True(moved);

                var (newX, newY, newHeight, content) = ReadMText(path);
                Assert.Equal("TEST", content);

                double charCount = content.Length;
                double textWidth = newHeight * 0.6 * charCount;
                double textLeft = newX;
                double textRight = newX + textWidth;

                // Metnin yeni konumu, bend çizgisinin (x=50) her iki tarafında da güvenlik payı
                // (2.0) kadar boşluk bırakmalı - yani ya tamamen solunda ya tamamen sağında olmalı.
                bool entirelyLeft = textRight <= 50.0 - 2.0 + 0.01;
                bool entirelyRight = textLeft >= 50.0 + 2.0 - 0.01;
                Assert.True(entirelyLeft || entirelyRight,
                    $"Metin hala bend çizgisiyle çakışıyor olabilir: X=[{textLeft},{textRight}]");
            }
            finally
            {
                File.Delete(path);
            }
        }

        // Aynı 100x100 kare + ortada dikey BEND_DOWN, ama hiç MTEXT yok - InsertLabel bunu
        // sıfırdan güvenli bir konuma eklemeli (Inventor çevirmeninin etiketi export'a dahil
        // etmediği canlı test senaryosunun simülasyonu).
        private const string NoLabelDxf =
            "0\nSECTION\n2\nENTITIES\n" +
            "0\nLINE\n8\nCUT\n10\n0.0\n20\n0.0\n11\n100.0\n21\n0.0\n" +
            "0\nLINE\n8\nCUT\n10\n100.0\n20\n0.0\n11\n100.0\n21\n100.0\n" +
            "0\nLINE\n8\nCUT\n10\n100.0\n20\n100.0\n11\n0.0\n21\n100.0\n" +
            "0\nLINE\n8\nCUT\n10\n0.0\n20\n100.0\n11\n0.0\n21\n0.0\n" +
            "0\nLINE\n8\nBEND_DOWN\n10\n50.0\n20\n0.0\n11\n50.0\n21\n100.0\n" +
            "0\nENDSEC\n0\nEOF\n";

        [Fact]
        public void InsertLabel_mtext_yoksa_guvenli_konuma_yeni_etiket_ekler()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, NoLabelDxf);

                var inserted = DxfPartLabelPlacer.InsertLabel(path, "TEST-PART", safetyMarginMm: 2.0);

                Assert.True(inserted);

                var (newX, newY, newHeight, content) = ReadMText(path);
                Assert.Equal("TEST-PART", content);

                double charCount = content.Length;
                double textWidth = newHeight * 0.6 * charCount;
                double textLeft = newX;
                double textRight = newX + textWidth;

                bool entirelyLeft = textRight <= 50.0 - 2.0 + 0.01;
                bool entirelyRight = textLeft >= 50.0 + 2.0 - 0.01;
                Assert.True(entirelyLeft || entirelyRight,
                    $"Eklenen etiket bend çizgisiyle çakışıyor olabilir: X=[{textLeft},{textRight}]");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void InsertAnnotations_agirlik_merkezi_ve_radius_yazilarini_katmanlariyla_ekler()
        {
            const string dxf =
                "0\nSECTION\n2\nHEADER\n9\n$HANDSEED\n5\n20\n0\nENDSEC\n" +
                "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n5\n2\n70\n2\n" +
                "0\nLAYER\n5\n10\n330\n2\n100\nAcDbSymbolTableRecord\n100\nAcDbLayerTableRecord\n2\n0\n70\n0\n62\n7\n6\nCONTINUOUS\n" +
                "0\nLAYER\n5\n11\n330\n2\n100\nAcDbSymbolTableRecord\n100\nAcDbLayerTableRecord\n2\nBEND_DOWN\n70\n0\n62\n1\n6\nDASHED\n" +
                "0\nENDTAB\n0\nENDSEC\n" +
                "0\nSECTION\n2\nENTITIES\n" +
                "0\nLINE\n5\n12\n330\n1F\n8\nBEND_DOWN\n10\n0\n20\n25\n11\n100\n21\n25\n" +
                "0\nENDSEC\n0\nEOF\n";
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, dxf);
                var annotations = new[]
                {
                    new CadTextAnnotation("PART-01", 50, 40, 6, 0, "MARKING", 2),
                    new CadTextAnnotation("R2", 15, 28, 3, 0, "BEND_DOWN", 1),
                };

                var inserted = DxfPartLabelPlacer.InsertAnnotations(path, annotations);
                var content = File.ReadAllText(path).Replace("\r\n", "\n");

                Assert.Equal(2, inserted);
                Assert.Contains("2\nMARKING\n70\n0\n62\n2", content);
                Assert.Equal(2, CountOccurrences(content, "0\nMTEXT\n"));
                Assert.Contains("8\nMARKING\n100\nAcDbMText\n10\n50\n20\n40", content);
                Assert.Contains("71\n5\n1\nPART-01", content);
                Assert.Contains("8\nBEND_DOWN\n100\nAcDbMText\n10\n15\n20\n28", content);
                Assert.Contains("71\n5\n1\nR2", content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void InsertLabel_gecerli_geometri_yoksa_false_doner_ve_dosyaya_dokunulmaz()
        {
            var path = Path.GetTempFileName();
            try
            {
                const string noGeometry = "0\nSECTION\n2\nENTITIES\n0\nENDSEC\n0\nEOF\n";
                File.WriteAllText(path, noGeometry);
                var before = File.ReadAllText(path);

                var inserted = DxfPartLabelPlacer.InsertLabel(path, "TEST-PART");

                Assert.False(inserted);
                Assert.Equal(before, File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Gecerli_geometri_veya_mtext_yoksa_false_doner_ve_dosyaya_dokunulmaz()
        {
            var path = Path.GetTempFileName();
            try
            {
                const string noGeometry = "0\nSECTION\n2\nENTITIES\n0\nENDSEC\n0\nEOF\n";
                File.WriteAllText(path, noGeometry);
                var before = File.ReadAllText(path);

                var moved = DxfPartLabelPlacer.RepositionLabel(path);

                Assert.False(moved);
                Assert.Equal(before, File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static (double X, double Y, double Height, string Content) ReadMText(string path)
        {
            var lines = File.ReadAllLines(path);
            double x = 0, y = 0, height = 0;
            string content = string.Empty;
            bool inMText = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                var code = lines[i].Trim();
                var value = lines[i + 1].Trim();

                if (code == "0" && value == "MTEXT") { inMText = true; continue; }
                if (code == "0") { if (inMText) break; continue; }
                if (!inMText) continue;

                if (code == "10") x = double.Parse(value, CultureInfo.InvariantCulture);
                else if (code == "20") y = double.Parse(value, CultureInfo.InvariantCulture);
                else if (code == "40") height = double.Parse(value, CultureInfo.InvariantCulture);
                else if (code == "1") content = value;
            }

            return (x, y, height, content);
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }
    }
}
