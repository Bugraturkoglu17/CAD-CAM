using System;
using System.Collections.Generic;
using CadAutomation.Core.Abstractions;
using CadAutomation.Core.Models;

namespace CadAutomation.Core.Services
{
    /// <summary>
    /// Montaj ağacını tek geçişte gezer; suppressed occurrence'ları atlar, alt montajlara iner,
    /// parça belgelerini FullFileName anahtarıyla dedup eder ve occurrence (adet) sayısını tutar.
    /// Aynı parçadan montajda N adet varsa bu servis sayesinde parça tek kez işlenir ama adet bilgisi korunur.
    /// </summary>
    public sealed class UniquePartCollector
    {
        public AssemblyAnalysisResult Collect(ICadAssemblyRoot assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            var uniqueByKey = new Dictionary<string, UniquePart>(StringComparer.OrdinalIgnoreCase);
            int totalComponents = 0;
            int skippedSuppressed = 0;

            void Traverse(IReadOnlyList<ICadOccurrence> occurrences)
            {
                for (int i = 0; i < occurrences.Count; i++)
                {
                    var occurrence = occurrences[i];
                    try
                    {
                        if (occurrence.Suppressed)
                        {
                            skippedSuppressed++;
                            continue;
                        }

                        var document = occurrence.Document;
                        totalComponents++;

                        if (document.IsAssemblyDocument)
                        {
                            // Alt montajın kendisi bir "parça" değildir (DXF/sheet metal adayı olamaz),
                            // sadece içindeki occurrence'lara inilir.
                            Traverse(occurrence.ChildOccurrences);
                            continue;
                        }

                        var key = document.FullFileName;
                        if (!uniqueByKey.TryGetValue(key, out var uniquePart))
                        {
                            uniquePart = new UniquePart(document);
                            uniqueByKey[key] = uniquePart;
                        }
                        uniquePart.IncrementOccurrence();
                    }
                    catch (Exception)
                    {
                        // CAD adaptörü unresolved/suppressed bir occurrence için COM hatası
                        // döndürebilir. Tek bozuk referans tüm büyük montaj analizini durdurmasın.
                        skippedSuppressed++;
                    }
                }
            }

            Traverse(assembly.RootOccurrences);

            var uniqueParts = new List<UniquePart>(uniqueByKey.Values);

            return new AssemblyAnalysisResult(
                totalComponentCount: totalComponents,
                skippedSuppressedCount: skippedSuppressed,
                uniqueParts: uniqueParts);
        }
    }
}
