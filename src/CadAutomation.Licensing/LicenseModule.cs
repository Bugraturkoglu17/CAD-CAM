using System;

namespace CadAutomation.Licensing
{
    /// <summary>
    /// Lisanslanabilir yetenekler (madde 14/16 - Basic/Professional/Enterprise ayrımı).
    /// Bit bayrağı olduğu için tek bir int ile serbestçe kombine edilip lisans payload'ında taşınabilir;
    /// paketleme (bundle) değişse bile bu enum'un kendisi değişmeden kalır.
    /// </summary>
    [Flags]
    public enum LicenseModule
    {
        None = 0,
        Core = 1 << 0,
        DxfExport = 1 << 1,
        Marking = 1 << 2,
        BendLayers = 1 << 3,
        BomExcel = 1 << 4,
        AutoDimension = 1 << 5,
        Reporting = 1 << 6,
    }
}
