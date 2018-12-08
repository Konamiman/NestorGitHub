using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string newCommandLine = "ngh new [-p] <repository name> [<repository description>]";

        static readonly string newCommandExplanation =
@"Creates a new repository in your GitHub account.
-p creates a private repository, you need a paid GitHub acount for that.";

        void NewCommand(string[] args)
        {
            #pragma warning restore 414

            if (args.Length == 0 || (args.Length == 1 && args[0].StartsWith("-")))
                throw BadParameter("Repository name is required");

            var isPrivate = false;
            if (args[0].Equals("-p", StringComparison.InvariantCultureIgnoreCase))
            {
                isPrivate = true;
                args = args.Skip(1).ToArray();
            }
            else if (args[0].StartsWith("-"))
            {
                throw BadParameter($"Unknown parameter {args[0]}");
            }

            var repositoryName = args[0];
            if (IsFullRepositoryName(repositoryName))
                throw BadParameter("Please specify a repository name without username (you can create repositories in your own account only)");
            else if(user == "")
                throw BadParameter("Please specify username and password or token in the configuration file");

            var respositoryDescription = args.Skip(1).JoinWithSpaces();

            var api = GetApi(FullRepositoryName(repositoryName));
            var result = api.CreateRepository(respositoryDescription, isPrivate);

            Print($"Repository {result.RespositoryFullName} created successfully");
        }
    }
}
