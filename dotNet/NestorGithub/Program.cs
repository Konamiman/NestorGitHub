using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace Konamiman.NestorGithub
{
    class Program
    {
        static int Main(string[] args)
        {
            var result = new Program().Run(args);
            if (Debugger.IsAttached) Console.ReadKey();
            return result;
        }

        readonly Dictionary<string, Func<string[], int>> actions;
        ApiClient api;
        
        const string explanation =
@"Usage:

  ngh new [-p] <repository name> [<repository description>]

Creates a new repository in your GitHub account.
-p creates a private repository, you need a paid GitHub acount for that.

  ngh destroy <repository name>

Destroys a repository in your GitHub account.
Be careful, this can't be undone!

";

        public Program()
        {
            actions = new Dictionary<string, Func<string[], int>>
            {
                { "new", CreateRepository },
                { "destroy", DestroyRepository }
            };
        }

        int Run(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Print("NestorGithub 1.0 - (c) Konamiman 2018\r\n\r\n");

            if(args.Length == 0 || !actions.Keys.Contains(args[0], StringComparer.CurrentCultureIgnoreCase))
            {
                Print(explanation);
                return 0;
            }

            var user = Configuration.GithubUser;
            var password = Configuration.GithubPasswordOrToken;
            var http = new HttpClient();
            api = new ApiClient(http, user, password);

            try
            {
                return actions[args[0]](args.Skip(1).ToArray());
            }
            catch (ApiException ex)
            {
                Print($"*** {ex.Message}\r\n");
                Print(ex.Errors.Select(e => e.Select(kvp => $"{kvp.Key} = {kvp.Value}").JoinInLines()).JoinInLines());
                return 1;
            }
            catch (Exception ex)
            {
                Print($"*** {ex.GetType().Name}: {ex.Message}\r\n");
                return 1;
            }
        }

        int CreateRepository(string[] args)
        {
            if(args.Length == 0 || (args.Length == 1 && args[0].StartsWith("-")))
            {
                Print("*** Repository name is required");
                return 1;
            }

            var isPrivate = false;
            if(args[0].Equals("-p", StringComparison.InvariantCultureIgnoreCase))
            {
                isPrivate = true;
                args = args.Skip(1).ToArray();
            }
            else if(args[0].StartsWith("-"))
            {
                Print($"*** Unknown parameter {args[0]}");
                return 1;
            }

            var repositoryName = args[0];
            var respositoryDescription = args.Skip(1).JoinWithSpaces();

            var result = api.CreateRepository(repositoryName, respositoryDescription, isPrivate);
            Print($"Repository {result.RespositoryFullName} created successfully");

            return 0;
        }

        int DestroyRepository(string[] args)
        {
            if (args.Length == 0)
            {
                Print("*** Repository name is required");
                return 1;
            }

            var repositoryName = args[0];

            Print(
$@"WARNING! WARNING! WARNING!

This action cannot be undone.
This will permanently delete the {Configuration.GithubUser}/{repositoryName} repository, wiki, issues, and comments, and remove all collaborator associations.

Please type in the name of the repository to confirm (or press Enter to cancel): ");

            var typedRespositoryName = Console.ReadLine();
            if(typedRespositoryName != repositoryName)
            {
                Print("Operation cancelled\r\n");
                return 0;
            }

            api.DeleteRepository(repositoryName);

            Print($"Repository {Configuration.GithubUser}/{repositoryName} successfully deleted");
            return 0;
        }

        void Print(string text)
        {
            Console.Write(text);
        }
    }
}

/*
 * This action cannot be undone. This will permanently delete the Konamiman/Test repository, wiki, issues, and comments, and remove all collaborator associations.

Please type in the name of the repository to confirm.

    */