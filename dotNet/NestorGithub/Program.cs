using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        static int Main(string[] args)
        {
            if(Debugger.IsAttached)
                //args = new[] { "commit", "-d", @"c:\temp\nex", "First commit from NestorGithub!!"};
                //args = new[] { "clone", "sandbox", @"c:\temp\nex" };
                args = new[] { "status", @"c:\temp\nex" };

            var result = new Program().Run(args);
            if (Debugger.IsAttached) Console.ReadKey();
            return result;
        }

        readonly Dictionary<string, Action<string[]>> actions;
                
        public Program()
        {
            actions = new Dictionary<string, Action<string[]>>
            {
                { "new", CreateRepository },
                { "destroy", DestroyRepository },
                { "clone", args => CloneRepository(args, false) },
                { "link", args => CloneRepository(args, true) },
                { "unlink", UnlinkRepository },
                { "status", Status },
                { "commit", Commit }
            };
        }

        string user, password;

        int Run(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Print("NestorGithub 1.0 - (c) Konamiman 2018\r\n\r\n");

            if(args.Length > 0 && args[0].Equals("-a", StringComparison.InvariantCultureIgnoreCase))
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

             if (args.Length == 0 || !actions.Keys.Contains(args[0], StringComparer.CurrentCultureIgnoreCase))
            {
                Print(explanation);
                return 0;
            }

            try
            {
                actions[args[0]](args.Skip(1).ToArray());
                return 0;
            }
            catch (ApiException ex)
            {
                Print($"*** {ex.Message}\r\n{ex.PrintableErrorsList}");
                return 1;
            }
            catch (InvalidOperationException ex)
            {
                Print($"*** {ex.Message}\r\n");
                return 1;
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                Print($"*** {ex.GetType().Name}: {ex.Message}\r\n{ex.StackTrace}");
                return 1;
            }
        }

        LocalRepository GetExistingLocalRepository(FilesystemDirectory localDirectory)
        {
            var repositoryName = LocalRepository.GetRepositoryNameFor(localDirectory.PhysicalPath);
            var api = GetApi(repositoryName);
            return new LocalRepository(localDirectory, GetApi(repositoryName));
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

        void Print(string text)
        {
            Printer.Print(text);
        }

        void PrintLine(string text)
        {
            Printer.PrintLine(text);
        }
    }
}
