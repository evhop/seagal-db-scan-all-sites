using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Seagal_TransformHttpContentToHttps.Core
{
    public class Serializer
    {
        private readonly NumberFormatInfo _nfi;
        public Encoding StringEncoding = new UTF8Encoding();

        private int _pos;
        private Dictionary<List<object>, bool> _seenArrayLists;
        private Dictionary<Dictionary<object, object>, bool> _seenHashtables;

        public Serializer()
        {
            _nfi = new NumberFormatInfo { NumberGroupSeparator = "", NumberDecimalSeparator = "." };
        }

        public string Serialize(object obj)
        {
            _seenArrayLists = new Dictionary<List<object>, bool>();
            _seenHashtables = new Dictionary<Dictionary<object, object>, bool>();

            return serialize(obj, new StringBuilder()).ToString();
        }

        private StringBuilder serialize(object obj, StringBuilder sb)
        {
            if (obj == null) return sb.Append("N;");
            if (obj is string)
            {
                var str = (string)obj;
                return sb.Append($"s:{StringEncoding.GetByteCount(str)}:\"{str}\";");
            }
            if (obj is bool) return sb.Append($"b:{(((bool)obj) ? "1" : "0")};");
            if (obj is int)
            {
                var i = (int)obj;
                return sb.Append($"i:{i.ToString(_nfi)};");
            }
            if (obj is double)
            {
                var d = (double)obj;
                return sb.Append($"d:{d.ToString(_nfi)};");
            }
            if (obj is List<object>)
            {
                if (_seenArrayLists.ContainsKey((List<object>)obj))
                    return sb.Append("N;");
                _seenArrayLists.Add((List<object>)obj, true);

                var a = (List<object>)obj;
                sb.Append("a:" + a.Count + ":{");
                for (int i = 0; i < a.Count; i++)
                {
                    serialize(i, sb);
                    serialize(a[i], sb);
                }
                sb.Append("}");
                return sb;
            }
            if (obj is Dictionary<object, object>)
            {
                if (_seenHashtables.ContainsKey((Dictionary<object, object>)obj))
                    return sb.Append("N;");
                _seenHashtables.Add((Dictionary<object, object>)obj, true);

                var a = (Dictionary<object, object>)obj;
                sb.Append("a:" + a.Count + ":{");
                foreach (var entry in a)
                {
                    serialize(entry.Key, sb);
                    serialize(entry.Value, sb);
                }
                sb.Append("}");
                return sb;
            }
            return sb;
        }

        public object Deserialize(string str)
        {
            _pos = 0;
            return deserialize(str);
        }

        private object deserialize(string str)
        {
            if (str == null || str.Length <= 0)
            { 
                return new Object();
            }

            int start, end, lenght;
            string strLen;
            
            switch (str[_pos])
            {
                case 'N':
                    _pos += 2;
                    return null;
                case 'b':
                    char chBool = str[_pos + 2];
                    _pos += 4;
                    return chBool == '1';
                case 'i':
                    start = str.IndexOf(":", _pos) + 1;
                    end = str.IndexOf(";", start);
                    var strInt = str.Substring(start, end - start);
                    _pos += 3 + strInt.Length;
                    return Int32.Parse(strInt, _nfi);
                case 'd':
                    start = str.IndexOf(":", _pos) + 1;
                    end = str.IndexOf(";", start);
                    var strDouble = str.Substring(start, end - start);
                    _pos += 3 + strDouble.Length;
                    return Double.Parse(strDouble, _nfi);
                case 's':
                    start = str.IndexOf(":", _pos) + 1;
                    end = str.IndexOf(":", start);
                    strLen = str.Substring(start, end - start);
                    var byteLen = Int32.Parse(strLen);
                    lenght = byteLen;
                    if ((end + 2 + lenght) >= str.Length)
                    {
                        lenght = str.Length - 2 - end;
                    }
                    var strRet = str.Substring(end + 2, lenght);
                    while (StringEncoding.GetByteCount(strRet) > byteLen)
                    {
                        lenght--;
                        strRet = str.Substring(end + 2, lenght);
                    }
                    _pos += 6 + strLen.Length + lenght;
                    return strRet;
                case 'a':
                    start = str.IndexOf(":", _pos) + 1;
                    end = str.IndexOf(":", start);
                    strLen = str.Substring(start, end - start);
                    lenght = Int32.Parse(strLen);
                    var htRet = new Dictionary<object, object>();
                    var alRet = new List<object>();

                    _pos += 4 + strLen.Length;

                    for (int i = 0; i < lenght; i++)
                    {
                        var key = deserialize(str);
                        var value = deserialize(str);

                        if (alRet != null)
                        {
                            if (key is int && (int)key == alRet.Count)
                            {
                                alRet.Add(value);
                            }
                            else
                            {
                                alRet = null;
                            }
                        }
                        htRet[key] = value;
                    }
                    _pos++;
                    if (_pos < str.Length && str[_pos] == ';')
                    {
                        _pos++;
                    }
                    return alRet != null ? (object)alRet : htRet;
                default:
                    if (!str.Contains(";"))
                    {
                        return str;
                    }
                    return "";
            }
        }
    }
}
