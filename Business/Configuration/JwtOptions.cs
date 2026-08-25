namespace miniDriveBackend.Business.Configuration
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "mini-drive";
        public string Audience { get; set; } = "mini-drive-clients";
        public int AccessTokenExpiryMinutes { get; set; } = 15;
        public int RefreshTokenExpiryDays { get; set; } = 7;
    }
}
