using CadAutomation.Core.Models;

namespace CadAutomation.Core.Abstractions
{
    /// <summary>
    /// Bütün parçalar export edildikten sonra tek kez çalışması gereken toplu son işlemler.
    /// </summary>
    public interface IBatchExportFinalizer
    {
        PartProcessResult? CompleteBatch(BatchExportOptions options);
    }
}
