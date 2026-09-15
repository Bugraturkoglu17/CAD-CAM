using System;
using System.Runtime.InteropServices;
using CadAutomation.Inventor.Adapters;
using CadAutomation.Inventor.Export;
using CadAutomation.UI.Views;
using Inv = Inventor;

namespace CadAutomation.Inventor
{
    /// <summary>
    /// Inventor'ın COM tabanlı Add-In yükleyicisinin instantiate ettiği giriş noktası.
    /// GUID, .addin manifest dosyasındaki &lt;ClassId&gt; ile birebir eşleşmeli ve
    /// regasm ile COM'a kaydedilmiş olmalıdır (bkz. scripts/register-dev-addin.ps1).
    ///
    /// "Inventor" ad alanı yerine "Inv" takma adı kullanılıyor: bu proje de
    /// "CadAutomation.Inventor" ad alanında olduğu için düz "Inventor.X" veya "using Inventor;"
    /// hem kendi ad alanımızla hem de System.Windows.Forms (Application, vb.) ile çakışıyor (CS0104).
    /// </summary>
    [Guid("2BC2E2F1-AAA7-41DA-A720-617BC5372484")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("CadAutomation.Inventor.StandardAddInServer")]
    public class StandardAddInServer : Inv.ApplicationAddInServer
    {
        private const string AnalyzeButtonClientId = "{04080E79-655D-4DC4-912B-52CD9BD20887}";

        private Inv.Application? _inventorApplication;
        private Inv.ButtonDefinition? _analyzeButtonDefinition;
        private MainPanelWindow? _panelWindow;

        public void Activate(Inv.ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            _inventorApplication = addInSiteObject.Application;

            _analyzeButtonDefinition = _inventorApplication.CommandManager.ControlDefinitions.AddButtonDefinition(
                "Analyze Assembly",
                "CadAutomation:AnalyzeAssembly",
                Inv.CommandTypesEnum.kNonShapeEditCmdType,
                AnalyzeButtonClientId,
                "CAD Automation panelini açar; montaj analizi ve Sheet Metal/DXF işlemleri buradan yapılır.",
                "CAD Automation'ı Aç");

            _analyzeButtonDefinition.OnExecute += AnalyzeButtonDefinition_OnExecute;

            new RibbonBuilder().AddToAssemblyRibbon(_inventorApplication, _analyzeButtonDefinition);
        }

        public void Deactivate()
        {
            if (_panelWindow != null)
            {
                _panelWindow.Close();
                _panelWindow = null;
            }

            if (_analyzeButtonDefinition != null)
            {
                _analyzeButtonDefinition.OnExecute -= AnalyzeButtonDefinition_OnExecute;
                Marshal.ReleaseComObject(_analyzeButtonDefinition);
                _analyzeButtonDefinition = null;
            }

            if (_inventorApplication != null)
            {
                Marshal.ReleaseComObject(_inventorApplication);
                _inventorApplication = null;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int commandID)
        {
            // Eski (deprecated) komut arayüzü - ribbon buton event'leri ile çalışılacağı için kullanılmıyor.
        }

        public object? Automation => null;

        public void OnHide()
        {
        }

        public void OnShow()
        {
        }

        private void AnalyzeButtonDefinition_OnExecute(Inv.NameValueMap context)
        {
            try
            {
                ShowPanel();
            }
            catch (Exception ex)
            {
                // Inventor, COM event handler'lardaki exception'ları sessizce yutuyor - kullanıcıya
                // en azından bir şeylerin ters gittiğini bildiriyoruz (madde 21 - hata yönetimi).
                // TODO: Serilog eklenince burada tam stack trace log dosyasına yazılacak.
                System.Windows.Forms.MessageBox.Show(
                    "CAD Automation panel açılırken beklenmeyen bir hata oluştu:" + Environment.NewLine + ex.Message,
                    "CAD Automation - Hata",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void ShowPanel()
        {
            if (_inventorApplication == null) return;

            // Inventor gibi bir COM host'ta hiçbir zaman System.Windows.Application oluşturulmadığı için
            // WPF'in pack://application:,,, kaynak URI şeması başlatılmamış olur; ilk WPF penceresinden
            // önce bunu elle kurmak gerekiyor, yoksa ResourceDictionary/pack URI çözümlemesi sessizce patlar.
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            }

            if (_panelWindow == null)
            {
                var assemblyProvider = new InventorActiveAssemblyProvider(_inventorApplication);
                var exporter = new InventorFlatPatternDxfExporter();
                _panelWindow = new MainPanelWindow(assemblyProvider, exporter);
                _panelWindow.Closed += (sender, args) => _panelWindow = null;
            }

            if (_panelWindow.IsVisible)
            {
                _panelWindow.Activate();
            }
            else
            {
                // Modeless: Inventor'ı bloklamaz, kullanıcı panel açıkken montajla çalışmaya devam edebilir.
                _panelWindow.Show();
            }
        }
    }
}
