using System;
using System.IO;
using System.Collections.Generic;

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
		List<string> arguments = new(args);

		if(arguments.Count > 0 && arguments[0].ToLower().Contains("dwarfone2c"))
			arguments.RemoveAt(0);

		if(arguments.Count < 1)
		{
			Usage();
			return 1;
		}
		else if(arguments.Count < 2 && arguments[0].ToLower() == "--list-files")
		{
			Usage();
			return 1;
		}
		else if((arguments.Count < 3 || arguments.Count > 4)
				&& !(arguments[0].ToLower() == "--help")
				&& !(arguments[0].ToLower() == "--list-files"))
		{
			Usage();
			return 1;
		}

		if(arguments[0].ToLower() == "--help")
		{
			Help();
			return 1;
		}

		string fileName = null;
		string fullPath = null;

		if(arguments[0].ToLower() == "--list-files")
		{
			fileName = arguments[1];
			fullPath = Path.GetFullPath(fileName);

			ListFiles(fullPath);
			return 0;
		}

		fileName = arguments[0];
		fullPath = Path.GetFullPath(fileName);

		string outputDirectory = null;
		if(arguments.Count > 3)
			outputDirectory = Path.GetFullPath(arguments[3]);
		else
			outputDirectory = Path.GetDirectoryName(fullPath);

		try
		{
			FileAttributes attrs = File.GetAttributes(outputDirectory);
			if(!attrs.HasFlag(FileAttributes.Directory))
			{
				Console.Error.WriteLine("Error: Output directory is not a directory");
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
				Console.Error.WriteLine("Error creating directory: " + se.ToString());
				return 1;
			}
		}
		catch(SystemException e)
		{
			Console.Error.WriteLine(
				"Error on trying to access output directory: " + e.ToString());

			return 1;
		}

		string splitPath = arguments[1];
		string cuPath = arguments[2];

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
