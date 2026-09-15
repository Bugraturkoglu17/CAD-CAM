using System.IO;
using CadAutomation.Core.Services;
using Xunit;

namespace CadAutomation.Core.Tests
{
    public class DxfHeaderAdjusterTests
    {
        private const string SampleDxf =
            "  0\r\nSECTION\r\n  2\r\nHEADER\r\n" +
            "  9\r\n$ACADVER\r\n  1\r\nAC1018\r\n" +
            "  9\r\n$LTSCALE\r\n 40\r\n1.0\r\n" +
            "  9\r\n$INSUNITS\r\n 70\r\n     4\r\n" +
            "  0\r\nENDSEC\r\n  0\r\nEOF\r\n";

        [Fact]
        public void Ltscale_degeri_dogru_sekilde_guncellenir()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);

                DxfHeaderAdjuster.SetLineTypeScale(path, 10);

                var lines = File.ReadAllLines(path);
                var ltscaleIndex = System.Array.FindIndex(lines, l => l.Trim() == "$LTSCALE");
                var valueLine = lines[ltscaleIndex + 2].Trim();

                Assert.Equal("10", valueLine);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Diger_header_degiskenlerine_dokunulmaz()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, SampleDxf);

                DxfHeaderAdjuster.SetLineTypeScale(path, 10);

                var content = File.ReadAllText(path);
                Assert.Contains("AC1018", content);
                Assert.Contains("$INSUNITS", content);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
