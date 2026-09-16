using System.Collections.Generic;
using System.Linq;
using CadAutomation.Core.Abstractions;
using CadAutomation.Core.Services;
using CadAutomation.Core.Tests.TestDoubles;
using Xunit;

namespace CadAutomation.Core.Tests
{
    public class UniquePartCollectorTests
    {
        [Fact]
        public void Ayni_parcadan_yirmi_instance_tek_unique_part_olarak_sayilir_ama_adet_korunur()
        {
            var bracket = new FakeCadDocument(@"C:\parts\bracket.ipt", isSheetMetal: true, thicknessCm: 0.2);
            var occurrences = Enumerable.Range(0, 20)
                .Select(_ => (Core.Abstractions.ICadOccurrence)new FakeCadOccurrence(bracket))
                .ToList();

            var assembly = new FakeCadAssemblyRoot(occurrences);
            var result = new UniquePartCollector().Collect(assembly);

            Assert.Equal(20, result.TotalComponentCount);
            Assert.Equal(1, result.UniquePartCount);
            Assert.Equal(20, result.UniqueParts.Single().OccurrenceCount);
        }

        [Fact]
        public void Nested_subassembly_icindeki_parcalar_dogru_sekilde_dedup_edilir()
        {
            var bolt = new FakeCadDocument(@"C:\parts\bolt.ipt", isSheetMetal: false);
            var cover = new FakeCadDocument(@"C:\parts\cover.ipt", isSheetMetal: true, thicknessCm: 0.15);

            var innerSub = new FakeCadOccurrence(
                new FakeCadDocument(@"C:\asm\inner_sub.iam", isAssemblyDocument: true),
                children: new List<Core.Abstractions.ICadOccurrence>
                {
                    new FakeCadOccurrence(bolt),
                    new FakeCadOccurrence(bolt),
                    new FakeCadOccurrence(cover),
                });

            var outerSub = new FakeCadOccurrence(
                new FakeCadDocument(@"C:\asm\outer_sub.iam", isAssemblyDocument: true),
                children: new List<Core.Abstractions.ICadOccurrence> { innerSub, new FakeCadOccurrence(bolt) });

            var assembly = new FakeCadAssemblyRoot(new List<Core.Abstractions.ICadOccurrence> { outerSub });
            var result = new UniquePartCollector().Collect(assembly);

            // Toplam: outerSub(1) + innerSub(1) + bolt(2) + cover(1) + bolt(1) = 6 occurrence
            // (alt montajların kendisi de birer occurrence olarak sayılır, ama unique part değildir)
            Assert.Equal(6, result.TotalComponentCount);
            Assert.Equal(2, result.UniquePartCount); // sadece bolt ve cover, alt montajlar unique part sayılmaz
            Assert.Equal(1, result.SheetMetalCount);

            var boltResult = result.UniqueParts.Single(p => p.Document.FullFileName == bolt.FullFileName);
            Assert.Equal(3, boltResult.OccurrenceCount);
        }

        [Fact]
        public void Suppressed_occurrence_atlanir_ve_ayrica_sayilir()
        {
            var part = new FakeCadDocument(@"C:\parts\part.ipt");
            var occurrences = new List<Core.Abstractions.ICadOccurrence>
            {
                new FakeCadOccurrence(part),
                new FakeCadOccurrence(part, suppressed: true),
            };

            var assembly = new FakeCadAssemblyRoot(occurrences);
            var result = new UniquePartCollector().Collect(assembly);

            Assert.Equal(1, result.TotalComponentCount);
            Assert.Equal(1, result.SkippedSuppressedCount);
            Assert.Equal(1, result.UniqueParts.Single().OccurrenceCount);
        }

        [Fact]
        public void Sheet_metal_olmayan_parcalar_sheet_metal_sayisina_dahil_edilmez()
        {
            var sheetMetalPart = new FakeCadDocument(@"C:\parts\sm.ipt", isSheetMetal: true, thicknessCm: 0.3);
            var machinedPart = new FakeCadDocument(@"C:\parts\machined.ipt", isSheetMetal: false);

            var assembly = new FakeCadAssemblyRoot(new List<Core.Abstractions.ICadOccurrence>
            {
                new FakeCadOccurrence(sheetMetalPart),
                new FakeCadOccurrence(machinedPart),
            });

            var result = new UniquePartCollector().Collect(assembly);

            Assert.Equal(2, result.UniquePartCount);
            Assert.Equal(1, result.SheetMetalCount);
        }

        [Fact]
        public void Erisilemeyen_occurrence_atlanir_ve_diger_parcalar_analiz_edilir()
        {
            var first = new FakeCadDocument(@"C:\parts\first.ipt", isSheetMetal: true);
            var second = new FakeCadDocument(@"C:\parts\second.ipt", isSheetMetal: true);
            var assembly = new FakeCadAssemblyRoot(new List<ICadOccurrence>
            {
                new FakeCadOccurrence(first),
                new FailingCadOccurrence(),
                new FakeCadOccurrence(second),
            });

            var result = new UniquePartCollector().Collect(assembly);

            Assert.Equal(2, result.TotalComponentCount);
            Assert.Equal(2, result.UniquePartCount);
            Assert.Equal(1, result.SkippedSuppressedCount);
        }

        private sealed class FailingCadOccurrence : ICadOccurrence
        {
            public ICadDocument Document => throw new System.Runtime.InteropServices.COMException("E_FAIL");
            public bool Suppressed => false;
            public IReadOnlyList<ICadOccurrence> ChildOccurrences => System.Array.Empty<ICadOccurrence>();
        }
    }
}
