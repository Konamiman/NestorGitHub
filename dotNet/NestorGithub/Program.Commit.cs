using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        void Commit(string[] args)
        {
            string directoryPath = null;
            if(args.FirstOrDefault().Equals("-d", StringComparison.InvariantCultureIgnoreCase))
            {
                directoryPath = args[1];
                args = args.Skip(2).ToArray();
            }

            if (args.Length > 1)
                throw BadParameter("Please specify the commit message in quotes if it contains spaces");

            var commitMessage = args[0];
            var directory = new FilesystemDirectory(directoryPath);
            var localRepository = GetExistingLocalRepository(directory);

            localRepository.Commit(Configuration.AuthorName, Configuration.AuthorEmail, commitMessage);

            Print("Commit completed successfully.");
        }
    }
}
