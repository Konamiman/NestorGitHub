using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
#pragma warning disable 414

        static readonly string mergeCommandLine = "ngh merge <source branch> <base branch> [<commit message>]";

        static readonly string mergeCommandExplanation =
@"Merges the specififed source branch into the specified base branch.
The merge is done in the remote repository, nothing is done locally.";

        void MergeCommand(string[] args)
        {
#pragma warning restore 414

            if (args.Length < 2)
                throw BadParameter("Source and base branches are required");

            if (args.Length > 3)
                throw BadParameter("Please specify the commit message in quotes if it contains spaces");

            var localRepository = GetExistingLocalRepository();
            localRepository.Merge(args[0], args[1], args.ElementAtOrDefault(2));

            UI.PrintLine($"'{args[0]}' has been merged into '{args[1]}' remotely.");
            if (localRepository.LocalBranchName == args[1])
                UI.PrintLine($"'{args[1]}' is your current local branch, please do 'ngh pull' to update your local repository.");
        }
    }
}
