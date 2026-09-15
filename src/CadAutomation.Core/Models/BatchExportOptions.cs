namespace CadAutomation.Core.Models
{
    /// <summary>Panel üzerindeki checkbox/çıkış klasörü seçimlerinin CAD'den bağımsız karşılığı.</summary>
    public sealed class BatchExportOptions
    {
        public string OutputFolder { get; set; } = string.Empty;
        public bool CreateFlatPattern { get; set; } = true;
        public bool ExportDxf { get; set; } = true;
        public bool FolderByThickness { get; set; } = true;

        /// <summary>Kullanıcının panelde DXF/DWG arasında seçtiği çıktı formatı.</summary>
        public ExportFormat Format { get; set; } = ExportFormat.Dxf;

        public bool CreateMarking { get; set; }
        public bool ShowBendLines { get; set; } = true;
        public LayerMappingOptions LayerMapping { get; set; } = new LayerMappingOptions();

        /// <summary>
        /// Kullanıcı isteği (2026-09-15): her parçanın flat pattern'i üzerine parça adı/numarası
        /// yazılsın, hiçbir çizgiyle (kesim/büküm) kesişmesin - kesişirse lazer hatalı keser.
        /// </summary>
        public bool AddPartLabel { get; set; } = true;

        /// <summary>NamingTemplateEngine'in anladığı token'lar: {PartNumber} {Description} {Revision} {Thickness}.</summary>
        public string NamingTemplate { get; set; } = "{PartNumber}";
    }
}
