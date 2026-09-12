using System.Collections.Generic;

namespace CadAutomation.Core.Abstractions
{
    /// <summary>
    /// Bir montaj ağacındaki tek bir component occurrence (instance) soyutlaması.
    /// Inventor'da ComponentOccurrence'a karşılık gelir.
    /// </summary>
    public interface ICadOccurrence
    {
        ICadDocument Document { get; }

        /// <summary>Occurrence suppress edilmiş mi (bu durumda analiz/export dışında bırakılır).</summary>
        bool Suppressed { get; }

        /// <summary>Document bir montaj ise alt occurrence'lar; parça ise boş liste.</summary>
        IReadOnlyList<ICadOccurrence> ChildOccurrences { get; }
    }
}
