using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        static int Main(string[] args)
        {
            if(Debugger.IsAttached)
                //args = new[] { "commit", "-d", @"c:\temp\nex", "First commit from NestorGithub!!"};
                //args = new[] { "clone", "sandbox", @"c:\temp\nex" };
                //args = new[] { "pull", "-d", @"c:\temp\TestingNgh" };
                //args = new[] { "branch", "-d", "branchie2" };
                args = new[] { "status" };

            var result = new Program().Run(args);
            if (Debugger.IsAttached) Console.ReadKey();
            return result;
        }

        public Program()
        {
        }

        string user, password;

        int Run(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            UI.Print("NestorGithub 1.0 - (c) Konamiman 2018\r\n\r\n");

            if (args.Length == 0)
            {
                UI.Print($"Usage:\r\n\r\n{GetCommandLines().JoinInLines()}\r\n\r\n");
                return 0;
            }

            if (args[0].Equals("-a", StringComparison.InvariantCultureIgnoreCase))
            {
                user = "";
                password = "";
                args = args.Skip(1).ToArray();
            }
            else
            {
                user = Configuration.GithubUser.Trim();
                password = Configuration.GithubPasswordOrToken;
            }

            try
            {
                RunCommand(args[0], args.Skip(1).ToArray());
                return 0;
            }
            catch (ApiException ex)
            {
                UI.Print($"*** {ex.Message}\r\n{ex.PrintableErrorsList}");
                return 1;
            }
            catch (InvalidOperationException ex)
            {
                UI.Print($"*** {ex.Message}\r\n");
                return 1;
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                UI.Print($"*** {ex.GetType().Name}: {ex.Message}\r\n{ex.StackTrace}");
                return 1;
            }
        }

        void RunCommand(string name, string[] args)
        {
            var method = this.GetType().GetMethod($"{name}Command", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (method == null)
                throw BadParameter($"Unknown command '{name}'");

            try
            {
                method.Invoke(this, new object[] { args });
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        string[] GetCommandLines()
        {
            return this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.Name.EndsWith("CommandLine"))
                .Select(f => (string)f.GetValue(null))
                .OrderBy(x => x)
                .ToArray();
        }

        LocalRepository GetExistingLocalRepository(string directoryPath = null)
        {
            var repositoryName = LocalRepository.GetRepositoryNameFor(directoryPath);
            var api = GetApi(repositoryName);
            return new LocalRepository(directoryPath, GetApi(repositoryName));
        }

        ApiClient GetApi(string fullRepositoryName)
        {
            return new ApiClient(new HttpClient(), user, password, fullRepositoryName);
        }

        string FullRepositoryName(string repositoryName)
        {
            if (IsFullRepositoryName(repositoryName))
                return repositoryName;

            if (user == "")
              throw new InvalidOperationException("Username is not configured, please specify a full repository name (user/repository)");

            return $"{user}/{repositoryName}";
        }

        bool IsFullRepositoryName(string name) => name.Contains("/");

        Exception BadParameter(string message) => new InvalidOperationException(message);

        bool IsFlagParam(string param, string expected)
        {
            return param.Equals($"-{expected}", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
