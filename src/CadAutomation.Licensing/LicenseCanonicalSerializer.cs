using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CadAutomation.Licensing
{
    /// <summary>
    /// LicensePayload'ı imzalanacak/doğrulanacak tek, deterministik bir string'e çevirir.
    /// Harici bir JSON kütüphanesine bağımlı değildir (netstandard2.0'da ekstra paket gerektirmeden
    /// çalışması ve imza içeriğinin serialization kütüphanesi davranışına bağlı kalmaması için).
    /// Format: "LIC1|customer|company|machineId|expiration|modules|maxActivations" (alanlar pipe ile
    /// ayrılır, serbest metin alanlarındaki "\" ve "|" karakterleri kaçış (escape) ile korunur).
    /// </summary>
    public static class LicenseCanonicalSerializer
    {
        private const string SchemaVersion = "LIC1";
        private const int FieldCount = 7;

        public static string Serialize(LicensePayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var fields = new[]
            {
                SchemaVersion,
                Escape(payload.Customer),
                Escape(payload.Company),
                Escape(payload.MachineId),
                payload.ExpirationUtc.HasValue
                    ? payload.ExpirationUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                    : "NONE",
                ((int)payload.EnabledModules).ToString(CultureInfo.InvariantCulture),
                payload.MaxActivations.ToString(CultureInfo.InvariantCulture),
            };

            return string.Join("|", fields);
        }

        public static LicensePayload Deserialize(string canonical)
        {
            if (string.IsNullOrEmpty(canonical))
                throw new FormatException("Lisans içeriği boş.");

            var fields = SplitEscaped(canonical);
            if (fields.Count != FieldCount || fields[0] != SchemaVersion)
                throw new FormatException("Lisans formatı tanınamadı (beklenmeyen şema).");

            return new LicensePayload
            {
                Customer = fields[1],
                Company = fields[2],
                MachineId = fields[3],
                ExpirationUtc = fields[4] == "NONE"
                    ? (DateTimeOffset?)null
                    : DateTimeOffset.Parse(fields[4], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                EnabledModules = (LicenseModule)int.Parse(fields[5], CultureInfo.InvariantCulture),
                MaxActivations = int.Parse(fields[6], CultureInfo.InvariantCulture),
            };
        }

        private static string Escape(string? value)
        {
            if (value == null || value.Length == 0) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("|", "\\|");
        }

        private static List<string> SplitEscaped(string input)
        {
            var fields = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '\\' && i + 1 < input.Length && (input[i + 1] == '|' || input[i + 1] == '\\'))
                {
                    current.Append(input[i + 1]);
                    i++;
                }
                else if (c == '|')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString());

            return fields;
        }
    }
}
