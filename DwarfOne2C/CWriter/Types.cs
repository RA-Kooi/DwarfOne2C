using System.Text;
using System.Collections.Generic;

namespace DwarfOne2C
{
public partial class CWriter
{
	private static string GetModifiers(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag tag,
		bool prependSpace = true)
	{
		StringBuilder sb = new();

		if(tag.modifiers.Count > 0 && prependSpace)
			sb.Append(' ');

		foreach(Type.Modifier modifier in tag.modifiers)
		{
			if(modifier == Type.Modifier.Pointer)
			{
				sb.Append('*');
			}
			else if(modifier == Type.Modifier.Reference)
			{
				sb.Append('&');
			}
			else
			{
				sb.Append(modifier.ToString().ToLower());
				sb.Append(' ');
			}
		}

		return sb.ToString();
	}

	private static string GetTypeName(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag current,
		bool appendSpace = true)
	{
		StringBuilder sb = new();

		if(current.typeID < 0)
		{
			sb.Append(
				Type.BuiltInToString(
					(Type.BuiltInType)current.typeID));

			if(current.typeID == (int)Type.BuiltInType.Pointer)
				appendSpace = false;
		}
		else
		{
			if(current.name == null)
				current = allTags[IDToIndex[current.typeID]];

			sb.Append(current.name);
			sb.Append(GetModifiers(allTags, IDToIndex, current));
		}

		if(appendSpace)
			sb.Append(' ');

		return sb.ToString();
	}

	private static (string part1, string part2) GetArray(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node current)
	{
		string type, part2 = "";

		if(current.tag.typeID > 0)
		{
			(type, part2) = GetType(allTags, IDToIndex, CU, allCUs, current);
		}
		else
		{
			type = GetTypeName(allTags, IDToIndex, current.tag);
			type += GetModifiers(allTags, IDToIndex, current.tag);
		}

		if (current.tag.isMultidimArray)
		{
			foreach (int len in current.tag.arrayDimLengths)
			{
				part2 = string.Format(
					"[{0}]",
					len < 0
						? ""
						: "" + (len + 1))
					+ part2;
			}
		}
		else
		{
			part2 = string.Format(
				"[{0}]",
				current.tag.length < 0
					? ""
					: "" + (current.tag.length + 1))
				+ part2;
		}

		return (type, part2);
	}

	private static (string part1, string part2) GetFunctionPointer(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node pointerNode,
		bool isMemberFunc = false)
	{
		Tag current = pointerNode.tag;

		(string part1, string part2) = (null, "");

		if(current.typeID < 0)
		{
			part1 = GetTypeName(allTags, IDToIndex, current);
			part1 += GetModifiers(allTags, IDToIndex, current, false) + "(";
		}
		else
		{
			(part1, part2) = GetType(allTags, IDToIndex, CU, allCUs, pointerNode);
			part1 += "(";
		}

		StringBuilder sb = new();
		sb.Append(')');
		sb.Append('(');

		if(pointerNode.children.Count > 0)
		{
			int i = 0;
			foreach(Node child in pointerNode.children)
			{
				(string pPart1, string pPart2) = GetType(
					allTags,
					IDToIndex,
					CU,
					allCUs,
					child);

					sb.Append(pPart1);
					sb.Append("/* unknown" + i + " */");
					sb.Append(pPart2);

				sb.Append(',');
				sb.Append(' ');

				++i;
			}

			sb.Remove(sb.Length - 2, 2);
		}

		sb.Append(')');

		return (part1, sb.ToString() + part2);
	}

	private static (string part1, string part2) GetMemberFunctionPointer(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node current)
	{
		Node referenced = CU.Find(current.tag.typeID);

		if(referenced == null)
		{
			for(int i = 0; i < allCUs.Count; ++i)
			{
				Node unit = allCUs[i];

				if(i < allCUs.Count - 2)
				{
					Node nextUnit = allCUs[i + 1];

					if(nextUnit.tag.ID < current.tag.typeID)
						continue;
				}

				referenced = unit.Find(current.tag.typeID);

				if(referenced != null)
					break;
			}
		}

		(string part1, string part2) = GetType(
			allTags,
			IDToIndex,
			CU,
			allCUs,
			referenced);

		Tag refClass = allTags[IDToIndex[current.tag.memberOfID]];

		part1 += refClass.name + "::";
		part1 += GetModifiers(allTags, IDToIndex, current.tag, false);

		return (part1, part2);
	}

	private static (string part1, string part2) GetType(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node current)
	{
		string part1, part2 = "";

		Node referenced = current;

		if(current.tag.typeID > 0)
		{
			referenced = CU.Find(current.tag.typeID);

			if(referenced == null)
			{
				for(int i = 0; i < allCUs.Count; ++i)
				{
					Node unit = allCUs[i];

					if(i < allCUs.Count - 2)
					{
						Node nextUnit = allCUs[i + 1];

						if(nextUnit.tag.ID < current.tag.typeID)
							continue;
					}

					referenced = unit.Find(current.tag.typeID);

					if(referenced != null)
						break;
				}
			}
		}

		if(referenced.tag.tagType == TagType.ArrayType)
		{
			(part1, part2) = GetArray(allTags, IDToIndex, CU, allCUs, referenced);
		}
		else if(referenced.tag.tagType == TagType.FunctionPointer)
		{
			(part1, part2) = GetFunctionPointer(
				allTags,
				IDToIndex,
				CU,
				allCUs,
				referenced);
		}
		else if(referenced.tag.tagType == TagType.PtrToMemberFunc)
		{
			(part1, part2) = GetMemberFunctionPointer(
				allTags,
				IDToIndex,
				CU,
				allCUs,
				referenced);
		}
		else
			part1 = GetTypeName(allTags, IDToIndex, referenced.tag);

		part1 += GetModifiers(allTags, IDToIndex, current.tag, false);

		return (part1, part2);
	}
}
}
