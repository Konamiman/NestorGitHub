using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Konamiman.NestorGithub
{
    class ApiClient
    {
        private readonly string user;
        private readonly IHttpClient httpClient;

        public ApiClient(IHttpClient httpClient, string user, string passwordOrToken)
        {
            this.user = user;

            this.httpClient = httpClient;
            httpClient.SetUrl("https://api.github.com");
            httpClient.SetHeaders(new Dictionary<string, string>
            {
                { "User-Agent", "NestorGithub for .NET" },
                { "Accept", "application/vnd.github.v3+json" },
                { "Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{passwordOrToken}")) }
            });
        }

        public CreateRepositoryResponse CreateRepository(string name, string description, bool @private = false)
        {
            var input = $@"
{{
  ""name"": {name.AsJson()},
  ""description"": {description.AsJson()},
  ""private"": {@private.AsJson()}
}}";

            var response = Post("/user/repos", input);
            return new CreateRepositoryResponse() { RespositoryFullName = response.AsJsonObject().Value<string>("full_name") };
        }

        public void DeleteRepository(string name)
        {
            Delete($"/repos/{user}/{name}");
        }

        private string Get(string path)
        {
            return DoApi(() => httpClient.ExecuteRequest(HttpMethod.Get, path));
        }

        private string Post(string path, string content = null)
        {
            return DoApi(() => httpClient.ExecuteRequest(HttpMethod.Post, path, content));
        }

        private string Delete(string path)
        {
            return DoApi(() => httpClient.ExecuteRequest(HttpMethod.Delete, path));
        }

        private string DoApi(Func<HttpResponse> apiAction)
        {
            var response = apiAction();
            if (response.IsSuccess)
                return response.Content;
            else
                throw ApiException.FromResponse(response);
        }
    }
}
