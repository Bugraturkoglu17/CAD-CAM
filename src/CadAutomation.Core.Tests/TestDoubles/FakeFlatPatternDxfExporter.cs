using System;
using System.Collections.Generic;
using CadAutomation.Core.Abstractions;
using CadAutomation.Core.Models;

namespace CadAutomation.Core.Tests.TestDoubles
{
    public sealed class FakeFlatPatternDxfExporter : IFlatPatternDxfExporter, IBatchExportFinalizer
    {
        private readonly Dictionary<string, PartProcessResult> _resultsByFileName = new Dictionary<string, PartProcessResult>();
        public List<string> ExportedFileNames { get; } = new List<string>();
        public bool BatchCompleted { get; private set; }
        public PartProcessResult? BatchCompletionResult { get; set; }

        public void SetResult(string fullFileName, PartProcessResult result)
        {
            _resultsByFileName[fullFileName] = result;
        }

        public PartProcessResult ExportPart(UniquePart part, BatchExportOptions options)
        {
            ExportedFileNames.Add(part.Document.FullFileName);

            if (_resultsByFileName.TryGetValue(part.Document.FullFileName, out var predefined))
            {
                if (predefined.Status == PartProcessStatus.Failed && predefined.Message == "__throw__")
                {
                    throw new InvalidOperationException("Simüle edilmiş beklenmedik hata.");
                }
                return predefined;
            }

            return new PartProcessResult(part.Document.DisplayName, PartProcessStatus.Success, "OK");
        }

        public PartProcessResult? CompleteBatch(BatchExportOptions options)
        {
            BatchCompleted = true;
            return BatchCompletionResult;
        }
    }
}
