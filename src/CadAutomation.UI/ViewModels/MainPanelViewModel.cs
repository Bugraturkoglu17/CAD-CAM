using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CadAutomation.Core.Abstractions;
using CadAutomation.Core.Models;
using CadAutomation.Core.Services;
using CadAutomation.UI.Mvvm;

namespace CadAutomation.UI.ViewModels
{
    /// <summary>
    /// Ana panelin ViewModel'i - CAD'den tamamen bağımsız (sadece Core'daki soyutlamalara bağımlı),
    /// bu sayede aynı panel ileride SolidWorks/CATIA eklentilerinde de yeniden kullanılabilir.
    /// Gerçek montaj erişimi <see cref="IActiveAssemblyProvider"/> üzerinden CAD adaptöründen gelir.
    /// </summary>
    public sealed class MainPanelViewModel : ViewModelBase
    {
        private readonly IActiveAssemblyProvider _activeAssemblyProvider;
        private readonly IFlatPatternDxfExporter _exporter;
        private readonly UniquePartCollector _collector = new UniquePartCollector();
        private AssemblyAnalysisResult? _lastAnalysisResult;

        private string _outputFolder = string.Empty;
        private string _statusMessage = "Analiz için hazır.";
        private bool _hasAnalysisResult;
        private int _totalComponentCount;
        private int _uniquePartCount;
        private int _sheetMetalCount;
        private int _skippedCount;

        private bool _createFlatPattern = true;
        private bool _exportDxf = true;
        private bool _folderByThickness = true;
        private bool _isDwgFormat = true;
        private bool _createMarking = true;
        private bool _showBendLines = true;
        private bool _autoDimension;
        private bool _addPartLabel = true;
        private BendLineColorOption _selectedBendLineColor;

        public MainPanelViewModel(IActiveAssemblyProvider activeAssemblyProvider, IFlatPatternDxfExporter exporter)
        {
            _activeAssemblyProvider = activeAssemblyProvider ?? throw new ArgumentNullException(nameof(activeAssemblyProvider));
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));

            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            AnalyzeCommand = new RelayCommand(RunAnalysis);
            StartProcessCommand = new RelayCommand(StartProcess, _ => HasAnalysisResult);
            ShowLicenseInfoCommand = new RelayCommand(ShowLicenseInfo);

            _selectedBendLineColor = BendLineColorOptions[1]; // Kırmızı - lazer çıktısındaki büküm merkez çizgisi varsayılanı.
        }

        /// <summary>Ayarlar sekmesi - büküm çizgisi rengi seçenekleri. Kırmızı varsayılandır.</summary>
        public IReadOnlyList<BendLineColorOption> BendLineColorOptions { get; } = new[]
        {
            new BendLineColorOption("Renksiz", null),
            new BendLineColorOption("Kırmızı", 1),
            new BendLineColorOption("Sarı", 2),
            new BendLineColorOption("Yeşil", 3),
            new BendLineColorOption("Mavi", 5),
            new BendLineColorOption("Turuncu", 30),
        };

        public BendLineColorOption SelectedBendLineColor
        {
            get => _selectedBendLineColor;
            set => SetField(ref _selectedBendLineColor, value);
        }

        /// <summary>View, klasör seçim dialogunu ve mesaj kutularını buradan gösterir - ViewModel WPF/WinForms'a bağımlı kalmasın diye.</summary>
        public Func<string?>? RequestFolderPath { get; set; }
        public Action<string, string>? NotifyUser { get; set; }

        public string OutputFolder
        {
            get => _outputFolder;
            set => SetField(ref _outputFolder, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public bool HasAnalysisResult
        {
            get => _hasAnalysisResult;
            private set
            {
                if (SetField(ref _hasAnalysisResult, value))
                {
                    ((RelayCommand)StartProcessCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int TotalComponentCount { get => _totalComponentCount; private set => SetField(ref _totalComponentCount, value); }
        public int UniquePartCount { get => _uniquePartCount; private set => SetField(ref _uniquePartCount, value); }
        public int SheetMetalCount { get => _sheetMetalCount; private set => SetField(ref _sheetMetalCount, value); }
        public int SkippedCount { get => _skippedCount; private set => SetField(ref _skippedCount, value); }

        // Sheet Metal tespiti her zaman açık (Core işlevi) - kullanıcı kapatamaz, bilgi amaçlı gösteriliyor.
        public bool DetectSheetMetal => true;

        public bool CreateFlatPattern { get => _createFlatPattern; set => SetField(ref _createFlatPattern, value); }
        public bool ExportDxf { get => _exportDxf; set => SetField(ref _exportDxf, value); }
        public bool FolderByThickness { get => _folderByThickness; set => SetField(ref _folderByThickness, value); }

        /// <summary>
        /// DXF/DWG seçimi tek bir kaynak bool'a dayanıyor (2026-09-17: eskiden IsDxfFormat/
        /// IsDwgFormat birbirinden bağımsız iki property'ydi, her biri diğerini setter'ında elle
        /// false'a çekiyordu - bu, ekranda görünen seçili radio ile StartProcess'in gerçekte
        /// okuduğu değerin sapmasına yol açan gerçek bir bug'dı, canlı testte doğrulandı: ekranda
        /// "DXF" işaretliyken export hep DWG üretiyordu). Artık DXF radiosu XAML'de
        /// InverseBooleanConverter ile bu TEK property'nin tersini gösteriyor - iki farklı
        /// property'nin senkron kalması gerekmediği için bu sınıf bug yapısal olarak imkansız.
        /// </summary>
        public bool IsDwgFormat { get => _isDwgFormat; set => SetField(ref _isDwgFormat, value); }

        public bool CreateMarking { get => _createMarking; set => SetField(ref _createMarking, value); }
        public bool ShowBendLines { get => _showBendLines; set => SetField(ref _showBendLines, value); }

        /// <summary>Kullanıcı isteği: parça adı gerçek FlatPattern ağırlık merkezine markalansın.</summary>
        public bool AddPartLabel { get => _addPartLabel; set => SetField(ref _addPartLabel, value); }

        // PRO modüller - lisans mimarisi hazır olana kadar UI'da kilitli gösteriliyor (madde 10/14).
        public bool AutoDimension { get => _autoDimension; set => SetField(ref _autoDimension, value); }
        public bool BomExcelLocked => true;
        public bool ExportDrawingsLocked => true;
        public bool AutoDimensionLocked => true;

        public System.Windows.Input.ICommand BrowseFolderCommand { get; }
        public System.Windows.Input.ICommand AnalyzeCommand { get; }
        public System.Windows.Input.ICommand StartProcessCommand { get; }
        public System.Windows.Input.ICommand ShowLicenseInfoCommand { get; }

        private void BrowseFolder(object? parameter)
        {
            var selected = RequestFolderPath?.Invoke();
            if (!string.IsNullOrEmpty(selected))
            {
                OutputFolder = selected!;
            }
        }

        private void RunAnalysis(object? parameter)
        {
            try
            {
                var assembly = _activeAssemblyProvider.GetActiveAssembly();
                if (assembly == null)
                {
                    HasAnalysisResult = false;
                    _lastAnalysisResult = null;
                    StatusMessage = "Analiz için önce bir Assembly (.iam) dosyası açık olmalı.";
                    return;
                }

                var result = _collector.Collect(assembly);
                _lastAnalysisResult = result;

                TotalComponentCount = result.TotalComponentCount;
                UniquePartCount = result.UniquePartCount;
                SheetMetalCount = result.SheetMetalCount;
                SkippedCount = result.SkippedSuppressedCount;
                HasAnalysisResult = true;
                StatusMessage = $"Analiz tamamlandı - {result.SheetMetalCount} Sheet Metal parça bulundu " +
                    $"(toplam benzersiz parça: {result.UniqueParts.Count}, sheet metal olmayan: {result.UniqueParts.Count - result.SheetMetalCount}, atlanan/çözülemeyen: {result.SkippedSuppressedCount}).";
            }
            catch (Exception ex)
            {
                HasAnalysisResult = false;
                _lastAnalysisResult = null;
                StatusMessage = "Montaj analizi güvenli biçimde durduruldu.";
                NotifyUser?.Invoke("Analiz Hatası", "Montaj analizi tamamlanamadı: " + ex.Message);
            }
        }

        private void StartProcess(object? parameter)
        {
            if (_lastAnalysisResult == null)
            {
                StatusMessage = "Önce ANALİZ ET'e basın.";
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputFolder))
            {
                NotifyUser?.Invoke("Çıkış Klasörü Gerekli", "Devam etmeden önce bir çıkış klasörü seçin.");
                return;
            }

            var options = new BatchExportOptions
            {
                OutputFolder = OutputFolder,
                CreateFlatPattern = CreateFlatPattern,
                ExportDxf = ExportDxf,
                FolderByThickness = FolderByThickness,
                Format = IsDwgFormat ? ExportFormat.Dwg : ExportFormat.Dxf,
                CreateMarking = CreateMarking,
                ShowBendLines = ShowBendLines,
                AddPartLabel = AddPartLabel,
                LayerMapping = new LayerMappingOptions
                {
                    BendLineColorAci = SelectedBendLineColor.Aci,
                },
            };

            var batchResult = new BatchProcessor(_exporter).Process(_lastAnalysisResult, options);
            var formatLabel = options.Format == ExportFormat.Dwg ? "DWG" : "DXF";

            StatusMessage = $"İşlem tamamlandı - {formatLabel} oluşturulan: {batchResult.SuccessCount}, Atlanan: {batchResult.SkippedCount}, Hatalı: {batchResult.FailedCount}";

            var summary = new StringBuilder();
            summary.AppendLine($"İşlenen Sheet Metal: {batchResult.Results.Count}");
            summary.AppendLine($"{formatLabel} oluşturulan: {batchResult.SuccessCount}");
            summary.AppendLine($"Atlanan: {batchResult.SkippedCount}");
            summary.AppendLine($"Hatalı: {batchResult.FailedCount}");

            var failures = batchResult.Failures.ToList();
            if (failures.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Hatalar:");
                foreach (var failure in failures.Take(10))
                {
                    summary.AppendLine($"  {failure.PartIdentifier}: {failure.Message}");
                }
                if (failures.Count > 10)
                {
                    summary.AppendLine($"  ... ve {failures.Count - 10} hata daha.");
                }
            }

            NotifyUser?.Invoke("İşlem Sonucu", summary.ToString());
        }

        private void ShowLicenseInfo(object? parameter)
        {
            NotifyUser?.Invoke(
                "Lisans Gerekli",
                "Bu özellik mevcut lisansınıza dahil değildir. Lisansınızı yükseltmek için Ayarlar > Lisans bölümünü kullanın.");
        }
    }
}
