namespace CadAutomation.Licensing
{
    public interface ILicenseVerifier
    {
        LicenseVerificationResult Verify(string licenseText);
    }
}
