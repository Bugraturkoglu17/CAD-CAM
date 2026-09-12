using System.Collections.Generic;
using CadAutomation.Core.Services;
using Xunit;

namespace CadAutomation.Core.Tests
{
    public class NamingTemplateEngineTests
    {
        private readonly NamingTemplateEngine _engine = new NamingTemplateEngine();

        [Fact]
        public void Basit_sablon_dosya_adini_uretir()
        {
            var tokens = new Dictionary<string, string?> { ["PartNumber"] = "BRACKET-032" };
            var result = _engine.BuildFileName("{PartNumber}", tokens, "dxf");
            Assert.Equal("BRACKET-032.dxf", result);
        }

        [Fact]
        public void Coklu_token_ve_sabit_metin_birlikte_calisir()
        {
            var tokens = new Dictionary<string, string?>
            {
                ["PartNumber"] = "BRACKET-032",
                ["Revision"] = "B",
            };
            var result = _engine.BuildFileName("{PartNumber}_{Revision}", tokens, ".dxf");
            Assert.Equal("BRACKET-032_B.dxf", result);
        }

        [Fact]
        public void Gecersiz_dosya_karakterleri_alt_tireye_cevrilir()
        {
            var tokens = new Dictionary<string, string?> { ["Description"] = "Kapak / Sağ:Ön" };
            var result = _engine.BuildFileName("{Description}", tokens, "dxf");
            Assert.DoesNotContain("/", result);
            Assert.DoesNotContain(":", result);
        }

        [Fact]
        public void Bilinmeyen_token_oldugu_gibi_birakilir()
        {
            var tokens = new Dictionary<string, string?> { ["PartNumber"] = "X1" };
            var result = _engine.BuildFileName("{PartNumber}_{Bilinmeyen}", tokens, "dxf");
            Assert.Equal("X1_{Bilinmeyen}.dxf", result);
        }
    }
}
