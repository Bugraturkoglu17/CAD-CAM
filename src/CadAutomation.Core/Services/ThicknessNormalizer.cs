using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CadAutomation.Core.Services
{
    /// <summary>Kalınlık klasör adı formatlama ayarları (madde 6 - Ayarlar / DXF bölümü).</summary>
    public sealed class ThicknessFormatOptions
    {
        public int DecimalPlaces { get; set; } = 2;
        public bool TrimTrailingZeros { get; set; } = true;
        public string DecimalSeparator { get; set; } = ".";
        public string UnitSuffix { get; set; } = "mm";
    }

    /// <summary>
    /// Kalınlık değerlerini tutarlı, tek bir klasör adına çevirir. Kaynak her zaman sayısal
    /// Inventor Thickness parametresi (cm) olduğu için "1" / "1.0" / "1,0 mm" gibi farklı yazımların
    /// farklı klasörlere gitmesi sorunu kaynağında ortadan kalkar.
    /// TryParseLegacyMillimeterText, sadece güvenilmeyen string iProperty gibi kaynaklar için fallback'tir.
    /// </summary>
    public static class ThicknessNormalizer
    {
        private const double CmToMm = 10.0;
        private static readonly Regex MillimeterSuffixPattern = new Regex("mm", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string ToFolderName(double thicknessCm, ThicknessFormatOptions? options = null)
        {
            options ??= new ThicknessFormatOptions();

            double millimeters = thicknessCm * CmToMm;
            double rounded = Math.Round(millimeters, options.DecimalPlaces, MidpointRounding.AwayFromZero);

            string formatted = rounded.ToString("F" + options.DecimalPlaces, CultureInfo.InvariantCulture);

            if (options.TrimTrailingZeros && formatted.Contains("."))
            {
                formatted = formatted.TrimEnd('0');
                formatted = formatted.TrimEnd('.');
            }

            if (options.DecimalSeparator != ".")
            {
                formatted = formatted.Replace(".", options.DecimalSeparator);
            }

            return formatted + options.UnitSuffix;
        }

        /// <summary>
        /// "1", "1.0", "1,0 mm", "1,5mm" gibi tutarsız string kaynaklardan milimetre değeri çıkarmaya çalışır.
        /// Ayrıştıramazsa null döner; çağıran taraf bunu "Thickness okunamadı" hatasına çevirmelidir.
        /// </summary>
        public static double? TryParseLegacyMillimeterText(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var cleaned = MillimeterSuffixPattern.Replace(raw, string.Empty)
                .Replace(" ", string.Empty)
                .Replace(",", ".");

            return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : (double?)null;
        }
    }
}
