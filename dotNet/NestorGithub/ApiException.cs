using System;
using System.Collections.Generic;
using System.Linq;

namespace Konamiman.NestorGithub
{
    class ApiException : Exception
    {
        public static ApiException FromResponse(HttpResponse response)
        {
            var jsonObject = response.Content.AsJsonObject();
            var message = jsonObject.Value<string>("message");
            var errors = jsonObject.HasKey("errors") ?
                jsonObject
                    .Value<JsonObject[]>("errors")
                    .Select(e => e.Keys.ToDictionary(k => k, k => e.Value<string>(k)))
                    .ToArray()
                :
                    new Dictionary<string, string>[0];

            return new ApiException(
                $"{response.StatusCode} {response.StatusMessage}: {message}",
                response.StatusCode,
                response.Content,
                errors
            );
        }

        public ApiException(string message, int statusCode, string jsonContent, IDictionary<string, string>[] Errors) : base(message)
        {
            this.StatusCode = statusCode;
            this.JsonContent = jsonContent;
            this.Errors = Errors;
        }

        public int StatusCode { get; }

        public string JsonContent { get; }

        public IDictionary<string, string>[] Errors { get; }
    }
}
