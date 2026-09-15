using System.Collections.Generic;
using System.IO;
using CadAutomation.Core.Services;
using Xunit;

namespace CadAutomation.Core.Tests
{
    public class DxfLayerColorAdjusterTests
    {
        private const string SampleDxf =
            "  0\r\nSECTION\r\n  2\r\nTABLES\r\n  0\r\nTABLE\r\n  2\r\nLAYER\r\n" +
            "  0\r\nLAYER\r\n  5\r\nA8\r\n100\r\nAcDbSymbolTableRecord\r\n100\r\nAcDbLayerTableRecord\r\n" +
            "  2\r\nCUT\r\n 70\r\n     0\r\n 62\r\n     7\r\n  6\r\nContinuous\r\n" +
            "  0\r\nLAYER\r\n  5\r\nA9\r\n100\r\nAcDbSymbolTableRecord\r\n100\r\nAcDbLayerTableRecord\r\n" +
            "  2\r\nBEND_UP\r\n 70\r\n     0\r\n 62\r\n     7\r\n  6\r\nContinuous\r\n" +
            "  0\r\nENDTAB\r\n  0\r\nENDSEC\r\n  0\r\nEOF\r\n";

        [Fact]
        public void Hedeflenen_layerin_rengi_degistirilir_digerleri_dokunulmaz()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);

                DxfLayerColorAdjuster.ApplyLayerColors(path, new Dictionary<string, int>
                {
                    ["BEND_UP"] = 1, // kırmızı
                });

                var lines = File.ReadAllLines(path);
                var cutColorLine = FindValueAfterLayerColor(lines, "CUT");
                var bendUpColorLine = FindValueAfterLayerColor(lines, "BEND_UP");

                Assert.Equal("7", cutColorLine.Trim());
                Assert.Equal("1", bendUpColorLine.Trim());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Layer_haritasi_bossa_dosyaya_dokunulmaz()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);
                var before = File.ReadAllText(path);

                DxfLayerColorAdjuster.ApplyLayerColors(path, new Dictionary<string, int>());

                Assert.Equal(before, File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Hedeflenen_layerin_cizgi_tipi_degistirilir_digerleri_dokunulmaz()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);

                DxfLayerColorAdjuster.ApplyLayerLineTypes(path, new Dictionary<string, string>
                {
                    ["BEND_UP"] = "DASHDOT",
                });

                var lines = File.ReadAllLines(path);
                Assert.Equal("Continuous", FindValueAfterLayerCode(lines, "CUT", "6").Trim());
                Assert.Equal("DASHDOT", FindValueAfterLayerCode(lines, "BEND_UP", "6").Trim());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Renk_ve_cizgi_tipi_ayni_anda_farkli_cagrilarla_uygulanabilir()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);

                DxfLayerColorAdjuster.ApplyLayerColors(path, new Dictionary<string, int> { ["BEND_UP"] = 1 });
                DxfLayerColorAdjuster.ApplyLayerLineTypes(path, new Dictionary<string, string> { ["BEND_UP"] = "DASHDOT" });

                var lines = File.ReadAllLines(path);
                Assert.Equal("1", FindValueAfterLayerCode(lines, "BEND_UP", "62").Trim());
                Assert.Equal("DASHDOT", FindValueAfterLayerCode(lines, "BEND_UP", "6").Trim());
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string FindValueAfterLayerColor(string[] lines, string layerName)
            => FindValueAfterLayerCode(lines, layerName, "62");

        private static string FindValueAfterLayerCode(string[] lines, string layerName, string targetCode)
        {
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "2" && lines[i + 1].Trim() == layerName)
                {
                    for (int j = i; j + 1 < lines.Length; j += 2)
                    {
                        if (lines[j].Trim() == "0") break;
                        if (lines[j].Trim() == targetCode)
                        {
                            return lines[j + 1];
                        }
                    }
                }
            }
            throw new System.InvalidOperationException("Layer bulunamadı: " + layerName);
        }
    }
}
