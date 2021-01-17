using System;

namespace ckad_requester.Configuration
{
    public class WebsiteConfiguration
    {
        protected bool Equals(WebsiteConfiguration other)
        {
            return Url == other.Url && NumberOfThreads == other.NumberOfThreads;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((WebsiteConfiguration) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Url, NumberOfThreads);
        }

        public static bool operator ==(WebsiteConfiguration left, WebsiteConfiguration right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(WebsiteConfiguration left, WebsiteConfiguration right)
        {
            return !Equals(left, right);
        }

        public string Url { get; set; }
        public int NumberOfThreads { get; set; }
    }
}