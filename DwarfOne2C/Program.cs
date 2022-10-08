using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;

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

		Command dumpCommand = new(
			"dump",
			"Dump one or more files from the symbol dump.");

		Argument<FileInfo?> listFile = new(
			"LISTFILE",
			"A text file with a newline separated list of the files to dump.");

		listFile.Arity = ArgumentArity.ExactlyOne;

		Argument<FileInfo?> stripList = new(
			"STRIPLIST",
			"A text file with a newline separated list of the paths to " +
			"strip.\nEach path only has to appear once. A strip path is part" +
			"of the path you want removed from the file name you want to " +
			"dump.\nE.g. \"C:\\GameCube\\killer7eu\" causes " +
			"\"C:\\GameCube\\killer7eu\\Src\\Wat\\main.cpp\" to become " +
			"<output directory>\\Src\\Wat\\main.cpp");

		stripList.Arity = ArgumentArity.ExactlyOne;

		Option<DirectoryInfo> outOption = new(
			aliases: new []{"--out", "-o"},
			description: "Ouput directory",
			getDefaultValue: () =>
			{
				return new DirectoryInfo(Directory.GetCurrentDirectory());
			});

		dumpCommand.AddArgument(dumpFile);
		dumpCommand.AddArgument(listFile);
		dumpCommand.AddArgument(stripList);

		dumpCommand.AddOption(outOption);

		dumpCommand.SetHandler(
			(dumpFile, listFile, stripFile, outOption) =>
			{
				DumpFiles(dumpFile!, listFile!, stripFile!, outOption);
			},
			dumpFile, listFile, stripList, outOption);

		rootCommand.AddCommand(listCommand);
		rootCommand.AddCommand(dumpCommand);

		return rootCommand.Invoke(args);
	}

	static void ListFiles(FileInfo file)
	{
		DumpParser dumpParser = new(file.FullName);
		dumpParser.ListCompilationUnits();
	}

	static void DumpFiles(
		FileInfo dumpFile,
		FileInfo listFile,
		FileInfo stripFile,
		DirectoryInfo outputDir)
	{
		try
		{
			if(!outputDir.Exists)
				outputDir.Create();

			string[] fileList = File.ReadAllLines(
				listFile.FullName,
				Encoding.UTF8);

			string[] stripList = File.ReadAllLines(
				stripFile.FullName,
				Encoding.UTF8);

			DumpParser parser = new(dumpFile.FullName);

			List<RootTag> units = parser.Parse();

			Dictionary<string, List<RootTag>> sharedNames = new();

			foreach(RootTag unit in units)
			{
				sharedNames.TryAdd(unit.name, new());
				List<RootTag> sharers = sharedNames[unit.name];

				sharers.AddRange(units.Where(c => c.name == unit.name));
			}

			sharedNames
				.Where(n => fileList.Where(f => f == n.Key).Any())
				.All(
					unit =>
					{
						IEnumerable<string> stripper = stripList
							.Where(s => unit.Key.Contains(s));

						if(!stripper.Any())
							return true;

						CWriter writer = new(outputDir.FullName, stripper.First());

						bool insertDelimiter = unit.Value.Count > 1;
						unit.Value.ForEach(
							(cu) =>
							{
								writer.GenerateCode(
									cu,
									parser.allTags,
									parser.IDToIndex);

								if(insertDelimiter)
									writer.InsertFileDelimiter();
							});

						writer.WriteCode();

						return true;
					});
		}
		catch(Exception e)
		{
			Console.Error.WriteLine($"Error: {e.ToString()}");
		}
	}
}
}
