using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace DwarfOne2C
{
using TagType = Tag.TagType;

public partial class CWriter
{
	private string outputDirectory, splitPath, outputPath;

	private List<string> code;

	public CWriter(string outputDirectory, string splitPath)
	{
		this.outputDirectory = outputDirectory;
		this.splitPath = splitPath;
		code = new();
	}

	public void GenerateCode(CompilationUnit unit)
	{
		if(splitPath.EndsWith('\\')
		   || splitPath.EndsWith('/'))
			splitPath = splitPath.Remove(splitPath.Length - 1);

		outputPath = unit.name.Replace(splitPath, string.Empty);

		if(Path.DirectorySeparatorChar == '/')
			outputPath = outputPath.Replace('\\', '/');

		outputPath = Path.Join(outputDirectory, outputPath);

		List<Tag> allTags = unit.allTags;
		Dictionary<int, int> IDToIndex = unit.IDToIndex;

		Tag current = allTags[IDToIndex[unit.firstChild]];

		List<Tag> memberFuncs = allTags
			.Where(i => i.tagType == TagType.GlobalFunc && i.memberOfID >= 0)
			.ToList();

		Func<Tag, bool> predicate = i =>
		{
			return allTags.Where(j => j.tagType == TagType.TypeDef)
				.Any(j => j.name == i.name);
		};

		// Filled by classes and structs
		List<Tag> staticMembers = allTags
			.Where(i => i.tagType == TagType.GlobalVar)
			.Where(predicate)
			.ToList();

		for(;
			current.sibling != Tag.NoSibling;
			current = allTags[IDToIndex[current.sibling]])
		{
			code.AddRange(
				TagDispatcher(allTags, memberFuncs, IDToIndex, current, 0));

			switch(current.tagType)
			{
			case TagType.GlobalFunc:
			{
				if(current.memberOfID >= 0)
					continue;

				code.AddRange(
					GenerateFunction(allTags, IDToIndex, current, 0));
			} break;
			case TagType.CULocalFunc:
			{
				code.Add("// Local to compilation unit");

				code.AddRange(
					GenerateFunction(allTags, IDToIndex, current, 0));
			} break;
			case TagType.GlobalVar:
			{
				if(staticMembers.Contains(current))
					continue;

				if(current.comment != null)
					code.Add($"// {current.comment}");

				if(current.location != -1)
					code.Add($"// Location: 0x{current.location:X}");

				(string part1, string part2) = GetType(
					allTags,
					IDToIndex,
					current);

				string line = string.Format(
					"{0}{1}{2};",
					part1,
					current.name,
					part2);

				code.Add(line);
				code.Add("");
			} break;
			case TagType.LocalVar:
			{
				code.Add("// Local to compilation unit");

				if(current.comment != null)
					code.Add($"// {current.comment}");

				if(current.location != -1)
					code.Add($"// Location: 0x{current.location:X}");

				(string part1, string part2) = GetType(
					allTags,
					IDToIndex,
					current);

				string line = string.Format(
					"static {0}{1}{2};",
					part1,
					current.name,
					part2);

				code.Add(line);
				code.Add("");
			} break;
			}
		}
	}

	public void WriteCode()
	{
		string allDirs = Path.GetDirectoryName(outputPath);

		Directory.CreateDirectory(allDirs);

		using(StreamWriter file = new(outputPath, false, Encoding.UTF8))
		{
			foreach(string line in code)
				file.WriteLine(line);
		}
	}

	private static List<string> TagDispatcher(
		List<Tag> allTags,
		List<Tag> memberFuncs,
		Dictionary<int, int> IDToIndex,
		Tag current,
		int depth)
	{
		List<string> code = new();

		switch(current.tagType)
		{
		case TagType.Class:
			// Parse class
			code.AddRange(
				GenerateClassStruct(
					true,
					allTags,
					memberFuncs,
					IDToIndex,
					current,
					depth));
		break;
		case TagType.Struct:
			// Parse struct
			code.AddRange(
				GenerateClassStruct(
					false,
					allTags,
					memberFuncs,
					IDToIndex,
					current,
					depth));
		break;
		case TagType.Union:
			// Parse union
			code.AddRange(GenerateUnion(allTags, IDToIndex, current, depth));
		break;
		case TagType.Enum:
			code.AddRange(GenerateEnum(allTags, IDToIndex, current, depth));
		break;
		case TagType.MemberFunc:
		{
			code.AddRange(
				GenerateMemberFunction(allTags, IDToIndex, current, depth));
		} break;
		}

		return code;
	}

	private static List<string> GenerateEnum(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag current,
		int depth)
	{
		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		code.Add(tabs + "enum " + current.name);
		code.Add(tabs + "{");

		foreach(string element in current.elements)
		{
			code.Add(tabs + "\t" + element);
		}
		string last = code[code.Count - 1];
		code[code.Count - 1] = last.Remove(last.Length - 1);

		code.Add(tabs + "};");
		code.Add("");

		return code;
	}
}
}
