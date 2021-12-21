using System;
using System.Text.RegularExpressions;

namespace DwarfOne2C
{
public partial class CompilationUnit: Tag
{
	private Tag ParseStruct(
		string[] lines,
		ref int current,
		int ID,
		int sibling,
		Tag.TagType tagType)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;

		tag.tagType = tagType;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag)
			   && !ParseByteSize(line, tag))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		tag.comment = "size: " + $"0x{tag.size:X}";

		// Check for mysterious anonymous structs...
		if(tag.name == null)
			tag.name = "__anon_" + $"0x{tag.ID:X}";

		return tag;
	}

	private Tag ParseEnum(string[] lines, ref int current, int ID, int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.Enum;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(ParseName(line, tag)
			   || ParseByteSize(line, tag))
			{
				; // Nothing
			}
			else if(line.StartsWith("AT_element_list"))
			{
				// (0="FOO")(1="BAR")
				Regex regex = new(@"(?>\((\d+)=""(\w+)""\))");
				MatchCollection matches = regex.Matches(line);

				foreach(Match match in matches)
				{
					GroupCollection groups = match.Groups;
					tag.elements.Add(
						String.Format(
							"{1} = {0},", groups[1].Value, groups[2].Value));
				}
			}
			else
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		tag.comment = $"size: 0x{tag.size:X}";

		return tag;
	}

	private Tag ParseArray(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.ArrayType;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(line.StartsWith("AT_subscr_data"))
			{
				if(!line.Contains("FT_long"))
				{
					throw new NotImplementedException(
						"Array with size type other than " +
						"FT_long is not supported");
				}

				Regex regex = new(
					@"(?>AT_subscr_data\(<\d+>FT_long\[0:(\d+)\], FMT_ET: (.*)\))");

				Match match = regex.Match(line);
				GroupCollection groups = match.Groups;

				uint length = Convert.ToUInt32(groups[1].Value);
				tag.length = unchecked((int)length);

				string typeRef = groups[2].Value;
				ParseTypes(typeRef, tag);
			}
			else
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParseInheritance(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.Inheritance;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseUserType(line, tag)
			   && !ParseLocation(line, tag)
			   && !ParseName(line, tag)
			   && !ParseAccessLevel(line, tag))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParseFunction(
		string[] lines,
		ref int current,
		int ID,
		int sibling,
		Tag.TagType tagType)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = tagType;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!line.StartsWith("AT_low_pc")
					&& !line.StartsWith("AT_high_pc")
					&& !line.StartsWith('(')
					&& !ParseName(line, tag)
					&& !ParseTypes(line, tag)
					&& !ParseLoUser(line, tag)
					&& !ParseAccessLevel(line, tag)
					&& !ParseAtMember(line, tag))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParseParameter(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.Param;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag)
			   && !ParseTypes(line, tag)
			   && !line.StartsWith("AT_location"))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParseLocalVariable(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.LocalVar;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag)
			   && !ParseTypes(line, tag)
			   && !line.StartsWith("AT_location"))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		// We cannot determine if this is a CU local variable or a function
		// local variable at this time, we have to determine that later.

		return tag;
	}

	private Tag ParseTypedef(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.TypeDef;
		tag.isStatic = true;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag) && !ParseTypes(line, tag))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParseMember(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.Member;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(ParseName(line, tag)
			   || ParseAccessLevel(line, tag)
			   || ParseTypes(line, tag)
			   || ParseLocation(line, tag))
			{
				; // nothing
			}
			else if(line.StartsWith("AT_bit_offset"))
			{
				tag.bitOffset = Convert.ToInt32(
					line.Substring(14, line.Length - 15),
					16);
			}
			else if(line.StartsWith("AT_bit_size"))
			{
				tag.bitSize = Convert.ToInt32(
					line.Substring(12, line.Length - 13),
					16);
			}
			else
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		tag.comment = $"0x{tag.location:X}";

		return tag;
	}

	private Tag ParseGlobalVariable(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.GlobalVar;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag)
			   && !ParseTypes(line, tag)
			   && !ParseLoUser(line, tag)
			   && !line.StartsWith("AT_location"))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParseFunctionPointer(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.FunctionPointer;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseTypes(line, tag))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParsePointerToMemberFunc(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = Tag.TagType.PtrToMemberFunc;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(line.StartsWith("AT_containing_type"))
			{
				tag.memberOfID =
					Convert.ToInt32(line.Substring(19, line.Length - 20), 16);
			}
			else if(!ParseUserType(line, tag)
					&& !line.Contains("AT_containing_type"))
				Console.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		tag.modifiers.Add(Type.Modifier.Pointer);

		tag.size = 12;

		return tag;
	}

	private Tag ParsePadding(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;

		// This is a global variable OR a member
		tag.tagType = Tag.TagType.Padding;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(line.StartsWith("AT_low_pc")
			   || line.StartsWith("AT_high_pc"))
			{
				// This is actually a function 🤡
				tag.isFunction = true;
			}
			else if(!ParseName(line, tag)
					&& !ParseTypes(line, tag)
					&& !ParseLoUser(line, tag)
					&& !ParseAtMember(line, tag)
					&& !line.StartsWith('(')
					&& !line.StartsWith("AT_location"))
				Console.WriteLine("Unkown attribute: " + line + " @" + current);
		}

		return tag;
	}
}
}
