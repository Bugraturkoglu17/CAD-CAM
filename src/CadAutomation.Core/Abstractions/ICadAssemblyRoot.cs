using System.Collections.Generic;

namespace CadAutomation.Core.Abstractions
{
    /// <summary>
    /// Analiz edilecek en üst düzey montajın soyutlaması (kullanıcının Inventor'da açtığı ana Assembly).
    /// </summary>
    public interface ICadAssemblyRoot
    {
        IReadOnlyList<ICadOccurrence> RootOccurrences { get; }
    }
}
