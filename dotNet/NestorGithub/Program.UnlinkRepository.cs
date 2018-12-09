using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string unlinkCommandLine = "ngh unlink";

        static readonly string unlinkCommandExplanation =
@"Unlinks the current directory from the remote repository.
Deletes the application data but otherwise the directory contents are kept untouched.";

        void UnlinkCommand(string[] args)
        {
            #pragma warning restore 414

            var localRepository = GetExistingLocalRepository();

            localRepository.Unlink();

            UI.Print($"{localRepository.LocalPath} has been unlinked from remote repository {localRepository.FullRepositoryName}");
        }
    }
}
