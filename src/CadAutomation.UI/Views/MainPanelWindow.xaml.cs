using System.Windows;
using System.Windows.Forms;
using CadAutomation.Core.Abstractions;
using CadAutomation.UI.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace CadAutomation.UI.Views
{
    /// <summary>
    /// Ana panel - Inventor'ı bloklamaması için modeless (Show, ShowDialog değil) açılmalı.
    /// ViewModel'in WPF'e bağımlı kalmaması için klasör seçimi ve mesaj kutuları burada,
    /// code-behind'da, delegate olarak bağlanıyor (bkz. MainPanelViewModel.RequestFolderPath/NotifyUser).
    /// </summary>
    public partial class MainPanelWindow : Window
    {
        public MainPanelWindow(IActiveAssemblyProvider activeAssemblyProvider, IFlatPatternDxfExporter exporter)
        {
            InitializeComponent();

            var viewModel = new MainPanelViewModel(activeAssemblyProvider, exporter)
            {
                RequestFolderPath = ShowFolderBrowser,
                NotifyUser = (title, message) => MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information)
            };

            DataContext = viewModel;
        }

        private string? ShowFolderBrowser()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "DXF/Flat Pattern çıktılarının kaydedileceği klasörü seçin"
            };

            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
        }
    }
}
