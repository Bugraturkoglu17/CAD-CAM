using System;
using System.Collections.Generic;
using System.IO;

namespace CadAutomation.Core.Services
{
    /// <summary>
    /// Inventor'ın flat pattern DXF çıktısına varsayılan olarak eklediği ama üretim/lazer kesim
    /// için istenmeyen referans geometrisini (tangent/feature-profile çizgileri - büküm merkez
    /// çizgisinin sağında/solunda görünen, kesilmemesi gereken çizgiler) kaldırır.
    ///
    /// DXF'te her entity kaydı "0" koduyla başlar ve grup kodu 8 ile hangi layer'a ait olduğunu
    /// belirtir. Bu sınıf, hedef layer'lardaki TÜM entity kayıtlarını (LINE, ARC, vb. fark etmeksizin)
    /// dosyadan komple çıkarır. Layer'ın kendisinin TABLES/LAYER tanımı silinmiyor (zararsız,
    /// boş/kullanılmayan bir layer tanımı üretim çıktısını etkilemiyor) - sadece geometri siliniyor.
    /// </summary>
    public static class DxfLayerRemover
    {
        public static void RemoveLayerGeometry(string dxfFilePath, IReadOnlyCollection<string> layerNamesToRemove)
        {
            if (layerNamesToRemove == null || layerNamesToRemove.Count == 0) return;

            var removeSet = new HashSet<string>(layerNamesToRemove, StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(dxfFilePath);
            var output = new List<string>(lines.Length);

            int i = 0;
            while (i < lines.Length)
            {
                if (i + 1 >= lines.Length || lines[i].Trim() != "0")
                {
                    // Beklenmeyen hizalama (olmamalı ama dosyayı bozmamak için olduğu gibi geçir).
                    output.Add(lines[i]);
                    i++;
                    continue;
                }

                // Bu kaydın ("0" ... bir sonraki "0"'a kadar) sınırlarını bul.
                int recordStart = i;
                int j = i + 2;
                while (j + 1 < lines.Length && lines[j].Trim() != "0")
                {
                    j += 2;
                }

                bool shouldRemove = false;
                for (int k = recordStart; k + 1 < j; k += 2)
                {
                    if (lines[k].Trim() == "8" && removeSet.Contains(lines[k + 1].Trim()))
                    {
                        shouldRemove = true;
                        break;
                    }
                }

                if (!shouldRemove)
                {
                    for (int k = recordStart; k < j; k++) output.Add(lines[k]);
                }

                i = j;
            }

            File.WriteAllLines(dxfFilePath, output);
        }
    }
}
