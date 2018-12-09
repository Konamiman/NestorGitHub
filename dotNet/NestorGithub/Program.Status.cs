using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string statusCommandLine = "ngh status";

        static readonly string statusCommandExplanation = 
@"Shows a list of changes (added, modified and deleted files) in the local repository.";

        void StatusCommand(string[] args)
        {
            #pragma warning restore 414

            var localRepository = GetExistingLocalRepository();

            UI.PrintLine($"Repository: {localRepository.FullRepositoryName}");
            UI.PrintLine($"Branch: {localRepository.LocalBranchName}");
            UI.PrintLine("");

            if (!localRepository.ExistsRemotely())
            {
                UI.PrintLine("*** The remote repository doesn't exist!");
            }
            else
            {
                try
                {
                    if(!localRepository.BranchExistsRemotely())
                        UI.PrintLine("Branch doesn't exist remotely, it will be created on commit.");
                    else if (localRepository.IsUpToDateWithRemote())
                        UI.PrintLine("Your local repository is up to date with the remote repository.");
                    else
                        UI.PrintLine("*** Your local repository is not up to date with the remote repository. You need to pull before you can commit.");
                }
                catch (ApiException ex)
                {
                    UI.PrintLine($"*** When checking remote repository status: {ex.Message}\r\n{ex.PrintableErrorsList}");
                }
                catch (Exception ex)
                {
                    UI.PrintLine($"*** When checking remote repository status: ({ex.GetType().Name}) {ex.Message}");
                }
            }
            UI.PrintLine("");

            var changes = localRepository.GetLocalState();

            if(!changes.HasChanges)
            {
                UI.PrintLine("No changes in the local repository.");
                return;
            }

            if (changes.AddedFiles.Length > 0)
            {
                UI.PrintLine("Added files:");
                foreach (var file in changes.AddedFiles)
                    UI.PrintLine($"  {file}");

                UI.PrintLine("");
            }

            if (changes.ModifiedFiles.Length > 0)
            {
                UI.PrintLine("Modified files:");
                foreach (var file in changes.ModifiedFiles)
                    UI.PrintLine($"  {file}");

                UI.PrintLine("");
            }

            if (changes.DeletedFiles.Length > 0)
            {
                UI.PrintLine("Deleted files:");
                foreach (var file in changes.DeletedFiles)
                    UI.PrintLine($"  {file}");

                UI.PrintLine("");
            }
        }
    }
}
