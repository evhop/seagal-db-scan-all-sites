using System;

namespace Fallback_blogg.Analys
{
    public class HttpLink : IEquatable<HttpLink>
    {
        public string SchemaTable { get; set; }
        public ulong Id { get; set; }
        public string HttpSource { get; set; }
        public bool? Succeded { get; set; }

        public override int GetHashCode() => HttpSource?.GetHashCode() ?? 0;

        public bool Equals(HttpLink other)
        {
            //Check whether the compared object is null. 
            if (Object.ReferenceEquals(other, null)) return false;

            //Check whether the compared object references the same data. 
            if (Object.ReferenceEquals(this, other)) return true;

            //Check whether the imageAnalys' properties are equal. 
            return HttpSource.Equals(other.HttpSource);
        }

    }
}
