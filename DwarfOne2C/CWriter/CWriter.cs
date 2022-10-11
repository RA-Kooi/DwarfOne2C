using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace DwarfOne2C
{
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

	public void GenerateCode(
		CompilationUnit unit,
		List<CompilationUnit> allUnits,
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex)
	{
		if(splitPath.EndsWith('\\')
		   || splitPath.EndsWith('/'))
			splitPath = splitPath.Remove(splitPath.Length - 1);

		outputPath = unit.root.tag.name.Replace(splitPath, string.Empty);

		if(Path.DirectorySeparatorChar == '/')
			outputPath = outputPath.Replace('\\', '/');

		outputPath = Path.Join(outputDirectory, outputPath);

		List<Node> allCUs = allUnits.Select(CU => CU.root).ToList();

		List<Node> memberFuncs = unit.root.children
			.Where(
				i =>
				{
					return i.tag.tagType == TagType.GlobalFunc
						&& i.tag.memberOfID >= 0;
				})
			.ToList();

		Func<Node, bool> predicate = i =>
		{
			return unit.root.children
				.Where(j => j.tag.tagType == TagType.TypeDef)
				.Any(j => j.tag.name == i.tag.name);
		};

		// Filled by classes and structs
		List<Node> staticMembers = unit.root.children
			.Where(i => i.tag.tagType == TagType.GlobalVar)
			.Where(predicate)
			.ToList();

		foreach(Node child in unit.root.children)
		{
			Tag current = child.tag;

			code.AddRange(
				TagDispatcher(
					allTags,
					memberFuncs,
					IDToIndex,
					unit.root,
					allCUs,
					child,
					0));

			switch(current.tagType)
			{
			case TagType.GlobalFunc:
			{
				if(current.memberOfID >= 0)
					continue;

				code.AddRange(
					GenerateFunction(
						allTags,
						IDToIndex,
						unit.root,
						allCUs,
						child,
						0));
			} break;
			case TagType.CULocalFunc:
			{
				code.Add("// Local to compilation unit");

				code.AddRange(
					GenerateFunction(
						allTags,
						IDToIndex,
						unit.root,
						allCUs,
						child,
						0));
			} break;
			case TagType.GlobalVar:
			{
				if(staticMembers.Contains(child))
					continue;

				if(current.comment != null)
					code.Add($"// {current.comment}");

				if(current.location != -1)
					code.Add($"// Location: 0x{current.location:X}");

				(string part1, string part2) = GetType(
					allTags,
					IDToIndex,
					unit.root,
					allCUs,
					child);

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
					unit.root,
					allCUs,
					child);

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

	public void InsertFileDelimiter()
	{
		code.Add(
			"// ------------------------------------------------------------" +
			"--------------------");
		code.Add("");
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
		List<Node> memberFuncs,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node current,
		int depth)
	{
		List<string> code = new();

		switch(current.tag.tagType)
		{
		case TagType.Class:
		{
			code.AddRange(
				GenerateClassStruct(
					true,
					allTags,
					memberFuncs,
					IDToIndex,
					CU,
					allCUs,
					current,
					depth));
		} break;
		case TagType.Struct:
		{
			code.AddRange(
				GenerateClassStruct(
					false,
					allTags,
					memberFuncs,
					IDToIndex,
					CU,
					allCUs,
					current,
					depth));
		} break;
		case TagType.Union:
		{
			code.AddRange(
				GenerateUnion(allTags, IDToIndex, CU, allCUs, current, depth));
		} break;
		case TagType.Enum:
			code.AddRange(GenerateEnum(allTags, IDToIndex, current.tag, depth));
		break;
		case TagType.MemberFunc:
		{
			code.AddRange(
				GenerateMemberFunction(
					allTags,
					IDToIndex,
					CU,
					allCUs,
					current,
					depth));
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
