using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string unlinkCommandLine = "ngh unlink [<local directory>]";

        static readonly string unlinkCommandExplanation =
@"Unlinks the local directory from the remote repository.
Deletes the application data but otherwise the directory contents are kept untouched.";

        void UnlinkCommand(string[] args)
        {
            #pragma warning restore 414

            var directory = new FilesystemDirectory(args.ElementAtOrDefault(0));
            var localRepository = GetExistingLocalRepository(directory);

            localRepository.Unlink();

            Print($"{directory.PhysicalPath} has been unlinked from remote repository {localRepository.FullRepositoryName}");
        }
    }
}
