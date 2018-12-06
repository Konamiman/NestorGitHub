using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.NestorGithub
{
    class LocalRepositoryChanges
    {
        public bool HasChanges => AddedFiles.Any() || ModifiedFiles.Any() || DeletedFiles.Any();

        public string[] AllChangedFiles => AddedFiles.Union(ModifiedFiles).Union(DeletedFiles).ToArray();

        public string[] AddedFiles { get; set; }
        public string[] ModifiedFiles { get; set; }
        public string[] DeletedFiles { get; set; }
    }
}
