using System.Collections.Generic;
using System.IO;
using System.Linq;
using CadAutomation.Core.Services;
using Xunit;

namespace CadAutomation.Core.Tests
{
    public class DxfLayerRemoverTests
    {
        // Basitleştirilmiş ama gerçekçi bir DXF: ENTITIES bölümünde CUT, BEND_UP ve
        // IV_TANGENT layer'larına ait birer LINE entity'si var.
        private const string SampleDxf =
            "  0\r\nSECTION\r\n  2\r\nENTITIES\r\n" +
            "  0\r\nLINE\r\n  5\r\n1A\r\n  8\r\nCUT\r\n 10\r\n0.0\r\n 20\r\n0.0\r\n 11\r\n10.0\r\n 21\r\n0.0\r\n" +
            "  0\r\nLINE\r\n  5\r\n1B\r\n  8\r\nBEND_UP\r\n 10\r\n1.0\r\n 20\r\n1.0\r\n 11\r\n1.0\r\n 21\r\n9.0\r\n" +
            "  0\r\nLINE\r\n  5\r\n1C\r\n  8\r\nIV_TANGENT\r\n 10\r\n2.0\r\n 20\r\n1.0\r\n 11\r\n2.0\r\n 21\r\n9.0\r\n" +
            "  0\r\nENDSEC\r\n  0\r\nEOF\r\n";

        [Fact]
        public void Hedeflenen_layerdaki_entity_tamamen_kaldirilir_digerleri_kalir()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);

                DxfLayerRemover.RemoveLayerGeometry(path, new[] { "IV_TANGENT" });

                var content = File.ReadAllText(path);
                Assert.DoesNotContain("IV_TANGENT", content);
                Assert.Contains("CUT", content);
                Assert.Contains("BEND_UP", content);

                // 3 LINE vardı, 1'i silinmeli -> 2 kalmalı.
                var lineCount = File.ReadAllLines(path).Count(l => l.Trim() == "LINE");
                Assert.Equal(2, lineCount);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Birden_fazla_layer_ayni_anda_kaldirilabilir()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);

                DxfLayerRemover.RemoveLayerGeometry(path, new[] { "IV_TANGENT", "BEND_UP" });

                var content = File.ReadAllText(path);
                Assert.DoesNotContain("IV_TANGENT", content);
                Assert.DoesNotContain("BEND_UP", content);
                Assert.Contains("CUT", content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Bos_liste_dosyaya_dokunmaz()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);
                var before = File.ReadAllText(path);

                DxfLayerRemover.RemoveLayerGeometry(path, System.Array.Empty<string>());

                Assert.Equal(before, File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
