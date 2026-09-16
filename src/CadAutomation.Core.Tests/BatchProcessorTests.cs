using System.Collections.Generic;
using CadAutomation.Core.Models;
using CadAutomation.Core.Services;
using CadAutomation.Core.Tests.TestDoubles;
using Xunit;

namespace CadAutomation.Core.Tests
{
    public class BatchProcessorTests
    {
        [Fact]
        public void Sadece_sheet_metal_parcalar_islenir()
        {
            var sheetMetal = new FakeCadDocument(@"C:\parts\sm.ipt", isSheetMetal: true, thicknessCm: 0.2);
            var machined = new FakeCadDocument(@"C:\parts\machined.ipt", isSheetMetal: false);

            var analysis = new AssemblyAnalysisResult(
                totalComponentCount: 2,
                skippedSuppressedCount: 0,
                uniqueParts: new List<UniquePart> { MakeUniquePart(sheetMetal), MakeUniquePart(machined) });

            var exporter = new FakeFlatPatternDxfExporter();
            var processor = new BatchProcessor(exporter);

            var result = processor.Process(analysis, new BatchExportOptions { OutputFolder = @"C:\out" });

            Assert.Single(result.Results);
            Assert.Contains(sheetMetal.FullFileName, exporter.ExportedFileNames);
            Assert.DoesNotContain(machined.FullFileName, exporter.ExportedFileNames);
        }

        [Fact]
        public void Bir_parcadaki_hata_digerlerinin_islenmesini_engellemez()
        {
            var failing = new FakeCadDocument(@"C:\parts\fail.ipt", isSheetMetal: true, thicknessCm: 0.1);
            var ok = new FakeCadDocument(@"C:\parts\ok.ipt", isSheetMetal: true, thicknessCm: 0.1);

            var analysis = new AssemblyAnalysisResult(
                totalComponentCount: 2,
                skippedSuppressedCount: 0,
                uniqueParts: new List<UniquePart> { MakeUniquePart(failing), MakeUniquePart(ok) });

            var exporter = new FakeFlatPatternDxfExporter();
            exporter.SetResult(failing.FullFileName, new PartProcessResult("fail", PartProcessStatus.Failed, "Flat Pattern oluşturulamadı."));

            var processor = new BatchProcessor(exporter);
            var result = processor.Process(analysis, new BatchExportOptions { OutputFolder = @"C:\out" });

            Assert.Equal(2, result.Results.Count);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal(1, result.SuccessCount);
        }

        [Fact]
        public void Exporterin_beklenmedik_exception_firlatmasi_batch_i_dusurmez()
        {
            var throwing = new FakeCadDocument(@"C:\parts\throw.ipt", isSheetMetal: true, thicknessCm: 0.1);
            var ok = new FakeCadDocument(@"C:\parts\ok.ipt", isSheetMetal: true, thicknessCm: 0.1);

            var analysis = new AssemblyAnalysisResult(
                totalComponentCount: 2,
                skippedSuppressedCount: 0,
                uniqueParts: new List<UniquePart> { MakeUniquePart(throwing), MakeUniquePart(ok) });

            var exporter = new FakeFlatPatternDxfExporter();
            exporter.SetResult(throwing.FullFileName, new PartProcessResult("throw", PartProcessStatus.Failed, "__throw__"));

            var processor = new BatchProcessor(exporter);
            var result = processor.Process(analysis, new BatchExportOptions { OutputFolder = @"C:\out" });

            Assert.Equal(2, result.Results.Count);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal(1, result.SuccessCount);
        }

        [Fact]
        public void Tum_parcalar_bittikten_sonra_batch_finalizer_cagrilir()
        {
            var part = new FakeCadDocument(@"C:\parts\sm.ipt", isSheetMetal: true, thicknessCm: 0.2);
            var analysis = new AssemblyAnalysisResult(
                totalComponentCount: 1,
                skippedSuppressedCount: 0,
                uniqueParts: new List<UniquePart> { MakeUniquePart(part) });
            var exporter = new FakeFlatPatternDxfExporter();

            var result = new BatchProcessor(exporter).Process(
                analysis,
                new BatchExportOptions { OutputFolder = @"C:\out" });

            Assert.True(exporter.BatchCompleted);
            Assert.Single(result.Results);
        }

        private static UniquePart MakeUniquePart(FakeCadDocument document) => new UniquePart(document);
    }
}
