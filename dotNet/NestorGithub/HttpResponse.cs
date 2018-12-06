namespace Konamiman.NestorGithub
{
    class HttpResponse<T> where T:class
    {
        public int StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public T Content { get; set; }

        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
        public bool IsError => !IsSuccess;

        public override string ToString()
        {
            var value = $"{StatusCode} {StatusMessage}";
            if (Content != null) value += $"\r\n\r\n{Content}";
            return value;
        }
    }
}
