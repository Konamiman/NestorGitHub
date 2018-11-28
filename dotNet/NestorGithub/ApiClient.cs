using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Linq;

namespace Konamiman.NestorGithub
{
    class ApiClient
    {
        private readonly IHttpClient httpClient;

        public ApiClient(IHttpClient httpClient, string user, string passwordOrToken, string fullRepositoryName)
        {
            if (!fullRepositoryName.Contains("/"))
                throw new ArgumentException($"{fullRepositoryName} is not a valid full repository name (it doesn't contain '/')");

            this.FullRepositoryName = fullRepositoryName;

            this.httpClient = httpClient;
            httpClient.SetUrl("https://api.github.com");

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "NestorGithub for .NET" },
                { "Accept", "application/vnd.github.v3+json" }
            };

            if (!string.IsNullOrWhiteSpace(user))
                headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{passwordOrToken}")));

            httpClient.SetHeaders(headers);
        }

        public string FullRepositoryName { get; }

        public CreateRepositoryResponse CreateRepository(string description, bool @private = false)
        {
            var name = FullRepositoryName.SplitBySlash()[1];

            var input = $@"
{{
  ""name"": {name.AsJson()},
  ""description"": {description.AsJson()},
  ""private"": {@private.AsJson()}
}}";

            var response = Post("/user/repos", input);
            return new CreateRepositoryResponse() { RespositoryFullName = response.AsJsonObject().Value<string>("full_name") };
        }

        public void DeleteRepository()
        {
            Delete($"/repos/{FullRepositoryName}");
        }

        public string GetBranchCommitSha(string branchName)
        {
            var response = Get($"/repos/{FullRepositoryName}/git/refs/heads/{branchName}", returnNullWhenNotFound: true);
            if (response == null)
                return null;

            return response.AsJsonObject().Value<JsonObject>("object").Value<string>("sha");
        }

        public string[] GetBranchNames()
        {
            var response = Get($"/repos/{FullRepositoryName}/git/refs/heads");

            return response.AsArrayOfJsonObjects().Select(o => o.Value<string>("ref").Substring("refs/heads/".Length)).ToArray();
        }

        public CommitData GetCommitData(string commitSha)
        {
            var response = Get($"/repos/{FullRepositoryName}/git/commits/{commitSha}").AsJsonObject();
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

        public RepositoryFileReference[] GetTreeFiles(string commitSha)
        {
            var treeSha = GetCommitData(commitSha).TreeSha;
            var response = Get($"/repos/{FullRepositoryName}/git/trees/{treeSha}?recursive=1").AsJsonObject();
            var treeData = response.Value<JsonObject[]>("tree");
            return treeData
                .Where(item => item.Value<string>("type") == "blob")
                .Select(item => new RepositoryFileReference {
                    Path = item.Value<string>("path"),
                    Size = long.Parse(item.Value<string>("size")),
                    BlobSha = item.Value<string>("sha") })
                .ToArray();
        }

        public byte[] GetBlob(string blobSha)
        {
            var result = Get($"/repos/{FullRepositoryName}/git/blobs/{blobSha}").AsJsonObject();
            return Convert.FromBase64String(result.Value<string>("content"));
        }

        public RepositoryInfo GetRepositoryInfo()
        {
            var result = Get($"/repos/{FullRepositoryName}").AsJsonObject();
            return new RepositoryInfo { DefaultBranch = result.Value<string>("default_branch") };
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
