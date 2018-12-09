using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
#pragma warning disable 414

        static readonly string branchCommandLine =
@"ngh branch [-s|-sa|-sr|-sl] <branch name>
ngh branch -n <branch name> [<base branch>]
ngh branch -d <branch name>";

        static readonly string branchCommandExplanation =
@"<branch name> only: same as -s
-s, -sa: Switch to the specified remote branch, ask what to do on conflicts
-sr: Switch to the specified remote branch, download remote version on conflict
-sl: Switch to the specified remote branch, keep local version on conflict
-n: Create a new remote branch, pointing to the current commit
    or to the commit pointed by <base branch> if specified
-d: Delete a remote branch";

        void BranchCommand(string[] args)
        {
#pragma warning restore 414

            if (args.Length == 0)
                throw BadParameter("Branch name is required");
            else if (args.Length == 1)
                SwitchToBranch(args[0], PullConflictStrategy.Ask);
            else if (IsFlagParam(args[0], "n"))
                CreateRemoteBranch(args[1], args.ElementAtOrDefault(2));
            else if (IsFlagParam(args[0], "d"))
                DestroyRemoteBranch(args[1]);
            else if (IsFlagParam(args[0], "s") || IsFlagParam(args[0], "sa"))
                SwitchToBranch(args[1], PullConflictStrategy.Ask);
            else if (IsFlagParam(args[0], "sr"))
                SwitchToBranch(args[1], PullConflictStrategy.OverWriteWithRemote);
            else if (IsFlagParam(args[0], "sl"))
                SwitchToBranch(args[1], PullConflictStrategy.KeepLocal);
            else
                throw BadParameter($"Unknwon parameter '{args[0]}'");
        }

        private void DestroyRemoteBranch(string branchName)
        {
            var repository = GetExistingLocalRepository();
            var destroyed = repository.DeleteRemoteBranch(branchName);
            if(destroyed)
                UI.PrintLine($"Branch '{branchName}' has been deleted from the remote repository.");
        }

        private void CreateRemoteBranch(string branchName, string baseBranchName)
        {
            var repository = GetExistingLocalRepository();
            repository.CreateRemoteBranch(branchName, baseBranchName, Configuration.AuthorName, Configuration.AuthorEmail);
            UI.PrintLine($"Branch '{branchName}' has been created, you can switch to it locally with 'ngh branch {branchName}'");
        }

        private void SwitchToBranch(string branchName, PullConflictStrategy conflictStrategy)
        {
            if (branchName.StartsWith("-"))
                throw BadParameter($"Unknown parameter '{branchName}'");

            var repository = GetExistingLocalRepository();
            repository.SwitchToBranch(branchName, conflictStrategy);

            UI.PrintLine($"Current local branch is now '{branchName}'");
        }
    }
}
