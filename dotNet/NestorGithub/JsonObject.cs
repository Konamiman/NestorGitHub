using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Konamiman.NestorGithub
{
    class JsonObject
    {
        private readonly JObject jObject;

        public JsonObject(JObject jObject)
        {
            this.jObject = jObject;
        }

        public T Value<T>(string key) where T:class
        {
            if(typeof(T) == typeof(JsonObject[]))
                return jObject[key].Values<JObject>().Select(o => new JsonObject(o)).ToArray() as T;

            return jObject[key].Value<T>();
        }

        public IEnumerable<string> Keys =>
            jObject.Properties().Select(p => p.Name).ToArray();

        public bool HasKey(string key) =>
            jObject.ContainsKey(key);
    }
}
