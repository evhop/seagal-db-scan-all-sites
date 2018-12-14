using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WPDatabaseWork.Core
{
    public static class SerializerHelpers
    {
        public static string SerializeToString(object o)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new Serializer(stream);
                serializer.Serialize(o);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
