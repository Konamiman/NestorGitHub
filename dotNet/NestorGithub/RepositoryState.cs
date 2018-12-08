using System.Linq;

namespace Konamiman.NestorGithub
{
    class RepositoryState
    {
        public bool HasChanges => AddedFiles.Any() || ModifiedFiles.Any() || DeletedFiles.Any();

        public string[] AllChangedFiles => AddedFiles.Union(ModifiedFiles).Union(DeletedFiles).ToArray();

        public string[] AddedFiles { get; set; }
        public string[] ModifiedFiles { get; set; }
        public string[] DeletedFiles { get; set; }
        public string[] UnchangedFiles { get; set; }
    }
}
