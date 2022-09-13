using System.CommandLine;
using System.IO;

#nullable enable

namespace DwarfOne2C
{
class Program
{
	static int Main(string[] args)
	{
		RootCommand rootCommand = new(
			"Parses the output of DWARFone by LuigiBlood and " +
			"outputs a set of header files with structs and " +
			"function prototypes.\n" +
			"Find DWARFone here: https://github.com/LuigiBlood/dwarfone");

		Argument<FileInfo?> dumpFile = new("DUMP", "The debug symbol dump.");
		dumpFile.Arity = ArgumentArity.ExactlyOne;

		Command listCommand = new("list", "List the files in the dump.");
		listCommand.AddArgument(dumpFile);

		listCommand.SetHandler(
			(file) =>
			{
				ListFiles(file!);
			},
			dumpFile);

		rootCommand.AddCommand(listCommand);

		return rootCommand.Invoke(args);
	}

	static void ListFiles(FileInfo file)
	{
		DumpParser dumpParser = new(file.FullName);
		dumpParser.ListCompilationUnits();
	}
}
}
