using System;
using System.Collections.Generic;

namespace DwarfOne2C
{
public partial class CWriter
{
	private static List<string> GenerateUnion(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node unionNode,
		int depth)
	{
		Tag current = unionNode.tag;

		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		code.Add(string.Format("{0}union {1}", tabs, current.name));
		code.Add(tabs + "{");

		if(unionNode.children.Count == 0)
		{
			code.Add(tabs + "};");
			code.Add("");

			return code;
		}

		foreach(Node childNode in unionNode.children)
		{
			Tag child = childNode.tag;

			if(child.tagType != TagType.Member)
				throw new NotImplementedException("?????");

			(string part1, string part2) = GetType(
				allTags,
				IDToIndex,
				CU,
				allCUs,
				childNode);

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
