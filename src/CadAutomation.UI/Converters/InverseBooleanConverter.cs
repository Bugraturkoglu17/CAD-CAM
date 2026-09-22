using System;
using System.Globalization;
using System.Windows.Data;

namespace CadAutomation.UI.Converters
{
    /// <summary>
    /// İki karşılıklı dışlayıcı RadioButton'ı TEK bir bool kaynağa bağlamak için (2026-09-17
    /// kullanıcı testinde bulunan bug'ın kök nedenine çözüm): DXF/DWG RadioButton'ları önceden
    /// birbirinden BAĞIMSIZ iki ayrı bool property'ye (IsDxfFormat/IsDwgFormat) bağlıydı, her biri
    /// kendi setter'ında diğerini elle false'a çekiyordu. Bu WPF host ortamında (Inventor COM
    /// process'i içinde barındırılan pencere) ekranda gösterilen işaretli radio ile gerçekte
    /// StartProcess'in okuduğu değer birbirinden SAPABİLİYORDU (ekranda "DXF" işaretliyken export
    /// hep DWG üretiyordu - canlı testte doğrulandı). TEK bir bool'a (IsDwgFormat) TwoWay bağlanıp
    /// DXF radiosunun bu converter ile tersini göstermesi, görünen ve gerçek durumun ASLA
    /// birbirinden sapamayacağı yapısal olarak garanti ediyor.
    /// </summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public static readonly InverseBooleanConverter Instance = new InverseBooleanConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }
    }
}
