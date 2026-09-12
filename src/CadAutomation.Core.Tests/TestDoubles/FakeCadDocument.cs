using CadAutomation.Core.Abstractions;

namespace CadAutomation.Core.Tests.TestDoubles
{
    public sealed class FakeCadDocument : ICadDocument
    {
        public FakeCadDocument(
            string fullFileName,
            bool isAssemblyDocument = false,
            bool isSheetMetal = false,
            double? thicknessCm = null,
            bool hasFlatPattern = false,
            string? partNumber = null,
            string? description = null,
            string? revision = null)
        {
            FullFileName = fullFileName;
            DisplayName = System.IO.Path.GetFileNameWithoutExtension(fullFileName);
            IsAssemblyDocument = isAssemblyDocument;
            IsSheetMetal = isSheetMetal;
            ThicknessCm = thicknessCm;
            HasFlatPattern = hasFlatPattern;
            PartNumber = partNumber;
            Description = description;
            Revision = revision;
        }

        public string FullFileName { get; }
        public string DisplayName { get; }
        public bool IsAssemblyDocument { get; }
        public bool IsSheetMetal { get; }
        public double? ThicknessCm { get; }
        public bool HasFlatPattern { get; }
        public string? PartNumber { get; }
        public string? Description { get; }
        public string? Revision { get; }
    }
}
