using System;
using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace EVEDataUtils
{
    public class Serialization
    {
        private static readonly ConcurrentDictionary<Type, XmlSerializer> SerializerCache = new ConcurrentDictionary<Type, XmlSerializer>();

        private static XmlSerializer GetSerializer(Type type)
        {
            return SerializerCache.GetOrAdd(type, t => new XmlSerializer(t));
        }

        public static T DeserializeFromDisk<T>(string filename)
        {
            try
            {
                XmlSerializer xms = GetSerializer(typeof(T));
                using(FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                using(XmlReader xmlr = XmlReader.Create(fs))
                {
                    return (T)xms.Deserialize(xmlr);
                }
            }
            catch
            {
                return default(T);
            }
        }

        public static void SerializeToDisk<T>(T obj, string fileName)
        {
            XmlSerializer xms = GetSerializer(typeof(T));

            using(TextWriter tw = new StreamWriter(fileName))
            {
                xms.Serialize(tw, obj);
            }
        }
    }
}
