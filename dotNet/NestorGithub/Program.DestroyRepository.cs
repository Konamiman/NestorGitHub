using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        void DestroyRepository(string[] args)
        {
            if (args.Length == 0)
                throw BadParameter("Repository name is required");

            var repositoryName = FullRepositoryName(args[0]);

            Print(
$@"WARNING! WARNING! WARNING!

This action cannot be undone.
This will permanently delete the {repositoryName} repository, wiki, issues, and comments, and remove all collaborator associations.

Please type in the full name of the repository to confirm (or press Enter to cancel): ");

            var typedRespositoryName = Console.ReadLine();
            if (!typedRespositoryName.Equals(repositoryName, StringComparison.InvariantCultureIgnoreCase))
            {
                Print("Operation cancelled\r\n");
                return;
            }

            var api = GetApi(repositoryName);
            api.DeleteRepository();

            Print($"Repository {repositoryName} successfully deleted");
        }
    }
}
