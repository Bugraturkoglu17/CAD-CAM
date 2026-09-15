using System.Collections.Generic;
using System.Linq;

namespace CadAutomation.Core.Models
{
    /// <summary>Batch işlem sonunda gösterilecek özet (madde 11 - Rapor).</summary>
    public sealed class BatchResult
    {
        public BatchResult(IReadOnlyList<PartProcessResult> results)
        {
            Results = results;
        }

        public IReadOnlyList<PartProcessResult> Results { get; }

        public int SuccessCount => Results.Count(r => r.Status == PartProcessStatus.Success);
        public int FailedCount => Results.Count(r => r.Status == PartProcessStatus.Failed);
        public int SkippedCount => Results.Count(r => r.Status == PartProcessStatus.Skipped);
        public int WarningCount => Results.Count(r => r.Status == PartProcessStatus.Warning);

        public IEnumerable<PartProcessResult> Failures => Results.Where(r => r.Status == PartProcessStatus.Failed);
    }
}
