using System;

namespace CadAutomation.Core.Models
{
    /// <summary>
    /// CAD çıktısına eklenecek, orta noktası belirtilmiş bir markalama metni.
    /// Tüm koordinatlar ve metin yüksekliği çıktı birimi olan milimetredir.
    /// </summary>
    public sealed class CadTextAnnotation
    {
        public CadTextAnnotation(
            string text,
            double xMillimeters,
            double yMillimeters,
            double heightMillimeters,
            double rotationRadians,
            string layerName,
            int layerColorAci)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Metin boş olamaz.", nameof(text));
            if (heightMillimeters <= 0) throw new ArgumentOutOfRangeException(nameof(heightMillimeters));
            if (string.IsNullOrWhiteSpace(layerName)) throw new ArgumentException("Katman adı boş olamaz.", nameof(layerName));

            Text = text;
            XMillimeters = xMillimeters;
            YMillimeters = yMillimeters;
            HeightMillimeters = heightMillimeters;
            RotationRadians = rotationRadians;
            LayerName = layerName;
            LayerColorAci = layerColorAci;
        }

        public string Text { get; }
        public double XMillimeters { get; }
        public double YMillimeters { get; }
        public double HeightMillimeters { get; }
        public double RotationRadians { get; }
        public string LayerName { get; }
        public int LayerColorAci { get; }
    }
}
