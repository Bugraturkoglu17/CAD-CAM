using System;
using CadAutomation.Core.Abstractions;
using Inv = Inventor;

namespace CadAutomation.Inventor.Adapters
{
    /// <summary>
    /// CadAutomation.Core'daki ICadDocument soyutlamasının gerçek Inventor Document üzerinden
    /// implementasyonu. Sheet Metal tespiti, "as SheetMetalComponentDefinition" cast'inin (COM
    /// QueryInterface) başarılı olup olmamasına dayanır - Inventor addin geliştirmede standart yöntem.
    /// </summary>
    public sealed class InventorCadDocument : ICadDocument
    {
        private readonly Inv.Document _document;
        private readonly Inv.SheetMetalComponentDefinition? _sheetMetalDefinition;

        public InventorCadDocument(Inv.Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            if (document is Inv.PartDocument partDocument)
            {
                _sheetMetalDefinition = partDocument.ComponentDefinition as Inv.SheetMetalComponentDefinition;
            }
        }

        /// <summary>
        /// Gerçek Inventor COM nesnesi - sadece Inventor projesindeki diğer sınıflar (ör. DXF exporter)
        /// için, Core'un CAD-bağımsız kalması için ICadDocument arayüzünde YOKTUR.
        /// </summary>
        public Inv.Document NativeDocument => _document;

        /// <summary>Sheet Metal değilse null - exporter'ın tekrar cast yapmasına gerek kalmasın diye.</summary>
        public Inv.SheetMetalComponentDefinition? NativeSheetMetalDefinition => _sheetMetalDefinition;

        public string FullFileName => _document.FullFileName;

        public string DisplayName => _document.DisplayName;

        public bool IsAssemblyDocument => _document.DocumentType == Inv.DocumentTypeEnum.kAssemblyDocumentObject;

        public bool IsSheetMetal => _sheetMetalDefinition != null;

        public double? ThicknessCm
        {
            get
            {
                if (_sheetMetalDefinition == null) return null;
                try { return (double)_sheetMetalDefinition.Thickness.Value; }
                catch (Exception) { return null; }
            }
        }

        public bool HasFlatPattern
        {
            get
            {
                if (_sheetMetalDefinition == null) return false;
                try { return _sheetMetalDefinition.HasFlatPattern; }
                catch (Exception) { return false; }
            }
        }

        public string? PartNumber => GetDesignTrackingProperty("Part Number");

        public string? Description => GetDesignTrackingProperty("Description");

        public string? Revision => GetDesignTrackingProperty("Revision Number");

        private string? GetDesignTrackingProperty(string propertyName)
        {
            try
            {
                var propertySet = _document.PropertySets["Design Tracking Properties"];
                return propertySet[propertyName].Value?.ToString();
            }
            catch (Exception)
            {
                // iProperty bulunamadı/okunamadı - Core tarafında bu alanlar zaten nullable,
                // batch işlemi durdurmak yerine null döndürüp devam ediyoruz (madde 21).
                return null;
            }
        }
    }
}
