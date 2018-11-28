using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        void UnlinkRepository(string[] args)
        {
            var directory = new FilesystemDirectory(args.ElementAtOrDefault(0));
            var localRepository = GetExistingLocalRepository(directory);

            localRepository.Unlink();

            Print($"{directory.PhysicalPath} has been unlinked from remote repository {localRepository.FullRepositoryName}");
        }
    }
}
