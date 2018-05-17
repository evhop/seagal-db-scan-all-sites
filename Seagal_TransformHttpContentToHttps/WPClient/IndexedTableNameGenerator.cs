using Seagal_TransformHttpContentToHttps.WPClient.Model;

namespace Seagal_TransformHttpContentToHttps.WPClient
{
    public class IndexedTableNameGenerator : ITableNameGenerator
	{
		public string TablePrefix { get; }
		public long Index { get; }
		public string Path { get; }

		public IndexedTableNameGenerator( string tablePrefix, long index, string path )
		{
			TablePrefix = tablePrefix;
			Index = index;
			Path = path;
		}

        public string GetName( string table ) => $"{TablePrefix}_{Index}_{table}";

        public string TransformUrlToFile( string url )
		{
			var transformed = url.Replace( $"/{Path}/", $"/{Index}/" );

			if( transformed.StartsWith( "//" ) || !transformed.StartsWith( "/" ) )
			{
				try
				{
					var idx = transformed.IndexOf( "//" );
					if( idx >= 0 )
					{
						idx = transformed.IndexOf( "/", idx + 2 );
						transformed = transformed.Substring( idx );
					}

					if( !transformed.StartsWith( "/" ) )
					{
						transformed = "/" + transformed;
					}
				}
				catch
				{
					return url;
				}
			}

			return transformed;
		}
	}
}