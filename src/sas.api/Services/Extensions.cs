using System.Linq;

namespace sas.api.Services
{
    internal class Extensions
    {
        public static bool AnyNull(params object[] args)
        {
            return args.Any(x => x == null);
        }
    }

    public class Result
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
