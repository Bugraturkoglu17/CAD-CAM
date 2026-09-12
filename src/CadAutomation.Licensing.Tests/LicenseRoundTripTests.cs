using System;
using System.Security.Cryptography;
using CadAutomation.Licensing;
using CadAutomation.Licensing.Generator;
using Xunit;

namespace CadAutomation.Licensing.Tests
{
    public class LicenseRoundTripTests
    {
        private static (RSA PrivateKeyHolder, RSA PublicKeyHolder) CreateTestKeyPair()
        {
            using var rsa = RSA.Create(2048);
            var privateKeyHolder = RSA.Create();
            privateKeyHolder.ImportRSAPrivateKey(rsa.ExportRSAPrivateKey(), out _);

            var publicKeyHolder = RSA.Create();
            publicKeyHolder.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out _);

            return (privateKeyHolder, publicKeyHolder);
        }

        private static LicensePayload CreateSamplePayload() => new LicensePayload
        {
            Customer = "Ahmet Yılmaz",
            Company = "Örnek Metal A.Ş.",
            MachineId = "MID-ABC123",
            ExpirationUtc = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EnabledModules = LicenseModule.Core | LicenseModule.DxfExport | LicenseModule.Marking,
            MaxActivations = 2,
        };

        [Fact]
        public void Gecerli_imza_dogrulamadan_gecer_ve_payload_geri_donusur()
        {
            var (privateKey, publicKey) = CreateTestKeyPair();
            using (privateKey) using (publicKey)
            {
                var signer = new RsaLicenseSigner(privateKey);
                var licenseText = signer.Sign(CreateSamplePayload());

                var verifier = new RsaLicenseVerifier(publicKey);
                var result = verifier.Verify(licenseText);

                Assert.True(result.IsValid);
                Assert.NotNull(result.Payload);
                Assert.Equal("Ahmet Yılmaz", result.Payload!.Customer);
                Assert.Equal("Örnek Metal A.Ş.", result.Payload.Company);
                Assert.True(result.Payload.HasModule(LicenseModule.DxfExport));
                Assert.False(result.Payload.HasModule(LicenseModule.BomExcel));
                Assert.Equal(2, result.Payload.MaxActivations);
            }
        }

        [Fact]
        public void Yanlis_public_key_ile_dogrulama_basarisiz_olur()
        {
            var (privateKey, _) = CreateTestKeyPair();
            var (_, wrongPublicKey) = CreateTestKeyPair();
            using (privateKey) using (wrongPublicKey)
            {
                var signer = new RsaLicenseSigner(privateKey);
                var licenseText = signer.Sign(CreateSamplePayload());

                var verifier = new RsaLicenseVerifier(wrongPublicKey);
                var result = verifier.Verify(licenseText);

                Assert.False(result.IsValid);
                Assert.Null(result.Payload);
            }
        }

        [Fact]
        public void Kurcalanmis_payload_imza_dogrulamasini_gecemez()
        {
            var (privateKey, publicKey) = CreateTestKeyPair();
            using (privateKey) using (publicKey)
            {
                var signer = new RsaLicenseSigner(privateKey);
                var licenseText = signer.Sign(CreateSamplePayload());

                // Kullanıcı lisans metnindeki payload kısmını değiştirip modül eklemeye çalışırsa
                var parts = licenseText.Split('.');
                var tampered = parts[0] + "AAAA" + "." + parts[1];

                var verifier = new RsaLicenseVerifier(publicKey);
                var result = verifier.Verify(tampered);

                Assert.False(result.IsValid);
            }
        }

        [Fact]
        public void Suresi_dolmus_lisans_IsExpired_true_doner()
        {
            var payload = CreateSamplePayload();
            var afterExpiration = new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero);

            Assert.True(payload.IsExpired(afterExpiration));
            Assert.False(payload.IsExpired(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Suresiz_lisans_hicbir_zaman_expire_olmaz()
        {
            var payload = CreateSamplePayload();
            payload.ExpirationUtc = null;

            Assert.False(payload.IsExpired(DateTimeOffset.MaxValue));
        }

        [Fact]
        public void Pipe_ve_backslash_iceren_firma_adi_dogru_sekilde_yuvarlanir()
        {
            var (privateKey, publicKey) = CreateTestKeyPair();
            using (privateKey) using (publicKey)
            {
                var payload = CreateSamplePayload();
                payload.Company = "A|B\\C Metal";

                var signer = new RsaLicenseSigner(privateKey);
                var licenseText = signer.Sign(payload);

                var verifier = new RsaLicenseVerifier(publicKey);
                var result = verifier.Verify(licenseText);

                Assert.True(result.IsValid);
                Assert.Equal("A|B\\C Metal", result.Payload!.Company);
            }
        }
    }
}
