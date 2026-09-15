using System;
using System.Globalization;
using System.IO;
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
    }
}
