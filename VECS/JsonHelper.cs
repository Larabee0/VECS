using System.Text.Json;

namespace VECS
{
    public class JsonHelper
    {
        public static readonly JsonSerializerOptions IncludeFields = new()
        {
            IncludeFields = true
        };
    }
}
