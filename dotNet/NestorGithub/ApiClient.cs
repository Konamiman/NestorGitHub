using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Linq;

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

        public string GetBranchCommitSha(string fullRepositoryName, string branchName)
        {
            var response = Get($"/repos/{fullRepositoryName}/git/refs/heads/{branchName}", returnNullWhenNotFound: true);
            if (response == null)
                return null;

            return response.AsJsonObject().Value<JsonObject>("object").Value<string>("sha");
        }

        public string[] GetBranchNames(string fullRepositoryName)
        {
            var response = Get($"/repos/{fullRepositoryName}/git/refs/heads");

            return response.AsArrayOfJsonObjects().Select(o => o.Value<string>("ref").Substring("refs/heads/".Length)).ToArray();
        }

        public CommitData GetCommitData(string fullRepositoryName, string commitSha)
        {
            var response = Get($"/repos/{fullRepositoryName}/git/commits/{commitSha}").AsJsonObject();
            var authorData = response.Value<JsonObject>("author");
            var treeData = response.Value<JsonObject>("tree");
            return new CommitData
            {
                Sha = commitSha,
                TreeSha = treeData.Value<string>("sha"),
                AuthorName = authorData.Value<string>("name"),
                AuthorEmail = authorData.Value<string>("email"),
                Date = DateTime.Parse(authorData.Value<string>("date"))
            };
        }

        public RepositoryFileReference[] GetTreeFiles(string fullRepositoryName, string commitSha)
        {
            var treeSha = GetCommitData(fullRepositoryName, commitSha).TreeSha;
            var response = Get($"/repos/{fullRepositoryName}/git/trees/{treeSha}?recursive=1").AsJsonObject();
            var treeData = response.Value<JsonObject[]>("tree");
            return treeData
                .Where(item => item.Value<string>("type") == "blob")
                .Select(item => new RepositoryFileReference { Path = item.Value<string>("path"), BlobSha = item.Value<string>("sha") })
                .ToArray();
        }

        public byte[] GetBlob(string fullRepositoryName, string blobSha)
        {
            var result = Get($"/repos/{fullRepositoryName}/git/blobs/{blobSha}").AsJsonObject();
            return Convert.FromBase64String(result.Value<string>("content"));
        }

        private string Get(string path, bool returnNullWhenNotFound = false)
        {
            return DoApi(() => httpClient.ExecuteRequest(HttpMethod.Get, path), returnNullWhenNotFound);
        }

        private string Post(string path, string content = null)
        {
            return DoApi(() => httpClient.ExecuteRequest(HttpMethod.Post, path, content));
        }

        private string Delete(string path)
        {
            return DoApi(() => httpClient.ExecuteRequest(HttpMethod.Delete, path));
        }

        private string DoApi(Func<HttpResponse> apiAction, bool returnNullWhenNotFound = false)
        {
            var response = apiAction();
            if (response.IsSuccess)
                return response.Content;
            else if (response.StatusCode == 404 && returnNullWhenNotFound)
                return null;
            else
                throw ApiException.FromResponse(response);
        }
    }
}
