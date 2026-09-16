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
        private readonly Lazy<ICadDocument> _document;
        private readonly Lazy<IReadOnlyList<ICadOccurrence>> _children;

        public InventorCadOccurrence(Inv.ComponentOccurrence occurrence)
        {
            _occurrence = occurrence ?? throw new ArgumentNullException(nameof(occurrence));
            // Suppressed/unresolved occurrence'larda Definition.Document doğrudan çağrılırsa
            // Inventor E_FAIL (0x80004005) verebilir. Belgeyi ancak occurrence gerçekten
            // işleneceği zaman çözüyoruz; böylece büyük montaj analizi Inventor'ı düşürmez.
            _document = new Lazy<ICadDocument>(BuildDocument);
            _children = new Lazy<IReadOnlyList<ICadOccurrence>>(BuildChildren);
        }

        public ICadDocument Document => _document.Value;

        public bool Suppressed
        {
            get
            {
                try
                {
                    if (_occurrence.Suppressed) return true;

                    // Bazı unresolved/express-mode occurrence'lar Suppressed=false döndürürken
                    // Definition.Document erişiminde E_FAIL verir. Bunları da güvenle atla.
                    _ = _document.Value;
                    return false;
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }

        public IReadOnlyList<ICadOccurrence> ChildOccurrences => _children.Value;

        private IReadOnlyList<ICadOccurrence> BuildChildren()
        {
            if (Suppressed || !Document.IsAssemblyDocument)
            {
                return Array.Empty<ICadOccurrence>();
            }

            var children = new List<ICadOccurrence>();
            try
            {
                foreach (Inv.ComponentOccurrence child in _occurrence.SubOccurrences)
                {
                    // Constructor artık Definition.Document'a dokunmadığı için bozuk bir çocuk
                    // occurrence diğer erişilebilir kardeşlerin analizini engellemez.
                    children.Add(new InventorCadOccurrence(child));
                }
            }
            catch (Exception)
            {
                // Inventor bazı kısmi yüklenmiş alt montajlarda SubOccurrences enumerasyonunu
                // yarıda kesebilir. O ana kadar alınan erişilebilir çocuklarla devam et.
            }

            return children;
        }

        private ICadDocument BuildDocument()
        {
            return new InventorCadDocument((Inv.Document)_occurrence.Definition.Document);
        }
    }
}
