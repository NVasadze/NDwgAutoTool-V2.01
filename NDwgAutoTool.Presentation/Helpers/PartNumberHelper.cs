using System.Text.RegularExpressions;

namespace NDwgAutoTool.Helpers
{
    public static class PartNumberHelper
    {
        private static readonly Regex SelfPartPattern =
            new Regex(@"^[A-Z]{4}\d{6}[A-Z]\d{4}$");

        public static bool IsSelfPart(string partNumber)
        {
            return SelfPartPattern.IsMatch(partNumber);
        }
    }
}