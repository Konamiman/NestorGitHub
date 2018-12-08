using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string statusCommandLine = "ngh status [<local directory>]";

        static readonly string statusCommandExplanation = 
@"Shows a list of changes (added, modified and deleted files) in the local repository.";

        void StatusCommand(string[] args)
        {
            #pragma warning restore 414

            var directory = new FilesystemDirectory(args.ElementAtOrDefault(0));
            var localRepository = GetExistingLocalRepository(directory);

            PrintLine($"Repository: {localRepository.FullRepositoryName}");
            PrintLine($"Branch: {localRepository.BranchName}");
            PrintLine("");

            if (!localRepository.ExistsRemotely())
            {
                PrintLine("*** The remote repository doesn't exist!");
            }
            else
            {
                try
                {
                    if(!localRepository.BranchExistsRemotely())
                        PrintLine("Branch doesn't exist remotely, it will be created on commit.");
                    else if (localRepository.IsUpToDateWithRemote())
                        PrintLine("Your local repository is up to date with the remote repository.");
                    else
                        PrintLine("*** Your local repository is not up to date with the remote repository. You need to pull before you can commit.");
                }
                catch (ApiException ex)
                {
                    PrintLine($"*** When checking remote repository status: {ex.Message}\r\n{ex.PrintableErrorsList}");
                }
                catch (Exception ex)
                {
                    PrintLine($"*** When checking remote repository status: ({ex.GetType().Name}) {ex.Message}");
                }
            }
            PrintLine("");

            var changes = localRepository.GetLocalState();

            if(!changes.HasChanges)
            {
                PrintLine("No changes in the local repository.");
                return;
            }

            if (changes.AddedFiles.Length > 0)
            {
                PrintLine("Added files:");
                foreach (var file in changes.AddedFiles)
                    PrintLine($"  {file}");

                PrintLine("");
            }

            if (changes.ModifiedFiles.Length > 0)
            {
                PrintLine("Modified files:");
                foreach (var file in changes.ModifiedFiles)
                    PrintLine($"  {file}");

                PrintLine("");
            }

            if (changes.DeletedFiles.Length > 0)
            {
                PrintLine("Deleted files:");
                foreach (var file in changes.DeletedFiles)
                    PrintLine($"  {file}");

                PrintLine("");
            }
        }
    }
}
