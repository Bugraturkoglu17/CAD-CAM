namespace CadAutomation.UI.ViewModels
{
    /// <summary>Ayarlar sekmesindeki büküm çizgisi rengi seçim listesi için görüntü modeli.</summary>
    public sealed class BendLineColorOption
    {
        public BendLineColorOption(string name, int? aci)
        {
            Name = name;
            Aci = aci;
        }

        public string Name { get; }

        /// <summary>null = "Renksiz" (post-process rengi hiç değiştirmez, Inventor varsayılanı kalır).</summary>
        public int? Aci { get; }

        public override string ToString() => Name;
    }
}
