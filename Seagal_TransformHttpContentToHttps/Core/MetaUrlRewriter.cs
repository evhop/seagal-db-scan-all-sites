using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Fallback_blogg.Core
{
    public class MetaUrlRewriter
    {
        private static Regex UrlHttpRegex = new Regex($"src=[\"']((http://[^/]+)?(/(?!/).*))[\"']", RegexOptions.Compiled);

        public MetaUrlRewriter()
        {
        }

        public object RewriteUrl(object obj, int urlGroup)
        {
            return urlrewriter(obj, urlGroup);
        }

        private object urlrewriter(object obj, int urlGroup)
        {
            if (obj == null) return obj;
            if (obj is string)
            {
                obj = ReplaceUrls((string)obj, urlGroup);
                return obj;
            }
            if (obj is bool) return obj;
            if (obj is int) return obj;
            if (obj is double) return obj;
            if (obj is List<object>)
            {
                var modObjList = new List<object>();
                var a = (List<object>)obj;
                for (int i = 0; i < a.Count; i++)
                {
                    modObjList.Add(urlrewriter(a[i], urlGroup));
                }
                return modObjList;
            }
            if (obj is Dictionary<object, object>)
            {
                var modObjDic = new Dictionary<object, object>();
                var a = (Dictionary<object, object>)obj;
                foreach (var entry in a)
                {
                    modObjDic.Add(entry.Key, urlrewriter(entry.Value, urlGroup));
                }
                return modObjDic;
            }
            return obj;
        }

        private string ReplaceUrls(string text, int urlGroup)
        {
            if (!String.IsNullOrEmpty(text))
            {
                var replacedText = UrlHttpRegex.Replace(text, match => RewriteSource(match, urlGroup));
                if (!string.Equals(text, replacedText))
                {
                    text = replacedText;
                }
            }
            return text;
        }

        private string RewriteSource(Match match, int urlGroup)
        {
            //TODO kolla om källan finns som https. Just nu görs den om till en relativ url  //
            var url = match.Groups[0].Value.Replace("http:", "");
            return url;
        }
    }
}
