using Microsoft.Extensions.DependencyModel;
using StructureMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Seagal_TransformHttpContentToHttps.Analys
{
    public class AnalysRepository : IAnalysRepository
    {
        private static HashSet<Type> _analysis = new HashSet<Type>();
        private static List<ISourceRewrites> _instances = new List<ISourceRewrites>();

        private static void RegisterAnalys( IServiceProvider serviceProvider, Type type )
        {
            if( _analysis.Add( type ) )
            {
                var container = (serviceProvider as StructureMapServiceProvider).Container;
                var analys = container.GetInstance( type ) as ISourceRewrites;
                if( analys == null )
                {
                    _analysis.Remove( type );
                    return;
                }

                _instances.Add( analys );
            }
        }

        public static void Initialize( IServiceProvider serviceProvider )
        {
            var dependencies = DependencyContext.Default.RuntimeLibraries;
            foreach( var library in dependencies )
            {
                try
                {
                    var assembly = Assembly.Load( new AssemblyName( library.Name ) );
                    var types = assembly.ExportedTypes;
                    var analysInterfaceType = typeof( ISourceRewrites );
                    foreach( var type in types )
                    {
                        if( analysInterfaceType.IsAssignableFrom( type ) )
                        {
                            var typeInfo = type.GetTypeInfo();
                            if( typeInfo.IsClass && !typeInfo.IsAbstract )
                            {
                                RegisterAnalys( serviceProvider, type );
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        public IReadOnlyList<ISourceRewrites> Analysis => _instances.AsReadOnly();

        public ISourceRewrites GetAnalys( string name ) => _instances?.SingleOrDefault( i => i.Name == name );
    }
}
