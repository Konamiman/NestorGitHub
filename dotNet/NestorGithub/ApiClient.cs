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
        private readonly string repositoryOwner;
        private readonly string repositoryName;

        public ApiClient(IHttpClient httpClient, string user, string passwordOrToken, string fullRepositoryName)
        {
            if (!fullRepositoryName.Contains("/"))
                throw new ArgumentException($"{fullRepositoryName} is not a valid full repository name (it doesn't contain '/')");

            this.FullRepositoryName = fullRepositoryName;
            var repositoryNameParts = fullRepositoryName.SplitBySlash();
            repositoryOwner = repositoryNameParts[0];
            repositoryName = repositoryNameParts[1];

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
            return new CreateRepositoryResponse() { RespositoryFullName = response.AsJsonObject().Value("full_name") };
        }

        public void DeleteRepository()
        {
            Delete($"/repos/{FullRepositoryName}");
        }

        public string GetBranchCommitSha(string branchName)
        {
            var query = GraphqlQueryForRepository($"ref (qualifiedName: {branchName.AsJson()}) {{ commit: target {{ ... on Commit {{ oid }} }} }}");
            var result = DoGraphQl(query);
            if (!result.HasValue("repository/ref")) return null;
            return result.Value("repository/ref/commit/oid");
        }

        public void SetBranchCommitSha(string branchName, string commitSha, bool force = false)
        {
            var input = $@"
{{
  ""sha"": {commitSha.AsJson()},
  ""force"": {force.AsJson()}
}}";

            Patch($"/repos/{FullRepositoryName}/git/refs/heads/{branchName}", input);
        }

        public string GetCommitTreeSha(string commitSha)
        {
            var query = GraphqlQueryForRepository($"object(oid: {commitSha.AsJson()}) {{ ...on Commit {{ tree {{ oid }} }} }}");
            var result = DoGraphQl(query);
            return result.Value("repository/object/tree/oid");
        }

        public string CreateCommit(string message, string treeSha, string parentSha, string authorName, string authorEmail)
        {
            var json = $@"
{{
    ""message"": {message.AsJson()},
    ""tree"": ""{treeSha}""
";
            if (parentSha != null)
                json += $@", ""parents"": [ ""{parentSha}"" ]
";

            if(!string.IsNullOrEmpty(authorName))
                json += $@", ""author"": {{ ""name"": {authorName.AsJson()}, ""email"": {authorEmail.AsJson()} }}
";

            json += "}";

            var result = Post($"/repos/{FullRepositoryName}/git/commits", json).AsJsonObject();

            var sha = result.Value("sha");
            return sha;
        }


        public RepositoryFileReference[] GetTreeFileReferences(string treeSha)
        {
            var response = Get<string>($"/repos/{FullRepositoryName}/git/trees/{treeSha}?recursive=1").AsJsonObject();
            var treeData = response.Value<JsonObject[]>("tree");
            return treeData
                .Where(item => item.Value("type") == "blob")
                .Select(item => new RepositoryFileReference {
                    Path = item.Value("path"),
                    Size = long.Parse(item.Value("size")),
                    BlobSha = item.Value("sha") })
                .ToArray();
        }

        public string CreateTree(RepositoryFileReference[] files)
        {
            var sb = new StringBuilder(@"{ ""tree"": [");
            foreach (var file in files)
            {
                sb.Append($@"
{{
    ""path"": {file.Path.AsJson()},
    ""mode"": ""100644"",
    ""type"": ""blob"",
    ""sha"": ""{file.BlobSha}""
}},");
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append("] }");

            var result = Post($"/repos/{FullRepositoryName}/git/trees", sb.ToString()).AsJsonObject();

            var sha = result.Value("sha");
            return sha;
        }

        public byte[] GetBlob(string blobSha)
        {
            return Get<byte[]>($"/repos/{FullRepositoryName}/git/blobs/{blobSha}");
        }

        public string CreateBlob(byte[] blobContents)
        {
            var input = $@"
{{
  ""content"": {Convert.ToBase64String(blobContents).AsJson()},
  ""encoding"": ""base64""
}}";

            var result = Post($"/repos/{FullRepositoryName}/git/blobs", input).AsJsonObject();
            var sha = result.Value("sha");
            return sha;
        }

        public RepositoryInfo GetRepositoryInfo()
        {
            var query = GraphqlQueryForRepository("defaultBranchRef { name }");
            var result = DoGraphQl(query);
            var defaultBranch = result.Value("repository/defaultBranchRef/name");
            return new RepositoryInfo { DefaultBranch = defaultBranch };
        }

        public bool RepositoryExists()
        {
            var query = GraphqlQueryForRepository($"isPrivate");
            var result = DoGraphQl(query, returnNullWhenNotFound: true);
            return result != null;
        }

        public int GetBranchCount()
        {
            var query = GraphqlQueryForRepository("refs(refPrefix: \"refs/heads/\", first: 0) { totalCount }");
            var result = DoGraphQl(query);
            var count = result.Value("repository/refs/totalCount");
            return int.Parse(count);
        }

        public string[] GetBranches()
        {
            var query = GraphqlQueryForRepository("refs(refPrefix: \"refs/heads/\", first: 100) { nodes { name } }");
            var result = DoGraphQl(query);
            var nodes = result.Value<JsonObject>("repository").Value<JsonObject>("refs").Value<JsonObject[]>("nodes");
            return nodes.Select(n => n.Value("name")).ToArray();
        }

        public string GetProperlyCasedRepositoryName()
        {
            var query = GraphqlQueryForRepository($"owner {{login}} name");
            var result = DoGraphQl(query, returnNullWhenNotFound: true);
            if(result == null) return null;
            var owner = result.Value("repository/owner/login");
            var repoName = result.Value("repository/name");
            return $"{owner}/{repoName}";
        }

        public void CreateFileInRemoteRepository(string path, string branch, string commitMessage, byte[] contents)
        {
            var input = $@"
{{
  ""branch"": {branch.AsJson()},
  ""message"": {commitMessage.AsJson()},
  ""content"": {Convert.ToBase64String(contents).AsJson()}
}}";
            Put($"repos/{FullRepositoryName}/contents/{path}", input);
            
        }

        public void CreateBranch(string branch, string commitSha)
        {
            var input = $@"
{{
  ""ref"": {$"refs/heads/{branch}".AsJson()},
  ""sha"": {commitSha.AsJson()}
}}";
            Post($"repos/{FullRepositoryName}/git/refs", input);
        }

        internal void MergeBranches(string sourceBranch, string baseBranch, string commitMessage)
        {
            var json = $@"
{{
    ""base"": {baseBranch.AsJson()},
    ""head"": {sourceBranch.AsJson()}
";
            if (commitMessage != null)
                json += $@", ""commit_message"": {commitMessage.AsJson()}
";

            json += "}";

            Post($"repos/{FullRepositoryName}/merges", json);
        }

        internal void DeleteBranch(string branchName)
        {
            Delete($"repos/{FullRepositoryName}/git/refs/heads/{branchName}");
        }

        private string GraphqlQueryForRepository(string queryBody)
        {
            return $"query {{ repository(owner: {repositoryOwner.AsJson()}, name: {repositoryName.AsJson()}) {{ {queryBody} }} }}";
        }

        private T Get<T>(string path) where T:class
        {
            return DoRestApi(() => httpClient.ExecuteRequest<T>(HttpMethod.Get, path));
        }

        private string Post(string path, string content = null)
        {
            return DoRestApi(() => httpClient.ExecuteRequest<string>(HttpMethod.Post, path, content));
        }

        private string Put(string path, string content = null)
        {
            return DoRestApi(() => httpClient.ExecuteRequest<string>(HttpMethod.Put, path, content));
        }

        private static readonly HttpMethod httpPatch = new HttpMethod("PATCH");
        private string Patch(string path, string content = null)
        {
            return DoRestApi(() => httpClient.ExecuteRequest<string>(httpPatch, path, content));
        }

        private string Delete(string path)
        {
            return DoRestApi(() => httpClient.ExecuteRequest<string>(HttpMethod.Delete, path));
        }

        private T DoRestApi<T>(Func<HttpResponse<T>> apiAction) where T:class
        {
            var response = apiAction();
            if (response.IsSuccess)
                return response.Content;
            else
                throw ApiException.FromResponse(response);
        }

        private JsonObject DoGraphQl(string query, bool returnNullWhenNotFound = false)
        {
            var body = $@"{{ ""query"": {query.AsJson()} }}".Replace("\r\n", "");
            var response = httpClient.ExecuteRequest<string>(HttpMethod.Post, "https://api.github.com/graphql", body);
            if (response.IsError)
                throw ApiException.FromResponse(response);

            var responseJson = response.Content.AsJsonObject();
            if (responseJson.HasKey("errors"))
            {
                if (returnNullWhenNotFound && responseJson.Value<JsonObject[]>("errors").Any(e => e.Value("type", true) == "NOT_FOUND"))
                    return null;
                else
                    throw ApiException.FromGraphqlResponse(response);
            }

            return responseJson.Value<JsonObject>("data");
        }
    }
}
