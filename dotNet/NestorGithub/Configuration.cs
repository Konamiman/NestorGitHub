using System.Configuration;
using System.Diagnostics;

namespace Konamiman.NestorGithub
{
    static class Configuration
    {
        public static string GithubUser => ConfigurationManager.AppSettings["GithubUser"];
        public static string GithubPasswordOrToken => ConfigurationManager.AppSettings["GithubPasswordOrToken"];
        public static string AuthorName => ConfigurationManager.AppSettings["AuthorName"];
        public static string AuthorEmail => ConfigurationManager.AppSettings["AuthorEmail"];
    }
}
