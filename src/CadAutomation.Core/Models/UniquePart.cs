using System;
using CadAutomation.Core.Abstractions;

namespace CadAutomation.Core.Models
{
    /// <summary>
    /// Montaj içinde bir veya daha fazla occurrence'ı bulunan, dedup edilmiş tekil parça.
    /// Aynı parçadan montajda 20 instance varsa, bu tip bir kez oluşur ve OccurrenceCount = 20 olur.
    /// </summary>
    public sealed class UniquePart
    {
        public UniquePart(ICadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public ICadDocument Document { get; }

        /// <summary>Bu parçanın montaj içinde kaç occurrence'ı (instance/adet) var.</summary>
        public int OccurrenceCount { get; private set; }

        public bool IsSheetMetal => Document.IsSheetMetal;

        internal void IncrementOccurrence() => OccurrenceCount++;
    }
}
