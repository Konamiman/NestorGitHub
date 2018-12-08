using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.NestorGithub
{
    class RepositoryFileReference
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public string BlobSha { get; set; }

        public override string ToString()
        {
            return $"{Path} - {BlobSha}";
        }
    }
}
