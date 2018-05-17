using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Migration.Analys
{
    public class ImageAnalys : IEquatable<ImageAnalys>
    {
        public string SiteAzureContainer { get; set; }
        public ulong Id { get; set; }
        public string TableSource { get; set; }
        public string ImageSource { get; set; }
        public bool? Succeded { get; set; }

        public override int GetHashCode() => ImageSource?.GetHashCode() ?? 0;

        public bool Equals(ImageAnalys other)
        {
            //Check whether the compared object is null. 
            if (Object.ReferenceEquals(other, null)) return false;

            //Check whether the compared object references the same data. 
            if (Object.ReferenceEquals(this, other)) return true;

            //Check whether the imageAnalys' properties are equal. 
            return ImageSource.Equals(other.ImageSource);
        }

    }
}
