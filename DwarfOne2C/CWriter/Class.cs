using System.Linq;
using System.Collections.Generic;

namespace DwarfOne2C
{
using TagType = Tag.TagType;

public partial class CWriter
{
	private static List<string> GenerateClassStruct(
		bool isClass,
		List<Tag> allTags,
		List<Tag> allMemberFuncs,
		Dictionary<int, int> IDToIndex,
		Tag current,
		int depth)
	{
		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		string line = string.Format(
			"{0}{1} {2}",
			tabs,
			isClass ? "class" : "struct",
			current.name);

		if(current.firstChild == -1)
		{
			if(current.name == null)
				code.Add(tabs + "// size: " + current.size + " bytes");

			code.Add(line);
			code.Add(tabs + "{");
			code.Add(tabs + "};");
			code.Add("");

			return code;
		}

		Tag child = allTags[IDToIndex[current.firstChild]];

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

		List<Tag> children = new();

		for(/* child */;
			child.sibling != Tag.NoSibling;
			child = allTags[IDToIndex[child.sibling]])
		{
			// This is perhaps not needed anymore...
			// Work around weirdness where anonymous structs are generated
			// with members (padding) and functions (padding) as children.
			/*if(child.tagType == TagType.Member
			   || child.tagType == TagType.TypeDef)*/
				children.Add(child);
		}

		// Assume children are in the correct order
		// Could contain unions
		List<Tag> childrenWithLocation = children
			.Where(i => i.location >= 0)
			.ToList();

		for(int i = 0; i < childrenWithLocation.Count; ++i)
		{
			child = childrenWithLocation[i];

			// Detect anonymous unions
			Tag first = childrenWithLocation
				.First(i => i.location == child.location);

			Tag last = childrenWithLocation
				.Last(i => i.location == child.location);

			if(first != last)
			{
				int firstIndex = childrenWithLocation.IndexOf(first);
				int lastIndex = childrenWithLocation.IndexOf(last);

				List<Tag> slice = childrenWithLocation.GetRange(
					firstIndex,
					lastIndex - firstIndex + 1);

				// If all are on the same location AND all are bitfields
				// assume it's not a union.
				if(!slice.All(i => i.location == first.location && i.bitSize > 0))
					slice.ForEach(i => i.isAnonUnionMember = true);
			}
		}

		while(children.Count > 0 && children[0].tagType == TagType.Inheritance)
			children.RemoveAt(0);

		AccessLevel accessLevel = isClass
			? AccessLevel.Private
			: AccessLevel.Public;

		for(int i = 0; i < children.Count; ++i)
		{
			child = children[i];

			List<string> innerCode = TagDispatcher(
					allTags,
					allMemberFuncs,
					IDToIndex,
					child,
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
					children,
					ref i,
					depth + 1);

				code.AddRange(unionCode);
			}
			else
			{
				(string part1, string part2) = GetType(allTags, IDToIndex, child);

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

		IEnumerable<Tag> memberFuncs = allMemberFuncs
			.Where(i => i.memberOfID == current.ID);

		// Assume all member functions are public, as we don't have the information
		// to know their access level.
		if(memberFuncs.Count() > 0)
		{
			code.Add("");
			code.Add(tabs + "public:");

			string extraTabs = new('\t', depth + 1);

			foreach(Tag memberFunc in memberFuncs)
			{
				code.AddRange(
					GenerateFunction(allTags, IDToIndex, memberFunc, depth + 1));

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
		List<Tag> members,
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

		for(; members[index].isAnonUnionMember; ++index)
		{
			string extraTabs = tabs + new string('\t', extraDepth);

			Tag member = members[index];
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
					Tag next = members[index + 1];

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

			(string part1, string part2) = GetType(allTags, IDToIndex, member);

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
