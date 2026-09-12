namespace CadAutomation.Core.Models
{
    /// <summary>Batch işlem sırasında bir parçanın işlem durumu.</summary>
    public enum PartProcessStatus
    {
        Pending,
        Success,
        Warning,
        Failed,
        Skipped
    }
}
