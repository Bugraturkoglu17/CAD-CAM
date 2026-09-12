using System;
using System.Security.Cryptography;
using System.Text;

namespace CadAutomation.Licensing
{
    /// <summary>
    /// Eklenti içinde dağıtılan doğrulayıcı — sadece PUBLIC KEY taşır, imza üretemez.
    /// Private key kesinlikle burada bulunmaz; sadece CadAutomation.Licensing.Generator
    /// (repo'da tools/ altında, git'e hiç girmez) private key'i tutar.
    /// </summary>
    public sealed class RsaLicenseVerifier : ILicenseVerifier
    {
        private readonly RSA _publicKey;

        public RsaLicenseVerifier(RSA publicKey)
        {
            _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        }

        public LicenseVerificationResult Verify(string licenseText)
        {
            if (string.IsNullOrWhiteSpace(licenseText))
                return LicenseVerificationResult.Failure("Lisans metni boş.");

            string canonical;
            byte[] signature;
            try
            {
                var parsed = LicenseFile.Parse(licenseText);
                canonical = parsed.CanonicalPayload;
                signature = parsed.Signature;
            }
            catch (Exception ex)
            {
                return LicenseVerificationResult.Failure("Lisans dosyası okunamadı: " + ex.Message);
            }

            bool signatureValid;
            try
            {
                var data = Encoding.UTF8.GetBytes(canonical);
                signatureValid = _publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (CryptographicException)
            {
                signatureValid = false;
            }

            if (!signatureValid)
                return LicenseVerificationResult.Failure("Lisans imzası geçersiz veya dosya bozulmuş.");

            try
            {
                var payload = LicenseCanonicalSerializer.Deserialize(canonical);
                return LicenseVerificationResult.Success(payload);
            }
            catch (Exception ex)
            {
                return LicenseVerificationResult.Failure("Lisans içeriği ayrıştırılamadı: " + ex.Message);
            }
        }
    }
}
