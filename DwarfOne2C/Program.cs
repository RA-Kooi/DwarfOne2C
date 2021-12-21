using System;
using System.IO;

namespace DwarfOne2C
{
class Program
{
	static void Usage()
	{
		Console.WriteLine(
			"DwarfOne2C --list-files <dwarf dump>");

		Console.WriteLine(
			"DwarfOne2C <dwarf dump> <path strip> <CU path> " +
			"<(optional) output directory>");

		Console.WriteLine("DwarfOne2C --help");
	}

	static void Help()
	{
		Console.WriteLine(
			"Parses the output of DWARFone by LuigiBlood and " +
			"outputs a set of header files with structs and " +
			"function prototypes.");

		Console.WriteLine(
			"Find DWARFone here: https://github.com/LuigiBlood/dwarfone");

		Console.WriteLine(
			"Args:\n" +
			"\t<dwarf dump>: Path to the output file of DWARFone\n" +
			"\t<path strip>: Part to strip of the file paths; " +
			"E.g. C:\\GameCube\\killer7eu\n" +
			"\t\tCauses C:\\GameCube\\killer7eu\\Src\\Wat\\main.cpp to become " +
			"<output dir>\\Src\\Wat\\main.cpp\n" +
			"\t<CU path>: Full path of the compilation unit (See --list-files)\n" +
			"\t<output directory>: Optional directory to dump output in. " +
			"Current directory if not supplied.");
	}

	static int Main(string[] args)
	{
		if(args.Length < 1
		   && (args.Length < 2 && args[0].ToLower() == "--list-files")
		   && args.Length < 3
		   && args.Length > 4)
		{
			Usage();
			return 1;
		}

		if(args[0].ToLower() == "--help")
		{
			Help();
			return 1;
		}

		string fileName = args[0];
		string fullPath = Path.GetFullPath(fileName);

		if(args[0].ToLower() == "--list-files")
		{
			ListFiles(fullPath);
			return 0;
		}

		string outputDirectory = null;
		if(args.Length > 3)
			outputDirectory = Path.GetFullPath(args[3]);
		else
			outputDirectory = Path.GetDirectoryName(fullPath);

		try
		{
			FileAttributes attrs = File.GetAttributes(outputDirectory);
			if(!attrs.HasFlag(FileAttributes.Directory))
			{
				Console.WriteLine("Error: Output directory is not a directory");
				return -1;
			}
		}
		catch(DirectoryNotFoundException)
		{
			try
			{
				Directory.CreateDirectory(outputDirectory);
			}
			catch(SystemException se)
			{
				Console.WriteLine("Error creating directory: " + se.ToString());
				return 1;
			}
		}
		catch(SystemException e)
		{
			Console.WriteLine(
				"Error on trying to access output directory: " + e.ToString());

			return 1;
		}

		string splitPath = args[1];
		string cuPath = args[2];

		DumpParser dumpParser = new DumpParser(fullPath);

		CompilationUnit unit = dumpParser.Parse(cuPath);

		CWriter writer = new CWriter(outputDirectory, splitPath);
		writer.GenerateCode(unit);
		writer.WriteCode();

		return 0;
	}

	static void ListFiles(string fullPath)
	{
		DumpParser dumpParser = new DumpParser(fullPath);
		dumpParser.ListCompilationUnits();
	}
}
}
