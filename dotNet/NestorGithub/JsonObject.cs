using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Konamiman.NestorGithub
{
    /// <summary>
    /// This class is used to decouple the application code from the specific JSON management library used,
    /// if some day we decide to use a different package all we need to change is this class
    /// and the code that instantiates it.
    /// </summary>
    class JsonObject
    {
        private readonly JObject jObject;

        public JsonObject(JObject jObject)
        {
            this.jObject = jObject;
        }

        public bool IsEmpty => jObject == null;

        public T Value<T>(string key, bool returnDefaultOnMissingKey = false) where T:class
        {
            if (typeof(T) == typeof(JsonObject))
                return new JsonObject(jObject[key] as JObject) as T;

            if (typeof(T) == typeof(JsonObject[]))
                return jObject[key].Values<JObject>().Select(o => new JsonObject(o)).ToArray() as T;

            if (key.Contains("/"))
            {
                return jObject.SelectToken(key.Replace("/", ".")).Value<T>();
            }
            else
            {
                return returnDefaultOnMissingKey && !HasKey(key) ? default(T) : jObject[key].Value<T>();
            }
        }

        public bool HasValue(string key) =>
            jObject.SelectToken(key.Replace("/", ".")).HasValues;

        public IEnumerable<string> Keys =>
            jObject.Properties().Select(p => p.Name).ToArray();

        public bool HasKey(string key) =>
            jObject.ContainsKey(key);
    }
}
