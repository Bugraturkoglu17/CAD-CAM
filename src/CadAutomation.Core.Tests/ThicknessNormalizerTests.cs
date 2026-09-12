using CadAutomation.Core.Services;
using Xunit;

namespace CadAutomation.Core.Tests
{
    public class ThicknessNormalizerTests
    {
        [Theory]
        [InlineData(0.1, "1mm")]   // 1mm sac -> Inventor'da 0.1 cm
        [InlineData(0.15, "1.5mm")]
        [InlineData(0.2, "2mm")]
        public void Sayisal_kalinlik_tutarli_klasor_adina_donusur(double thicknessCm, string expected)
        {
            var result = ThicknessNormalizer.ToFolderName(thicknessCm);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Ondalik_ayrac_ozellestirilebilir()
        {
            var options = new ThicknessFormatOptions { DecimalSeparator = "," };
            var result = ThicknessNormalizer.ToFolderName(0.15, options);
            Assert.Equal("1,5mm", result);
        }

        [Theory]
        [InlineData("1", 1.0)]
        [InlineData("1.0", 1.0)]
        [InlineData("1,0 mm", 1.0)]
        [InlineData("1,5mm", 1.5)]
        [InlineData("2 MM", 2.0)]
        public void Farkli_yazimlar_ayni_sayisal_degere_ayristirilir(string raw, double expectedMm)
        {
            var value = ThicknessNormalizer.TryParseLegacyMillimeterText(raw);
            Assert.NotNull(value);
            Assert.Equal(expectedMm, value!.Value, 3);
        }

        [Fact]
        public void Ayristirilamayan_metin_null_doner()
        {
            var value = ThicknessNormalizer.TryParseLegacyMillimeterText("bilinmiyor");
            Assert.Null(value);
        }
    }
}
