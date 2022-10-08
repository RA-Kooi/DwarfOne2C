using System;
using System.Text.RegularExpressions;

namespace DwarfOne2C
{
public partial class DumpParser
{
	private RootTag ParseCompileUnit(
		string[] lines,
		ref int current,
		int ID,
		int sibling)
	{
		RootTag tag = new(ID, sibling);

		for(; current < lines.Length; ++current)
		{
			if(lines[current] == string.Empty)
				break;

			string line = lines[current].TrimStart();

			if(ParseName(line, tag))
			{
				; // Nothing
			}
			else if(line.StartsWith("AT_language"))
			{
				string language = line.Substring(12, line.Length - 13);

				if(language == "LANG_C_PLUS_PLUS")
				{
					tag.language = RootTag.Language.Cpp;
				}
				else if(language.StartsWith("LANG_C"))
				{
					tag.language = RootTag.Language.C;
				}
				else
				{
					throw new NotImplementedException(
						"Unimplemented language tag.");
				}
			}
			else
				Console.Error.WriteLine($"Unknown attribute: {line} @{current}");
		}

		return tag;
	}

	private Tag ParseStruct(
		string[] lines,
		ref int current,
		int ID,
		int sibling,
		TagType tagType)
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
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.Enum;

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
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.ArrayType;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(line.StartsWith("AT_subscr_data"))
			{
				Regex regex;
				string searchFull = @"(?>AT_subscr_data\(<\d+>";

				int dimensionCount = 0;

				if(line.Contains("FT_long["))
				{
					string searchStr = @"FT_long\[0:(\d+)\], ";
					dimensionCount = Regex.Matches(line, searchStr).Count;

					for (int i = dimensionCount; i > 0; --i)
						searchFull += searchStr;

					regex = new(searchFull + @"FMT_ET: (.*)\))");
				}
				else if(line.Contains("FT_integer["))
				{
					string searchStr = @"FT_integer\[0:(\d+)\], ";
					dimensionCount = Regex.Matches(line, searchStr).Count;

					for (int i = dimensionCount; i > 0; --i)
						searchFull += searchStr;

					regex = new(searchFull + @"FMT_ET: (.*)\))");
				}
				else
				{
					throw new NotImplementedException(
						"Arrays with size type other than " +
						"FT_long or FT_integer are not supported");
				}

				Match match = regex.Match(line);

				if (match.Success)
				{
					string typeRef;

					GroupCollection groups = match.Groups;

					tag.isMultidimArray = dimensionCount > 1;

					if (tag.isMultidimArray)
					{
						for (int i = 0; i < dimensionCount; ++i)
						{
							uint length = Convert.ToUInt32(groups[1 + i].Value);
							tag.arrayDimLengths.Add(unchecked((int)length));
						}

						typeRef = groups[1 + dimensionCount].Value;
					}
					else
					{
						uint length = Convert.ToUInt32(groups[1].Value);
						tag.length = unchecked((int)length);
						typeRef = groups[2].Value;
					}

					ParseTypes(typeRef, tag);
				}
				else
				{
					throw new NotImplementedException(
						"Unknown array format: " + line + " @" + current);
				}
			}
			else
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.Inheritance;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseUserType(line, tag)
			   && !ParseLocation(line, tag)
			   && !ParseName(line, tag)
			   && !ParseAccessLevel(line, tag))
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParseFunction(
		string[] lines,
		ref int current,
		int ID,
		int sibling,
		TagType tagType)
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
					&& !ParseReference(line, tag)
					&& !ParseName(line, tag)
					&& !ParseTypes(line, tag)
					&& !ParseLoUser(line, tag)
					&& !ParseAccessLevel(line, tag)
					&& !ParseAtMember(line, tag))
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
		}

		return tag;
	}

	private Tag ParseParameter(
		string[] lines,
		ref int current,
		int ID,
		int sibling,
		bool isVariadic)
	{
		Tag tag = new();
		tag.ID = ID;
		tag.sibling = sibling;
		tag.tagType = isVariadic ? TagType.VariadicParam : TagType.Param;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag)
			   && !ParseTypes(line, tag)
			   && !line.StartsWith("AT_location"))
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.LocalVar;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag)
			   && !ParseTypes(line, tag)
			   && !ParseLocation(line, tag))
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.TypeDef;
		tag.isStatic = true;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag) && !ParseTypes(line, tag))
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.Member;

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
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.GlobalVar;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseName(line, tag)
			   && !ParseTypes(line, tag)
			   && !ParseLoUser(line, tag)
			   && !ParseLocation(line, tag))
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.FunctionPointer;

		for(; lines[current].StartsWith(' '); ++current)
		{
			string line = lines[current].TrimStart();

			if(!ParseTypes(line, tag))
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.PtrToMemberFunc;

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
				Console.Error.WriteLine("Unknown attribute: " + line + " @" + current);
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
		tag.tagType = TagType.Padding;

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
					&& !ParseReference(line, tag)
					&& !ParseLocation(line, tag))
				Console.Error.WriteLine("Unkown attribute: " + line + " @" + current);
		}

		return tag;
	}
}
}
