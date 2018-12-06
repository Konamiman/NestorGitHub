using System.Collections.Generic;
using System.Net.Http;

namespace Konamiman.NestorGithub
{
    interface IHttpClient
    {
        void SetHeaders(IDictionary<string, string> headers);

        void SetUrl(string url);

        HttpResponse<T> ExecuteRequest<T>(HttpMethod method, string path, string content = null, string accept = null) where T : class;
    }
}