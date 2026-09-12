using System;

namespace CadAutomation.Licensing
{
    /// <summary>
    /// İmzalanan/doğrulanan lisans içeriği (madde 9). Sunucu olmadığı için tüm doğrulama
    /// bu payload'ın imzası üzerinden, tamamen offline yapılır.
    /// </summary>
    public sealed class LicensePayload
    {
        public string Customer { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;

        /// <summary>Lisansın bağlı olduğu makine kimliği. Boş/"" ise makineye kilitli değildir (ör. deneme lisansı).</summary>
        public string MachineId { get; set; } = string.Empty;

        /// <summary>Son kullanma tarihi (UTC). null ise süresiz lisans.</summary>
        public DateTimeOffset? ExpirationUtc { get; set; }

        public LicenseModule EnabledModules { get; set; }

        /// <summary>
        /// Sunucusuz mimaride donanım değişimini tolere etmek için izin verilen maksimum aktivasyon sayısı.
        /// Aktivasyon takibi addin tarafında local olarak tutulur (bkz. madde 9 - server istemiyoruz kararının doğal sınırı).
        /// </summary>
        public int MaxActivations { get; set; } = 1;

        public bool HasModule(LicenseModule module) => (EnabledModules & module) == module;

        public bool IsExpired(DateTimeOffset now) => ExpirationUtc.HasValue && now > ExpirationUtc.Value;
    }
}
