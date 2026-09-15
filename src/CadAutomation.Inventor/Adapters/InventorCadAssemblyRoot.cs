using System;
using System.Collections.Generic;
using CadAutomation.Core.Abstractions;
using Inv = Inventor;

namespace CadAutomation.Inventor.Adapters
{
    /// <summary>
    /// ICadAssemblyRoot'un, kullanıcının Inventor'da o an aktif olan AssemblyDocument'i
    /// üzerinden implementasyonu. "Analyze Assembly" butonuna basıldığında bir kez oluşturulur.
    /// </summary>
    public sealed class InventorCadAssemblyRoot : ICadAssemblyRoot
    {
        public InventorCadAssemblyRoot(Inv.AssemblyDocument assemblyDocument)
        {
            if (assemblyDocument == null) throw new ArgumentNullException(nameof(assemblyDocument));

            var occurrences = new List<ICadOccurrence>();
            foreach (Inv.ComponentOccurrence occurrence in assemblyDocument.ComponentDefinition.Occurrences)
            {
                occurrences.Add(new InventorCadOccurrence(occurrence));
            }
            RootOccurrences = occurrences;
        }

        public IReadOnlyList<ICadOccurrence> RootOccurrences { get; }
    }
}
