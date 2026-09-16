using System;
using System.Collections.Generic;
using System.Linq;
using CadAutomation.Core.Abstractions;
using CadAutomation.Core.Models;

namespace CadAutomation.Core.Services
{
    /// <summary>
    /// Analiz edilmiş montajdaki Sheet Metal parçaları tek tek exporter'a gönderir.
    /// Bir parçadaki hata tüm batch'i durdurmaz (madde 21) - IFlatPatternDxfExporter zaten
    /// exception fırlatmak yerine Failed sonucu döndürmesi bekleniyor, ama yine de burada
    /// beklenmedik bir exception (ör. exporter'daki bir bug) batch'i düşürmesin diye try/catch var.
    /// </summary>
    public sealed class BatchProcessor
    {
        private readonly IFlatPatternDxfExporter _exporter;

        public BatchProcessor(IFlatPatternDxfExporter exporter)
        {
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        public BatchResult Process(AssemblyAnalysisResult analysis, BatchExportOptions options)
        {
            if (analysis == null) throw new ArgumentNullException(nameof(analysis));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var results = new List<PartProcessResult>();

            foreach (var part in analysis.UniqueParts.Where(p => p.IsSheetMetal))
            {
                PartProcessResult result;
                try
                {
                    result = _exporter.ExportPart(part, options);
                }
                catch (Exception ex)
                {
                    result = new PartProcessResult(part.Document.DisplayName, PartProcessStatus.Failed, ex.Message);
                }

                results.Add(result);
            }

            if (_exporter is IBatchExportFinalizer finalizer)
            {
                try
                {
                    var finalizerResult = finalizer.CompleteBatch(options);
                    if (finalizerResult != null)
                    {
                        results.Add(finalizerResult);
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new PartProcessResult(
                        "DWG son işlemi",
                        PartProcessStatus.Warning,
                        "DWG çizgi ölçeği uygulanamadı: " + ex.Message));
                }
            }

            return new BatchResult(results);
        }
    }
}
