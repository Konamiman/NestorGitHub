using System.Configuration;
using System.Diagnostics;

namespace Konamiman.NestorGithub
{
    static class Configuration
    {
        public static string GithubUser => ConfigurationManager.AppSettings["GithubUser"];
        public static string GithubPasswordOrToken => ConfigurationManager.AppSettings["GithubPasswordOrToken"];
    }
}
