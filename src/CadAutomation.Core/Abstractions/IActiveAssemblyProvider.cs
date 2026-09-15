namespace CadAutomation.Core.Abstractions
{
    /// <summary>
    /// UI katmanının, kullanıcının o an CAD uygulamasında hangi montajı açık tuttuğunu
    /// CAD'e özel API'lere hiç dokunmadan sorabilmesini sağlar. Her CAD adaptörü
    /// (Inventor/SolidWorks/CATIA) bunu kendi "aktif belge" mantığıyla gerçekleştirir.
    /// </summary>
    public interface IActiveAssemblyProvider
    {
        /// <summary>Aktif belge bir montaj değilse (veya hiç belge açık değilse) null döner.</summary>
        ICadAssemblyRoot? GetActiveAssembly();
    }
}
