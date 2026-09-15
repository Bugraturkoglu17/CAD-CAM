using System;
using Inv = Inventor;

namespace CadAutomation.Inventor
{
    /// <summary>
    /// "CAD AUTOMATION" ribbon sekmesini kurar (madde 18). Inventor API'sinde bir koleksiyonda
    /// öğenin var olup olmadığını kontrol eden doğrudan bir "TryGet" yok; var olmayan bir anahtarla
    /// indekslemek exception fırlatıyor - bu yüzden var/yok kontrolü try/catch ile yapılıyor
    /// (Inventor addin geliştirmede yaygın kullanılan bir pratik).
    /// </summary>
    internal sealed class RibbonBuilder
    {
        private const string ClientId = "{04080E79-655D-4DC4-912B-52CD9BD20887}";
        private const string TabInternalName = "CadAutomation:MainTab";
        private const string TabDisplayName = "CAD Automation";
        private const string PanelInternalName = "CadAutomation:ProjectPanel";
        private const string PanelDisplayName = "Project";

        /// <summary>
        /// Şimdilik sadece Assembly ortamına ekleniyor çünkü Faz 2 kapsamı montaj analiziyle sınırlı.
        /// Part/Drawing ortamları ileriki fazlarda (flat pattern/DXF) ayrıca eklenecek.
        /// </summary>
        public void AddToAssemblyRibbon(Inv.Application application, Inv.ButtonDefinition buttonDefinition)
        {
            var ribbon = application.UserInterfaceManager.Ribbons["Assembly"];
            var tab = GetOrAddTab(ribbon);
            var panel = GetOrAddPanel(tab);
            panel.CommandControls.AddButton(buttonDefinition, true);
        }

        private static Inv.RibbonTab GetOrAddTab(Inv.Ribbon ribbon)
        {
            try
            {
                return ribbon.RibbonTabs[TabInternalName];
            }
            catch (Exception)
            {
                return ribbon.RibbonTabs.Add(TabDisplayName, TabInternalName, ClientId);
            }
        }

        private static Inv.RibbonPanel GetOrAddPanel(Inv.RibbonTab tab)
        {
            try
            {
                return tab.RibbonPanels[PanelInternalName];
            }
            catch (Exception)
            {
                return tab.RibbonPanels.Add(PanelDisplayName, PanelInternalName, ClientId);
            }
        }
    }
}
