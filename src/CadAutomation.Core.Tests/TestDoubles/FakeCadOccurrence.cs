using System.Collections.Generic;
using CadAutomation.Core.Abstractions;

namespace CadAutomation.Core.Tests.TestDoubles
{
    public sealed class FakeCadOccurrence : ICadOccurrence
    {
        public FakeCadOccurrence(ICadDocument document, bool suppressed = false, IReadOnlyList<ICadOccurrence>? children = null)
        {
            Document = document;
            Suppressed = suppressed;
            ChildOccurrences = children ?? System.Array.Empty<ICadOccurrence>();
        }

        public ICadDocument Document { get; }
        public bool Suppressed { get; }
        public IReadOnlyList<ICadOccurrence> ChildOccurrences { get; }
    }
}
