using Seagal_TransformHttpContentToHttps.View;
using Seagal_TransformHttpContentToHttps.WPClient.Model;
using System.Text.RegularExpressions;

namespace Seagal_TransformHttpContentToHttps.Model
{
    public interface ISite: ILogTransformer
    {
        Regex HttpRegex { get; }
        ITableNameGenerator Generator { get; }
        Settings Settings { get; }
        string Path { get; }
        long Index { get; }
        string GetUrlPath(string url, string urlNew);
        bool RewriteUrl(string url);
    }
}
