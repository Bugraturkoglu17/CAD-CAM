using System.Collections.Generic;

namespace CadAutomation.Core.Models
{
    /// <summary>
    /// Kullanıcının "ANALİZ ET" butonuna basmasının sonucu — batch işlem başlamadan önce
    /// gösterilen ön analiz özeti (Toplam Component / Unique Part / Sheet Metal ...).
    /// </summary>
    public sealed class AssemblyAnalysisResult
    {
        public AssemblyAnalysisResult(
            int totalComponentCount,
            int skippedSuppressedCount,
            IReadOnlyList<UniquePart> uniqueParts)
        {
            TotalComponentCount = totalComponentCount;
            SkippedSuppressedCount = skippedSuppressedCount;
            UniqueParts = uniqueParts;
        }

        /// <summary>Suppress edilmemiş, montaj ağacındaki tüm occurrence'ların toplamı (parça + alt montaj instance'ları).</summary>
        public int TotalComponentCount { get; }

        public int SkippedSuppressedCount { get; }

        /// <summary>Dedup edilmiş tekil parçalar (alt montajlar hariç, sadece parça belgeleri).</summary>
        public IReadOnlyList<UniquePart> UniqueParts { get; }

        public int UniquePartCount => UniqueParts.Count;

        public int SheetMetalCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < UniqueParts.Count; i++)
                {
                    if (UniqueParts[i].IsSheetMetal) count++;
                }
                return count;
            }
        }
    }
}
