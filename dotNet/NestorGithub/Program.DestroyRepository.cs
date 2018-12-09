using System;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string destroyCommandLine = "ngh destroy [<owner>/]<repository name>";

        static readonly string destroyCommandExplanation =
@"Destroys a repository in your GitHub account.
Be careful, this can't be undone!";

        void DestroyCommand(string[] args)
        {
            #pragma warning restore 414

            if (args.Length == 0)
                throw BadParameter("Repository name is required");

            var repositoryName = FullRepositoryName(args[0]);

            UI.Print(
$@"WARNING! WARNING! WARNING!

This action cannot be undone.
This will permanently delete the {repositoryName} repository, wiki, issues, and comments, and remove all collaborator associations.

Please type in the full name of the repository to confirm (or press Enter to cancel): ");

            var typedRespositoryName = Console.ReadLine();
            if (!typedRespositoryName.Equals(repositoryName, StringComparison.InvariantCultureIgnoreCase))
            {
                UI.Print("Operation cancelled\r\n");
                return;
            }

            var api = GetApi(repositoryName);
            api.DeleteRepository();

            UI.Print($"Repository {repositoryName} successfully deleted");
        }
    }
}
