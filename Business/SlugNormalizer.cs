using System.Text.RegularExpressions;

namespace miniDriveBackend.Business
{
    public static class SlugNormalizer
    {
        private static readonly Regex ValidSlug = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

        public static string Normalize(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

        public static bool IsValid(string slug) =>
            !string.IsNullOrWhiteSpace(slug) && slug.Length <= 100 && ValidSlug.IsMatch(slug);
    }
}
