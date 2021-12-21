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

		if(current.firstChild >= 0)
		{
			bool firstLocal = true;
			bool hasParams = false;

			for(Tag child = allTags[IDToIndex[current.firstChild]];
				child.sibling != Tag.NoSibling;
				child = allTags[IDToIndex[child.sibling]])
			{
				if(child.name == "this")
					continue;

				(part1, part2) = GetType(allTags, IDToIndex, child);

				if(child.tagType == TagType.Param)
				{
					hasParams = true;
					line += part1 + child.name + part2 + ", ";
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
						part1,
						child.name,
						part2);

					code.Add(line);
				}
			}

			// We had a local variable;
			if(!firstLocal)
			{
				code.Add(tabs + "}");
			}
			else
			{
				if(hasParams)
					line = line.Remove(line.Length - 2, 2);

				line += ")" + part2 + ";";
				code.Add(line);
			}
		}
		else
		{
			line += ")" + part2 + ";";
			code.Add(line);
		}

		code.Add("");

		return code;
	}
}
}
