using System;
using System.IO;

namespace DwarfOne2C
{
class DumpParser
{
	private string[] lines;
	private int start, current;

	public DumpParser(string fileName)
	{
		try
		{
			lines = File.ReadAllLines(fileName, System.Text.Encoding.UTF8);
		}
		catch(Exception e)
		{
			Console.Error.WriteLine("Error opening dump: " + e.ToString());
			throw;
		}

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
				for(; current < lines.Length && lines[current] != string.Empty; ++current)
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

	public CompilationUnit Parse(string compilationUnit)
	{
		// Parse global variables
		//	if var has AT_lo_user -> static class/struct variable (C++)
		//	else append to globals list

		for(current = start; current < lines.Length; ++current)
		{
			if(lines[current].EndsWith("TAG_compile_unit"))
			{
				int cuStart = current;

				for(; current < lines.Length; ++current)
				{
					string line = lines[current].TrimStart();
					if(line.StartsWith("AT_name"))
					{
						string name = line.Substring(9, line.Length - 11);
						if(name.ToLower() == compilationUnit.ToLower())
						{
							current = cuStart;
							return ParseCompilationUnit();
						}
					}
					else if(line == string.Empty)
						break;
				}
			}
		}

		return null;
	}

	private CompilationUnit ParseCompilationUnit()
	{
		CompilationUnit unit = new();

		unit.ID = Convert.ToInt32(
			lines[current++].Split(
				':',
				StringSplitOptions.RemoveEmptyEntries)[0],
			16);

		string sibling = lines[current++].TrimStart();
		unit.sibling = Convert.ToInt32(
			sibling.Substring(11, sibling.Length - 12),
			16);

		for(; current < lines.Length; ++current)
		{
			if(lines[current] == string.Empty)
				break;

			string line = lines[current].TrimStart();

			if(line.StartsWith("AT_name"))
			{
				unit.name = line.Substring(9, line.Length - 11);
			}
			else if(line.StartsWith("AT_language"))
			{
				string language = line.Substring(12, line.Length - 13);
				if(language == "LANG_C_PLUS_PLUS")
					unit.language = CompilationUnit.Language.Cpp;
				else if(language.StartsWith("LANG_C"))
					unit.language = CompilationUnit.Language.C;
				else
					throw new NotImplementedException(
						"Unimplemented language tag.");
			}
		}

		Console.Error.WriteLine(unit.name);
		Console.Error.Flush();

		unit.FirstPass(lines, current);
		unit.SecondPass();

		return unit;
	}
}
}
