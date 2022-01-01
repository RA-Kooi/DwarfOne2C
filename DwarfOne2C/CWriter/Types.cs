using System.Text;
using System.Collections.Generic;

namespace DwarfOne2C
{
using TagType = Tag.TagType;

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
		Tag current)
	{
		string type, part2 = "";

		if(current.typeID > 0)
		{
			(type, part2) = GetType(allTags, IDToIndex, current);
			type += GetModifiers(allTags, IDToIndex, current);
		}
		else
		{
			type = GetTypeName(allTags, IDToIndex, current);
			type += GetModifiers(allTags, IDToIndex, current);
		}

		if (current.isMultidimArray)
		{
			foreach (int len in current.arrayDimLengths)
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
				current.length < 0
					? ""
					: "" + (current.length + 1))
				+ part2;
		}

		return (type, part2);
	}

	private static (string part1, string part2) GetFunctionPointer(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag current,
		bool isMemberFunc = false)
	{
		(string part1, string part2) = (null, "");

		if(current.typeID < 0)
		{
			part1 = GetTypeName(allTags, IDToIndex, current);
			part1 += GetModifiers(allTags, IDToIndex, current, false) + "(";
		}
		else
		{
			(part1, part2) = GetType(allTags, IDToIndex, current);
			part1 += GetModifiers(allTags, IDToIndex, current) + "(";
		}

		StringBuilder sb = new();
		sb.Append(')');
		sb.Append('(');

		if(current.firstChild > -1)
		{
			int i = 0;
			for(Tag child = allTags[IDToIndex[current.firstChild]];
				child.sibling != Tag.NoSibling;
				child = allTags[IDToIndex[child.sibling]])
			{
				(string pPart1, string pPart2) = GetType(
					allTags,
					IDToIndex,
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
		Tag current)
	{
		Tag referenced = allTags[IDToIndex[current.typeID]];
		(string part1, string part2) = GetType(allTags, IDToIndex, referenced);

		Tag refClass = allTags[IDToIndex[current.memberOfID]];
		part1 += refClass.name + "::";
		part1 += GetModifiers(allTags, IDToIndex, current, false);

		return (part1, part2);
	}

	private static (string part1, string part2) GetType(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag current)
	{
		string part1, part2 = "";

		Tag referenced = current;

		if(current.typeID > 0)
		{
			referenced = allTags[IDToIndex[current.typeID]];
		}

		if(referenced.tagType == TagType.ArrayType)
			(part1, part2) = GetArray(allTags, IDToIndex, referenced);
		else if(referenced.tagType == TagType.FunctionPointer)
		{
			(part1, part2) = GetFunctionPointer(allTags, IDToIndex, referenced);
		}
		else if(referenced.tagType == TagType.PtrToMemberFunc)
		{
			(part1, part2) = GetMemberFunctionPointer(
				allTags,
				IDToIndex,
				referenced);
		}
		else
			part1 = GetTypeName(allTags, IDToIndex, referenced);

		part1 += GetModifiers(allTags, IDToIndex, current, false);

		return (part1, part2);
	}
}
}
