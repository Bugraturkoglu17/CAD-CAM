namespace CadAutomation.Core.Abstractions
{
    /// <summary>
    /// CAD platformundan bağımsız bir belge (parça veya montaj) soyutlaması.
    /// Inventor/SolidWorks/CATIA adaptörleri bu arayüzü kendi API tipleri üzerinden gerçekleştirir.
    /// </summary>
    public interface ICadDocument
    {
        /// <summary>Belgenin diskteki tam yolu. Unique part tespiti için anahtar olarak kullanılır.</summary>
        string FullFileName { get; }

        /// <summary>Kullanıcıya gösterilecek dosya adı (uzantısız).</summary>
        string DisplayName { get; }

        /// <summary>Bu belge bir montaj (assembly) belgesi mi, yoksa parça belgesi mi.</summary>
        bool IsAssemblyDocument { get; }

        /// <summary>Belge bir Sheet Metal parçası mı.</summary>
        bool IsSheetMetal { get; }

        /// <summary>Sheet Metal parçalar için kalınlık (Inventor iç birimi olan cm cinsinden). Sheet Metal değilse null.</summary>
        double? ThicknessCm { get; }

        /// <summary>Sheet Metal parçanın hâlihazırda bir Flat Pattern'i var mı.</summary>
        bool HasFlatPattern { get; }

        string? PartNumber { get; }
        string? Description { get; }
        string? Revision { get; }
    }
}
