using System.Collections.Generic;
using CadAutomation.Core.Abstractions;

namespace CadAutomation.Core.Tests.TestDoubles
{
    public sealed class FakeCadAssemblyRoot : ICadAssemblyRoot
    {
        public FakeCadAssemblyRoot(IReadOnlyList<ICadOccurrence> rootOccurrences)
        {
            RootOccurrences = rootOccurrences;
        }

        public IReadOnlyList<ICadOccurrence> RootOccurrences { get; }
    }
}
