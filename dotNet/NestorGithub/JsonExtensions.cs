using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Konamiman.NestorGithub
{
    static class JsonExtensions
    {
        public static string AsJson(this string value) =>
            JsonConvert.ToString(value);

        public static string AsJson(this bool value) =>
            value ? "true" : "false";

        public static JsonObject AsJsonObject(this string value) =>
            new JsonObject((JObject)JsonConvert.DeserializeObject(value));

        public static JsonObject[] AsArrayOfJsonObjects(this string value) =>
            ((JArray)JsonConvert.DeserializeObject(value)).Select(o => new JsonObject((JObject)o)).ToArray();
    }
}
