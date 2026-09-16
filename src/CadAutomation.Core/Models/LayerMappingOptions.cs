namespace CadAutomation.Core.Models
{
    /// <summary>
    /// DXF/DWG çıktısındaki layer isimlendirmesi ve rengi (madde 7-8). Farklı CNC/lazer makineleri
    /// farklı layer adlarını/renklerini bekleyebildiği için kullanıcı bunu değiştirebilmeli - şimdilik
    /// isim+renk değiştirilebilir, tam preset yönetimi (Durma/Bystronic/Trumpf) sonraki fazda.
    ///
    /// Renkler AutoCAD Color Index (ACI) ile tutuluyor (true-color/RGB yerine) - ACI, DXF/DWG
    /// standardının en eski ve en evrensel desteklenen renk sistemi, lazer/CNC yazılımlarının
    /// neredeyse tamamı ACI'yi tanır. Yaygın kodlar: 1=Kırmızı, 2=Sarı, 3=Yeşil, 5=Mavi, 7=Beyaz/Siyah.
    /// </summary>
    public sealed class LayerMappingOptions
    {
        public string OutlineLayer { get; set; } = "CUT";
        public int OutlineColorAci { get; set; } = 7;

        public string BendUpLayer { get; set; } = "BEND_UP";
        public string BendDownLayer { get; set; } = "BEND_DOWN";

        /// <summary>
        /// Büküm çizgisi rengi Ayarlar'dan değiştirilebilir. Lazer çıktısında merkez çizgisinin
        /// kesim konturundan açıkça ayrılması için varsayılan kırmızıdır (ACI 1).
        /// </summary>
        public int? BendLineColorAci { get; set; } = 1;

        /// <summary>
        /// Kullanıcı isteği (2026-09-15): büküm merkez çizgisi kesikli olmalı, düz çizgi
        /// olmamalı. "DASHED", DXF'in standart AutoCAD linetype isimlerinden biridir.
        /// </summary>
        public string BendLineType { get; set; } = "DASHED";

        /// <summary>
        /// Kullanıcı isteği (2026-09-15): kesikli desen Inventor çıktısında 1mm'de bir tekrarlıyor
        /// (birim dönüşümü yapılmamış inç bazlı bir tanımdan kalma) - normal parça boyutlarında bu
        /// kadar sık tekrar görsel/çıktıda düz çizgi gibi algılanıyor. $LTSCALE ile bu, deseni
        /// "gererek" gerçekten ayırt edilebilir kesikli-noktalı hale getiriyor.
        /// </summary>
        public double BendLineTypeScale { get; set; } = 10.0;

        public string MarkingLayer { get; set; } = "MARKING";
        public int MarkingColorAci { get; set; } = 2;
    }
}
