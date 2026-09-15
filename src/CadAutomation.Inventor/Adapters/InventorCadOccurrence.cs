using System;
using System.Collections.Generic;
using CadAutomation.Core.Abstractions;
using Inv = Inventor;

namespace CadAutomation.Inventor.Adapters
{
    /// <summary>
    /// ICadOccurrence'ın Inventor ComponentOccurrence üzerinden implementasyonu.
    /// Alt occurrence'lar (SubOccurrences) sadece belge bir montaj ise okunur - Inventor API
    /// bu property'yi parça occurrence'larında çağırınca hata verir.
    /// </summary>
    public sealed class InventorCadOccurrence : ICadOccurrence
    {
        private readonly Inv.ComponentOccurrence _occurrence;
        private readonly Lazy<IReadOnlyList<ICadOccurrence>> _children;

        public InventorCadOccurrence(Inv.ComponentOccurrence occurrence)
        {
            _occurrence = occurrence ?? throw new ArgumentNullException(nameof(occurrence));
            Document = new InventorCadDocument((Inv.Document)occurrence.Definition.Document);
            _children = new Lazy<IReadOnlyList<ICadOccurrence>>(BuildChildren);
        }

        public ICadDocument Document { get; }

        public bool Suppressed => _occurrence.Suppressed;

        public IReadOnlyList<ICadOccurrence> ChildOccurrences => _children.Value;

        private IReadOnlyList<ICadOccurrence> BuildChildren()
        {
            if (!Document.IsAssemblyDocument)
            {
                return Array.Empty<ICadOccurrence>();
            }

            var children = new List<ICadOccurrence>();
            foreach (Inv.ComponentOccurrence child in _occurrence.SubOccurrences)
            {
                children.Add(new InventorCadOccurrence(child));
            }
            return children;
        }
    }
}
