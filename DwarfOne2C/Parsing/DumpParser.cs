using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DwarfOne2C
{
class DumpParser
{
	private string[] lines;
	private int start, current;

	public List<Tag> allTags = new();
	public Dictionary<int /* ID */, int /* tagIndex */> IDToIndex = new();

	public DumpParser(string fileName)
	{
		lines = File.ReadAllLines(fileName, Encoding.UTF8);

		for(int i = 0; i < Math.Min(100, lines.Length); ++i)
		{
			if(lines[i].StartsWith("DWARF v1 dump -"))
			{
				start = i + 5;
				break;
			}
		}
	}

	public void ListCompilationUnits()
	{
		for(current = start; current < lines.Length; ++current)
		{
			if(lines[current].EndsWith("TAG_compile_unit"))
			{
				for(;
					current < lines.Length && lines[current] != string.Empty;
					++current)
				{
					string line = lines[current].TrimStart();

					if(line.StartsWith("AT_name"))
					{
						string unitName = line.Substring(9, line.Length - 11);
						Console.WriteLine(unitName);
					}
				}
			}
		}
	}

	public HashSet<CompilationUnit> Parse()
	{
		// Parse global variables
		//	if var has AT_lo_user -> static class/struct variable (C++)
		//	else append to globals list

		HashSet<CompilationUnit> units = new(100);

		for(current = start; current < lines.Length; ++current)
		{
			if(lines[current].EndsWith("TAG_compile_unit"))
			{
				CompilationUnit unit = new(
					allTags,
					IDToIndex,
					lines,
					ref current);

				Console.Error.WriteLine(unit.name);
				Console.Error.Flush();

				unit.Parse(lines, current);

				units.Add(unit);
			}
		}

		return units;
	}
}
}
