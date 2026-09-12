using System;

namespace CadAutomation.Core.Models
{
    /// <summary>
    /// Bir parçanın batch işlem (Flat Pattern / DXF export vb.) sonucu.
    /// Bir parçadaki hata tüm batch'i durdurmaz; her parça için ayrı sonuç tutulur.
    /// </summary>
    public sealed class PartProcessResult
    {
        public PartProcessResult(string partIdentifier, PartProcessStatus status, string? message = null)
        {
            if (string.IsNullOrWhiteSpace(partIdentifier))
                throw new ArgumentException("Parça tanımlayıcısı boş olamaz.", nameof(partIdentifier));

            PartIdentifier = partIdentifier;
            Status = status;
            Message = message;
            Timestamp = DateTimeOffset.Now;
        }

        public string PartIdentifier { get; }
        public PartProcessStatus Status { get; }
        public string? Message { get; }
        public DateTimeOffset Timestamp { get; }
    }
}
