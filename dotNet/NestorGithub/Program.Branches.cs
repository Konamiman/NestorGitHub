using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string branchesCommandLine = "ngh branches";

        static readonly string branchesCommandExplanation = "List the branches that exist in the remote repository.";

        void BranchesCommand(string[] args)
        {
            #pragma warning restore 414

            var localRepository = GetExistingLocalRepository();
            var branchNames = localRepository.Api.GetBranches();

            if(branchNames.Length == 0)
            {
                UI.PrintLine($"No branches exist in repository {localRepository.FullRepositoryName}");
                return;
            }

            UI.PrintLine($"The following branches exist in repository {localRepository.FullRepositoryName}:\r\n");
            UI.Print(branchNames.OrderBy(x => x).ToArray().JoinInLines());

        }
    }
}
