using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Migration.Analys;
using Migration.Core;
using StructureMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Migration.Transforms
{
    public class AnalysRepository : IAnalysRepository
    {
        private static HashSet<Type> _analysis = new HashSet<Type>();
        private static List<IAnalys> _instances = new List<IAnalys>();

        private static void RegisterAnalys( IServiceProvider serviceProvider, Type type )
        {
            if( _analysis.Add( type ) )
            {
                var container = (serviceProvider as StructureMapServiceProvider).Container;
                var analys = container.GetInstance( type ) as IAnalys;
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
                    var analysInterfaceType = typeof( IAnalys );
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

        public IReadOnlyList<IAnalys> Analysis => _instances.AsReadOnly();

        public IAnalys GetAnalys( string name ) => _instances?.SingleOrDefault( i => i.Name == name );
    }
}
