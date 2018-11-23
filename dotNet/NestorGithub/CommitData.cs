using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.NestorGithub
{
    class CommitData
    {
        public string Sha { get; set; }
        public string TreeSha { get; set; }
        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
        public DateTime Date { get; set; }
    }
}
