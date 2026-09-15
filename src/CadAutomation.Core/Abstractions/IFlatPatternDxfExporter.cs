using CadAutomation.Core.Models;

namespace CadAutomation.Core.Abstractions
{
    /// <summary>
    /// Tek bir Sheet Metal parça için Flat Pattern + DXF export işlemini CAD'den bağımsız şekilde
    /// tetikler. Gerçek iş her CAD adaptöründe (Inventor/SolidWorks/CATIA) ayrı ayrı yapılır.
    /// Bir parçadaki hata (madde 21) burada exception fırlatmak yerine PartProcessResult.Failed
    /// olarak döner - BatchProcessor tüm batch'i durdurmadan devam edebilsin diye.
    /// </summary>
    public interface IFlatPatternDxfExporter
    {
        PartProcessResult ExportPart(UniquePart part, BatchExportOptions options);
    }
}
