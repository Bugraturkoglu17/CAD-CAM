using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CadAutomation.Core.Services
{
    /// <summary>
    /// Kullanıcı tanımlı dosya adı şablonlarını (madde 5 - naming template) çözümler.
    /// Örnek şablonlar: "{PartNumber}", "{PartNumber}_{Revision}", "{PartNumber}_{Description}_{Thickness}".
    /// Dosya sistemi için geçersiz karakterler otomatik olarak alt tireye çevrilir.
    /// </summary>
    public sealed class NamingTemplateEngine
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        public string BuildFileName(string template, IReadOnlyDictionary<string, string?> tokenValues, string extension)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Şablon boş olamaz.", nameof(template));
            if (tokenValues == null) throw new ArgumentNullException(nameof(tokenValues));
            if (string.IsNullOrWhiteSpace(extension))
                throw new ArgumentException("Uzantı boş olamaz.", nameof(extension));

            var result = new StringBuilder(template.Length);
            int i = 0;
            while (i < template.Length)
            {
                char c = template[i];
                if (c == '{')
                {
                    int close = template.IndexOf('}', i + 1);
                    if (close > i)
                    {
                        var token = template.Substring(i + 1, close - i - 1);
                        if (tokenValues.TryGetValue(token, out var value))
                        {
                            result.Append(Sanitize(value));
                            i = close + 1;
                            continue;
                        }
                    }
                }
                result.Append(c);
                i++;
            }

            var ext = extension[0] == '.' ? extension : "." + extension;
            return result.ToString() + ext;
        }

        private static string Sanitize(string? value)
        {
            if (value == null || value.Length == 0) return string.Empty;

            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(InvalidFileNameChars, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }
}
