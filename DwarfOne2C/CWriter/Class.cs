using System.Linq;
using System.Collections.Generic;

namespace DwarfOne2C
{
public partial class CWriter
{
	private static List<string> GenerateClassStruct(
		bool isClass,
		List<Tag> allTags,
		List<Node> allMemberFuncs,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node classNode,
		int depth)
	{
		Tag current = classNode.tag;

		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		string line = string.Format(
			"{0}{1} {2}",
			tabs,
			isClass ? "class" : "struct",
			current.name);

		if(classNode.children.Count == 0)
		{
			if(current.name == null)
				code.Add(tabs + "// size: " + current.size + " bytes");

			code.Add(line);
			code.Add(tabs + "{");
			code.Add(tabs + "};");
			code.Add("");

			return code;
		}

		Tag child = classNode.children[0].tag;

		bool firstInherit = true;
		while(child.tagType == TagType.Inheritance)
		{
			line += string.Format(
				"{0} {1} {2}",
				firstInherit ? ":" : ",",
				child.accessLevel.ToString().ToLower(),
				child.name);

			firstInherit = false;

			child = child.sibling == Tag.NoSibling
				? child
				: allTags[IDToIndex[child.sibling]];
		}

		code.Add(line);
		code.Add(tabs + "{");

		// Assume children are in the correct order
		// Could contain unions
		List<Node> childrenWithLocation = classNode.children
			.Where(i => i.tag.location >= 0)
			.ToList();

		int i = 0;

		for(; i < childrenWithLocation.Count; ++i)
		{
			child = childrenWithLocation[i].tag;

			// Detect anonymous unions
			Node first = childrenWithLocation
				.First(i => i.tag.location == child.location);

			Node last = childrenWithLocation
				.Last(i => i.tag.location == child.location);

			if(first != last)
			{
				int firstIndex = childrenWithLocation.IndexOf(first);
				int lastIndex = childrenWithLocation.IndexOf(last);

				List<Node> slice = childrenWithLocation.GetRange(
					firstIndex,
					lastIndex - firstIndex + 1);

				// If all are on the same location AND all are bitfields
				// assume it's not a union.
				bool all = slice.All(
					i =>
					{
						return i.tag.location == first.tag.location
							&& i.tag.bitSize > 0;
					});

				if(!all)
					slice.ForEach(i => i.tag.isAnonUnionMember = true);
			}
		}

		while(classNode.children.Count > 0
			  && classNode.children[0].tag.tagType == TagType.Inheritance)
			++i;

		AccessLevel accessLevel = isClass
			? AccessLevel.Private
			: AccessLevel.Public;

		for(i = 0; i < classNode.children.Count; ++i)
		{
			child = classNode.children[i].tag;

			List<string> innerCode = TagDispatcher(
					allTags,
					allMemberFuncs,
					IDToIndex,
					CU,
					allCUs,
					classNode.children[i],
					depth + 1);

			if(innerCode.Count > 0)
			{
				innerCode.RemoveAt(innerCode.Count - 1);

				code.Add("");
				code.AddRange(innerCode);

				continue;
			}

			if(child.accessLevel != accessLevel
			   && child.accessLevel != AccessLevel.None)
			{
				code.Add(tabs + child.accessLevel.ToString().ToLower() + ":");
				accessLevel = child.accessLevel;
			}

			if(child.isAnonUnionMember)
			{
				List<string> unionCode = GenerateAnonUnion(
					allTags,
					IDToIndex,
					CU,
					allCUs,
					classNode.children,
					ref i,
					depth + 1);

				code.AddRange(unionCode);
			}
			else
			{
				(string part1, string part2) = GetType(
					allTags,
					IDToIndex,
					CU,
					allCUs,
					classNode.children[i]);

					line = string.Format(
						"{0}\t{1}{2}{3}{4}{5};{6}",
						tabs,
						child.isStatic ? "static " : "",
						part1,
						child.name,
						part2,
						child.bitSize > 0 ? ": " + child.bitSize : "",
						!child.isStatic ? " // " + child.comment : "");

				code.Add(line);
			}
		}

		IEnumerable<Node> memberFuncs = allMemberFuncs
			.Where(i => i.tag.memberOfID == current.ID);

		// Assume all member functions are public, as we don't have the information
		// to know their access level.
		if(memberFuncs.Count() > 0)
		{
			code.Add("");
			code.Add(tabs + "public:");

			string extraTabs = new('\t', depth + 1);

			foreach(Node memberFunc in memberFuncs)
			{
				code.AddRange(
					GenerateFunction(
						allTags,
						IDToIndex,
						CU,
						allCUs,
						memberFunc,
						depth + 1));

				code.Add("");
			}

			code.RemoveAt(code.Count - 1);
		}

		code.Add(tabs + "};");
		code.Add("");

		return code;
	}

	// There is a chance we have unions (in structs) in unions, but the code
	// assumes it does not occur anywhere. If it did, I'm not even sure we
	// could properly detect it.
	private static List<string> GenerateAnonUnion(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		List<Node> members,
		ref int index,
		int depth)
	{
		string tabs = new('\t', depth);

		int extraDepth = 0;

		bool isStruct = false;

		List<string> code = new();
		code.Add("");
		code.Add(tabs + "union");
		code.Add(tabs + "{");

		for(; members[index].tag.isAnonUnionMember; ++index)
		{
			string extraTabs = tabs + new string('\t', extraDepth);

			Tag member = members[index].tag;
			if(member.bitSize > 0 && !isStruct)
			{
				++extraDepth;
				extraTabs = tabs + new string('\t', extraDepth);

				if(!code[code.Count - 1].EndsWith('{'))
					code.Add("");

				code.Add(extraTabs + "struct");
				code.Add(extraTabs + "{");

				isStruct = true;
			}
			else if(member.bitSize < 0 && isStruct)
			{
				if(index + 1 < members.Count)
				{
					Tag next = members[index + 1].tag;

					if(next.location < member.location && next.isAnonUnionMember)
						goto writeMember;
				}

				code.Add(extraTabs + "};");
				code.Add("");

				--extraDepth;
				isStruct = false;
			}

writeMember:
			string line = null;

			extraTabs = tabs + new string('\t', extraDepth);

			(string part1, string part2) = GetType(
				allTags,
				IDToIndex,
				CU,
				allCUs,
				members[index]);

			// Static not allowed in anonymous unions (YAY)
			if(member.bitSize > 0)
			{
				line = string.Format(
					"{0}\t{1}{2}{3}: {4};",
					extraTabs,
					part1,
					member.name,
					part2,
					member.bitSize);
			}
			else
			{
				line = string.Format(
					"{0}\t{1}{2}{3}; // {4}",
					extraTabs,
					part1,
					member.name,
					part2,
					member.comment);
			}

			code.Add(line);

			if(index + 1 >= members.Count)
			{
				++index;
				break;
			}
		}

		--index;

		code.Add(tabs + "};");
		code.Add("");

		return code;
	}
}
}
