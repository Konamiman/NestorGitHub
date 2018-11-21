using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Konamiman.NestorGithub
{
    class HttpClient : IHttpClient
    {
        System.Net.Http.HttpClient httpClient;

        public void SetHeaders(IDictionary<string, string> headers)
        {
            foreach (var kv in headers)
                httpClient.DefaultRequestHeaders.Add(kv.Key, kv.Value);
        }

        public void SetUrl(string url)
        {
            httpClient.BaseAddress = new Uri(url);
        }

        public HttpClient()
        {
            httpClient = new System.Net.Http.HttpClient();
        }

        public HttpResponse ExecuteRequest(HttpMethod method, string path, string content = null)
        {
            var request = new HttpRequestMessage(method, path);
            if (content != null) request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            var response = new HttpResponse();

            var task = httpClient.SendAsync(request)
                .ContinueWith((taskwithmsg) =>
                {
                    var r = taskwithmsg.Result;
                    response.StatusCode = (int)r.StatusCode;
                    response.StatusMessage = r.ReasonPhrase;

                    var readTask = r.Content.ReadAsStringAsync();
                    readTask.Wait();
                        response.Content = readTask.Result;
                });
            task.Wait();

            return response;
        }
    }
}
