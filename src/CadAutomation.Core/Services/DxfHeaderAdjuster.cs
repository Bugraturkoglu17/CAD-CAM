using System.Globalization;
using System.IO;

namespace CadAutomation.Core.Services
{
    /// <summary>
    /// DXF HEADER bölümündeki genel çizim değişkenlerini (ör. $LTSCALE) post-process ile ayarlar.
    ///
    /// Neden gerekli: Inventor'ın DASHDOT linetype tanımı 1.0 çizim birimi (mm) uzunluğunda bir
    /// desen tekrarı kullanıyor - muhtemelen orijinal AutoCAD "acad.lin" kütüphanesindeki inç bazlı
    /// tanımın (~0.5 inç ≈ 12.7mm) birim dönüşümü yapılmadan aktarılmış hali. Sonuç: normal
    /// boyuttaki bir sac parçasında (onlarca-yüzlerce mm) desen o kadar sık tekrarlıyor ki
    /// ekranda/çıktıda neredeyse düz bir çizgi gibi görünüyor. $LTSCALE'i büyütmek deseni "gerer"
    /// ve gerçekten kesikli-noktalı görünmesini sağlar. Sadece kesikli/noktalı (non-Continuous)
    /// linetype kullanan entity'leri etkiler - CUT gibi Continuous çizgiler bundan etkilenmez.
    /// </summary>
    public static class DxfHeaderAdjuster
    {
        public static void SetLineTypeScale(string dxfFilePath, double scale)
        {
            var lines = File.ReadAllLines(dxfFilePath);

            for (int i = 0; i + 3 < lines.Length; i += 2)
            {
                var code = lines[i].Trim();
                var value = lines[i + 1].Trim();

                if (code == "9" && value == "$LTSCALE")
                {
                    // Bir sonraki (kod, değer) çifti $LTSCALE'in gerçek sayısal değeri (kod 40).
                    lines[i + 3] = scale.ToString(CultureInfo.InvariantCulture);
                    break;
                }
            }

            File.WriteAllLines(dxfFilePath, lines);
        }
    }
}
