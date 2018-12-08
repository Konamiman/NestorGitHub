using System.Reflection;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string helpCommandLine = "ngh help <command>";

        static readonly string helpCommandExplanation = "Get detailed help about a command.";

        void HelpCommand(string[] args)
        {
            #pragma warning restore 414

            if (args.Length == 0)
                throw BadParameter("Please specify a command to display help for");

            var commandName = args[0];

            var commandLineField = this.GetType().GetField($"{commandName}CommandLine", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase);
            if(commandLineField == null)
                throw BadParameter($"Unknown command '{commandName}'");

            var commandLine = commandLineField.GetValue(null);

            var explanation = this.GetType().GetField($"{commandName}CommandExplanation", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase)
                .GetValue(null);

            Print($"{commandLine}\r\n\r\n{explanation}\r\n\r\n");
        }
    }
}
