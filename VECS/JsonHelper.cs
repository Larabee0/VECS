using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VECS
{
    public class JsonHelper
    {
        public static readonly JsonSerializerOptions IncludeFields = new()
        {
            IncludeFields = true,
        };
    }
}
