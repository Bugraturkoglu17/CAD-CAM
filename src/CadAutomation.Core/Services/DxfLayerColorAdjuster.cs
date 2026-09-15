using System.Collections.Generic;
using System.IO;

namespace CadAutomation.Core.Services
{
    /// <summary>
    /// DXF LAYER tablosundaki renk (ACI, kod 62) ve çizgi tipi (kod 6) değerlerini dosya
    /// yazıldıktan SONRA post-process ile günceller. Inventor'ın FlatPattern translator'ı
    /// (WriteDataToFile) options string'i üzerinden sadece layer İSMİNİ ayarlamaya izin veriyor,
    /// renk/çizgi tipini değil - bu yüzden DXF'in kendisi düz metin olduğundan doğrudan üzerinde
    /// çalışıyoruz. Çizgi tipi için hedeflenen isim (ör. "DASHDOT") TABLES/LTYPE bölümünde zaten
    /// tanımlı olmalı - Inventor'ın varsayılan çevirmeni CONTINUOUS/CENTER/DASHDOT/PHANTOM gibi
    /// standart AutoCAD linetype'larını zaten önceden tanımlı olarak ekliyor, ayrıca tanım
    /// eklemeye gerek yok (canlı testte doğrulandı).
    ///
    /// Sadece DXF için çalışır - DWG binary format olduğu için bu yöntem DWG'ye uygulanamaz
    /// (bilinen sınırlama, madde 7).
    /// </summary>
    public static class DxfLayerColorAdjuster
    {
        public static void ApplyLayerColors(string dxfFilePath, IReadOnlyDictionary<string, int> layerColorsByName)
        {
            ApplyLayerStyles(dxfFilePath, layerColorsByName, null);
        }

        public static void ApplyLayerLineTypes(string dxfFilePath, IReadOnlyDictionary<string, string> layerLineTypesByName)
        {
            ApplyLayerStyles(dxfFilePath, null, layerLineTypesByName);
        }

        private static void ApplyLayerStyles(
            string dxfFilePath,
            IReadOnlyDictionary<string, int>? layerColorsByName,
            IReadOnlyDictionary<string, string>? layerLineTypesByName)
        {
            bool hasColors = layerColorsByName != null && layerColorsByName.Count > 0;
            bool hasLineTypes = layerLineTypesByName != null && layerLineTypesByName.Count > 0;
            if (!hasColors && !hasLineTypes) return;

            var lines = File.ReadAllLines(dxfFilePath);
            string? currentLayerName = null;
            bool inLayerRecord = false;

            // DXF ASCII formatı dosya başından itibaren kesintisiz "grup kodu satırı, değer satırı"
            // çiftleri halinde ilerler - bu yüzden i += 2 ile çift bazlı okumak zorunlu. Tek tek her
            // satırın İÇERİĞİNE bakmak yanlış: bir değer satırı da (ör. kod 70'in değeri "0" olabilir)
            // yanlışlıkla yeni bir kayıt başlangıcı ("0" kodu) sanılabilir.
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                var code = lines[i].Trim();
                var value = lines[i + 1].Trim();

                if (code == "0")
                {
                    inLayerRecord = value == "LAYER";
                    currentLayerName = null;
                    continue;
                }

                if (!inLayerRecord) continue;

                if (code == "2")
                {
                    currentLayerName = value;
                    continue;
                }

                if (currentLayerName == null) continue;

                if (hasColors && code == "62" && layerColorsByName!.TryGetValue(currentLayerName, out var aciColor))
                {
                    lines[i + 1] = "     " + aciColor;
                }
                else if (hasLineTypes && code == "6" && layerLineTypesByName!.TryGetValue(currentLayerName, out var lineType))
                {
                    lines[i + 1] = lineType;
                }
            }

            File.WriteAllLines(dxfFilePath, lines);
        }
    }
}
