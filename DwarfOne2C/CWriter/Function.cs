using System.Collections.Generic;

namespace DwarfOne2C
{
using TagType = Tag.TagType;

public partial class CWriter
{
	private static List<string> GenerateFunction(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag current,
		int depth)
	{
		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		(string part1, string part2) = GetType(allTags, IDToIndex, current);

		// E.g. (With function pointer)
		// static void (*Foo(int))(int, float);
		// (With member function pointer)
		// static int (Foo::*Bar(float, char))(int, int);
		string line = string.Format(
			"{0}{1}{2}{3}(",
			tabs,
			current.isStatic ? "static " : "",
			part1,
			current.name);

		bool firstLocal = true;

		if(current.firstChild >= 0)
		{
			bool hasParams = false;

			int i = 0;

			for(Tag child = allTags[IDToIndex[current.firstChild]];
				child.sibling != Tag.NoSibling;
				child = allTags[IDToIndex[child.sibling]], ++i)
			{
				if(child.name == "this")
					continue;

				string name = child.name != null
					? child.name
					: $"unknown{i}";

				(string pPart1, string pPart2) = GetType(allTags, IDToIndex, child);

				if(child.tagType == TagType.Param)
				{
					hasParams = true;
					line += pPart1 + name + pPart2 + ", ";
				}
				else if(child.tagType == TagType.VariadicParam)
				{
					hasParams = true;
					line += name + ", ";
				}
				else if(child.tagType == TagType.LocalVar)
				{
					if(firstLocal)
					{
						if(hasParams)
							line = line.Remove(line.Length - 2, 2);

						line += ")" + part2;
						code.Add(line);
						code.Add(tabs + "{");
						firstLocal = false;
					}

					line = string.Format(
						"{0}\t{1}{2}{3};",
						tabs,
						pPart1,
						child.name,
						pPart2);

					code.Add(line);
				}
			}

			// We didn't have a local variable;
			if(firstLocal)
			{
				if(hasParams)
					line = line.Remove(line.Length - 2, 2);

				line += ")" + part2;
			}
		}
		else
		{
			line += ")" + part2;
		}

		if(current.references.Count > 0)
		{
			if(firstLocal)
			{
				code.Add(line);
				code.Add("{");
			}

			foreach(int reference in current.references)
			{
				Tag referenced = allTags[IDToIndex[reference]];
				string name = referenced.name != null
					? referenced.name
					: $"unknown_0x{reference:X}";

				line = $"{tabs}\t// References: {name}";

				if(referenced.location != -1)
					line += $" (0x{referenced.location:X})";

				code.Add(line);
			}

			code.Add("}");
		}
		else if(firstLocal)
		{
			line += ';';
			code.Add(line);
		}

		code.Add("");

		return code;
	}

	private static List<string> GenerateMemberFunction(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag current,
		int depth)
	{
		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		Tag referenced = allTags[IDToIndex[current.typeID]];

		// Reforge the tag to generate properly.
		referenced.tagType = TagType.GlobalFunc;
		referenced.name = current.name;

		code.AddRange(
			GenerateFunction(
				allTags,
				IDToIndex,
				referenced,
				depth));

		return code;
	}
}
}
