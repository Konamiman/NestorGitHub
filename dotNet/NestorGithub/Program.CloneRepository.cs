using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        void CloneRepository(string[] args, bool linkOnly)
        {
            if (args.Length == 0)
                throw BadParameter("Repository name is required");

            var repositoryName = FullRepositoryName(args[0]);
            var directory = new FilesystemDirectory(args.ElementAtOrDefault(1));
            var api = GetApi(repositoryName);
            var localRepository = new LocalRepository(directory, api, allowNonLinkedDirectory: true);

            if (linkOnly)
            {
                localRepository.Link(repositoryName);
                Print($"{directory.PhysicalPath} has been linked to {repositoryName} on branch {localRepository.BranchName}");
            }
            else
            {
                localRepository.Clone(repositoryName);
                Print($"\r\nRepository {repositoryName} has been cloned at {directory.PhysicalPath}");
            }
        }

    }
}
