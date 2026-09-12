namespace CadAutomation.Licensing
{
    public sealed class LicenseVerificationResult
    {
        private LicenseVerificationResult(bool isValid, LicensePayload? payload, string? error)
        {
            IsValid = isValid;
            Payload = payload;
            Error = error;
        }

        public bool IsValid { get; }
        public LicensePayload? Payload { get; }
        public string? Error { get; }

        public static LicenseVerificationResult Success(LicensePayload payload) =>
            new LicenseVerificationResult(true, payload, null);

        public static LicenseVerificationResult Failure(string error) =>
            new LicenseVerificationResult(false, null, error);
    }
}
