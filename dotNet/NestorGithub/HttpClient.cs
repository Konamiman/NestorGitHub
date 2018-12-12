using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Konamiman.NestorGithub
{
    /// <summary>
    /// Generic HTTP client. It doesn't have anything specific to GitHub API, except that 
    /// assumes that the content to send, if any, is always JSON.
    /// </summary>
    class HttpClient : IHttpClient
    {
        readonly System.Net.Http.HttpClient httpClient;

        public void SetDefaultHeaders(IDictionary<string, string> headers)
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

        public HttpResponse<T> ExecuteRequest<T>(HttpMethod method, string path, string accept, string content = null) where T:class
        {
            bool isBinary = false;
            if (typeof(T) == typeof(byte[]))
                isBinary = true;
            else if (typeof(T) != typeof(string))
                throw new ArgumentException("The generic type must be either string or byte[]");

            var request = new HttpRequestMessage(method, path);
            if (content != null) request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            request.Headers.Add("Accept", accept);
            var response = new HttpResponse<T>();

            var task = httpClient.SendAsync(request)
                .ContinueWith((taskwithmsg) =>
                {
                    var r = taskwithmsg.Result;
                    response.StatusCode = (int)r.StatusCode;
                    response.StatusMessage = r.ReasonPhrase;

                    if (isBinary)
                    {
                        var readTask = r.Content.ReadAsByteArrayAsync();
                        readTask.Wait();
                        response.Content = readTask.Result as T;
                    }
                    else
                    {
                        var readTask = r.Content.ReadAsStringAsync();
                        readTask.Wait();
                        response.Content = readTask.Result as T;
                    }
                });
            task.Wait();

            return response;
        }
    }
}
