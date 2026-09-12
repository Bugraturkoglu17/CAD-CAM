using System;
using System.Text;

namespace CadAutomation.Licensing
{
    /// <summary>
    /// Dağıtılabilir lisans dosyası/key metnini kodlar/çözer: "base64url(canonical).base64url(imza)".
    /// Kullanıcının LİSANS AKTİVE ET ekranına yapıştıracağı metin bu formattadır.
    /// </summary>
    public static class LicenseFile
    {
        public static string Create(LicensePayload payload, byte[] signature)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (signature == null || signature.Length == 0) throw new ArgumentException("İmza boş olamaz.", nameof(signature));

            var canonical = LicenseCanonicalSerializer.Serialize(payload);
            var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(canonical));
            var signaturePart = Base64UrlEncode(signature);
            return payloadPart + "." + signaturePart;
        }

        public static (string CanonicalPayload, byte[] Signature) Parse(string licenseText)
        {
            if (string.IsNullOrWhiteSpace(licenseText))
                throw new FormatException("Lisans metni boş.");

            var parts = licenseText.Trim().Split('.');
            if (parts.Length != 2)
                throw new FormatException("Lisans dosyası formatı tanınamadı.");

            var canonical = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var signature = Base64UrlDecode(parts[1]);
            return (canonical, signature);
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static byte[] Base64UrlDecode(string text)
        {
            var padded = text.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
                case 1: throw new FormatException("Geçersiz base64url uzunluğu.");
            }
            return Convert.FromBase64String(padded);
        }
    }
}
