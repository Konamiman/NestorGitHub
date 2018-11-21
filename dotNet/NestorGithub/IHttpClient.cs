using System.Collections.Generic;
using System.Net.Http;

namespace Konamiman.NestorGithub
{
    interface IHttpClient
    {
        void SetHeaders(IDictionary<string, string> headers);

        void SetUrl(string url);

        HttpResponse ExecuteRequest(HttpMethod method, string path, string content = null);
    }
}