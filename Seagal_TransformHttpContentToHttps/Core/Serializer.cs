using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace WPDatabaseWork.Core
{
    internal class Serializer
    {
        private Stream _stream;
        private StreamWriter _writer;

        public Serializer(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _writer = new StreamWriter(_stream, new UTF8Encoding(false));
        }

        public void Serialize(object value)
        {
            if (value is IDictionary<object, object> dictionary)
            {
                InternalSerializeDictionary(dictionary);
            }
            else if (value is IEnumerable enumerable)
            {
                InternalSerializeEnumerable(enumerable);
            }
            else
            {
                InternalSerializeObject(value);
            }
        }

        private void InternalSerializeObject(object value)
        {
            var type = value.GetType();
            var typeInfo = type.GetTypeInfo();

            var properties = typeInfo.GetProperties();

            Write($"a:{properties.Count()}:{{");
            foreach (var property in properties)
            {
                var propertyName = property.Name;
                var propertyValue = property.GetValue(value);

                WriteSingle(propertyName);
                WriteSingle(propertyValue);
            }
            Write("}");
        }

        private void InternalSerializeEnumerable(IEnumerable enumerable)
        {
            var enumerableCount = enumerable
                .Cast<object>()
                .Count();

            Write($"a:{enumerableCount}:{{");
            var index = 0;
            foreach (var o in enumerable)
            {
                WriteSingle(index);
                WriteSingle(o);
                index++;
            }
            Write("}");
        }

        private void InternalSerializeDictionary(IDictionary<object, object> dictionary)
        {
            Write($"a:{dictionary.Count}:{{");
            foreach (var o in dictionary.Keys)
            {
                var value = dictionary[o];
                WriteSingle(o);
                WriteSingle(value);
            }
            Write("}");
        }

        private void WriteSingle(object value)
        {
            if (value is int iValue)
            {
                Write($"i:{iValue};");
            }
            else if (value is long lValue)
            {
                Write($"i:{lValue};");
            }
            else if (value is ulong ulValue)
            {
                Write($"i:{ulValue};");
            }
            else if (value is uint uiValue)
            {
                Write($"i:{uiValue};");
            }
            else if (value is string str)
            {
                WriteString(str);
            }
            else if (value is bool b)
            {
                Write($"b:{(b ? 1 : 0)};");
            }
            else if (value is IDictionary<object, object> dictionary)
            {
                Serialize(dictionary);
            }
            else if (value is IEnumerable enumerable)
            {
                Serialize(enumerable.OfType<object>());
            }
            else
            {
                Serialize(value);
            }
        }

        private void Write(string text)
        {
            _writer.Write(text);
            _writer.Flush();
        }

        private void WriteString(string text)
        {
            var utf8 = new UTF8Encoding(false);
            var bytes = utf8.GetByteCount(text);
            Write($"s:{bytes}:\"{text}\";");
        }

        public void Dispose()
        {
        }
    }
}