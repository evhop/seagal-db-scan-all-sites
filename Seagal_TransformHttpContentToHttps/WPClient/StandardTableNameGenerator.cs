using Seagal_TransformHttpContentToHttps.WPClient.Model;

namespace Seagal_TransformHttpContentToHttps.WPClient
{
	public class StandardTableNameGenerator : ITableNameGenerator
	{
		public string TablePrefix { get; }

        public StandardTableNameGenerator( string tablePrefix ) => TablePrefix = tablePrefix;

        public string GetName( string table ) => $"{TablePrefix}_{table}";

        public string TransformUrlToFile( string url )
		{
			var transformed = url.Replace( "/wp-content/uploads/", "/" );

			if( transformed.StartsWith( "//" ) || !transformed.StartsWith( "/" ) )
			{
				var idx = transformed.IndexOf( "//" );
				if( idx >= 0 )
				{
					idx = transformed.IndexOf( "/", idx + 2 );
					transformed = transformed.Substring( idx );
				}

				if( !transformed.StartsWith( "/" ) )
				{
					transformed = "/" + url;
				}
			}

			return transformed;
		}
	}
}