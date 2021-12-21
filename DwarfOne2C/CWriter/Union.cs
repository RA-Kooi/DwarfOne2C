using System;
using System.Collections.Generic;

namespace DwarfOne2C
{
using TagType = Tag.TagType;

public partial class CWriter
{
	private static List<string> GenerateUnion(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag current,
		int depth)
	{
		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		code.Add(string.Format("{0}union {1}", tabs, current.name));
		code.Add(tabs + "{");

		if(current.firstChild == -1)
		{
			code.Add(tabs + "{");
			code.Add(tabs + "};");
			code.Add("");

			return code;
		}

		code.Add(tabs + "{");

		for(Tag child = allTags[IDToIndex[current.firstChild]];
			child.sibling != Tag.NoSibling;
			child = allTags[IDToIndex[child.sibling]])
		{
			if(child.tagType != TagType.Member)
				throw new NotImplementedException("?????");

			(string part1, string part2) = GetType(allTags, IDToIndex, child);

			code.Add(
				string.Format(
					"{0}\t{1}{2}{3}{4}{5};{6}",
					tabs,
					child.isStatic ? "static " : "",
					part1,
					child.name,
					part2,
					child.bitSize > 0 ? ": " + child.bitSize : "",
					!child.isStatic ? " // " + child.comment : ""));
		}

		code.Add(tabs + "};");
		code.Add("");

		return code;
	}
}
}
