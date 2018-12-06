using System;
using System.Collections.Generic;
using System.Linq;

namespace Konamiman.NestorGithub
{
    class ApiException : Exception
    {
        public static ApiException FromGraphqlResponse(HttpResponse<string> response)
        {
            var jsonObject = response.Content.AsJsonObject();
            var errorsObject = jsonObject.Value<JsonObject[]>("errors");
            var firstError = errorsObject[0];
            var message = firstError.Value("message");
            var errors = 
                firstError.HasKey("type") ?
                new Dictionary<string, string>[] { new Dictionary<string, string> { { "type", firstError.Value("type") } } }
                :
                new Dictionary<string, string>[0];


            return new ApiException(
                $"{response.StatusCode} {response.StatusMessage}: {message}",
                response.StatusCode,
                response.Content,
                errors
            );
        }

        public static ApiException FromResponse<T>(HttpResponse<T> response) where T:class
        {
            if (typeof(T) != typeof(string))
                return new ApiException(
                    $"{response.StatusCode} {response.StatusMessage}",
                    response.StatusCode,
                    "",
                    new Dictionary<string, string>[0]);

            var content = response.Content as string;
            var jsonObject = content.AsJsonObject();
            var message = jsonObject.IsEmpty ? "(no message)" : jsonObject.Value("message");
            var errors = !jsonObject.IsEmpty && jsonObject.HasKey("errors") ?
                jsonObject
                    .Value<JsonObject[]>("errors")
                    .Select(e => e.Keys.ToDictionary(k => k, k => e.Value(k)))
                    .ToArray()
                :
                    new Dictionary<string, string>[0];

            return new ApiException(
                $"{response.StatusCode} {response.StatusMessage}: {message}",
                response.StatusCode,
                content,
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

        public string PrintableErrorsList => Errors.Select(e => e.Select(kvp => $"{kvp.Key} = {kvp.Value}").JoinInLines()).JoinInLines();
    }
}
