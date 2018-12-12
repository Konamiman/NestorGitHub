using System.Collections.Generic;
using System.Net.Http;

namespace Konamiman.NestorGithub
{
    interface IHttpClient
    {
        void SetDefaultHeaders(IDictionary<string, string> headers);

        void SetUrl(string url);

        HttpResponse<T> ExecuteRequest<T>(HttpMethod method, string path, string accept, string content = null) where T : class;
    }
}