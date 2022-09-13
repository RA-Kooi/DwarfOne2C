using System;

namespace DwarfOne2C
{
public partial class CompilationUnit: Tag
{
	private static bool ParseName(string line, Tag tag)
	{
		if(line.StartsWith("AT_name"))
		{
			tag.name = line.Substring(9, line.Length - 11);
			return true;
		}

		return false;
	}

	private static bool ParseAccessLevel(string line, Tag tag)
	{
		if(line.StartsWith("AT_public"))
		{
			tag.accessLevel = AccessLevel.Public;
			return true;
		}
		else if(line.StartsWith("AT_private"))
		{
			tag.accessLevel = AccessLevel.Private;
			return true;
		}
		else if(line.StartsWith("AT_protected"))
		{
			tag.accessLevel = AccessLevel.Protected;
			return true;
		}

		return false;
	}

	private static bool ParseByteSize(string line, Tag tag)
	{
		if(!line.StartsWith("AT_byte_size"))
			return false;

		tag.size = Convert.ToInt32(line.Substring(13, line.Length - 14), 16);

		return true;
	}

	private static bool ParseAtMember(string line, Tag tag)
	{
		if(line.StartsWith("AT_member"))
		{
			// It's a (static) class member
			tag.memberOfID = Convert.ToInt32(
				line.Substring(10, line.Length - 11),
				16);

			return true;
		}

		return false;
	}

	private static void ParseModifiers(string line, int index, Tag tag)
	{
		int pointerLen = "MOD_pointer_to".Length;
		int refLen = "MOD_reference_to".Length;
		int constLen = "MOD_const".Length;
		int volatileLen = "MOD_volatile".Length;

		while(true)
		{
			if(line[index] != 'M')
				break;

			if(index + pointerLen <= line.Length
			   && line.Substring(index, pointerLen) == "MOD_pointer_to")
			{
				tag.modifiers.Add(Type.Modifier.Pointer);
				index += pointerLen + 1;
			}
			else if(index + refLen <= line.Length
					&& line.Substring(index, refLen) == "MOD_reference_to")
			{
				tag.modifiers.Add(Type.Modifier.Reference);
				index += refLen + 1;
			}
			else if(index + constLen <= line.Length
					&& line.Substring(index, constLen) == "MOD_const")
			{
				tag.modifiers.Add(Type.Modifier.Const);
				index += constLen + 1;
			}
			else if(index + volatileLen <= line.Length
					&& line.Substring(index, volatileLen) == "MOD_volatile")
			{
				tag.modifiers.Add(Type.Modifier.Volatile);
				index += volatileLen + 1;
			}
		}

		// This is some stupid bullshit to check if we have a reference to a
		// pointer (size 4), a pointer, or just a reference. So in this case
		// a reference to a pointer will be tagged as a pointer for ordering
		// purposes.

		bool maybeReference = false;
		foreach(Type.Modifier modifier in tag.modifiers)
		{
			if(modifier == Type.Modifier.Reference)
				maybeReference = true;
			else if(modifier == Type.Modifier.Pointer)
			{
				tag.isPointer = true;
				break;
			}
		}

		tag.isReference = maybeReference && !tag.isPointer;
	}

	private static bool ParseFundamentalType(string line, Tag tag)
	{
		if(line.StartsWith("AT_fund_type"))
		{
			string FT = line.Substring(13, line.Length - 14);
			Type.BuiltInType builtInType = Type.FTToBuiltInType(FT);

			/*if(builtInType == Type.BuiltInType.Pointer)
			{
				tag.typeID = (int)builtInType + 1;
				tag.modifiers.Add(Type.Modifier.Pointer);
				tag.size = 4;
				tag.isPointer = true;
			}
			else*/
			{
				tag.typeID = (int)builtInType;
				tag.size = Type.BuiltInTypeSize(builtInType);
			}

			return true;
		}

		return false;
	}

	private static bool ParseFundamentalTypeMod(string line, Tag tag)
	{
		if(line.StartsWith("AT_mod_fund_type"))
		{
			ParseModifiers(line, line.IndexOf("MOD_"), tag);

			int typeNameStart = line.IndexOf("FT_");
			int typeNameEnd = line.IndexOf(')');

			string typeName = line.Substring(
				typeNameStart,
				typeNameEnd - typeNameStart);

			Type.BuiltInType builtInType = Type.FTToBuiltInType(typeName);
			tag.typeID = (int)builtInType;

			int size = Type.BuiltInTypeSize(builtInType);

			tag.size = tag.isPointer
				? 4
				: tag.tagType == TagType.ArrayType
					? (tag.length < 0 ? 4 : size * tag.length)
					: size;

			return true;
		}

		return false;
	}

	private bool ParseUserType(string line, Tag tag)
	{
		if(line.StartsWith("AT_user_def_type"))
		{
			string hexID = line.Substring(17, line.Length - 18);
			tag.typeID = Convert.ToInt32(hexID, 16);

			int index = -1;

			if(IDToIndex.ContainsKey(tag.typeID))
				index = IDToIndex[tag.typeID];
			else
			{
				tag.isDirty = true;
				return true;
			}

			Tag referenced = allTags[index];
			tag.size = tag.tagType == TagType.ArrayType
				? (tag.length < 0 ? 4 : referenced.size * tag.length)
				: referenced.size;

			return true;
		}

		return false;
	}

	private bool ParseUserTypeMod(string line, Tag tag)
	{
		if(line.StartsWith("AT_mod_u_d_type"))
		{
			ParseModifiers(line, line.IndexOf("MOD_"), tag);

			int numIndex = line.IndexOf("0x");
			string hexID = line.Substring(numIndex, line.Length - numIndex - 1);
			tag.typeID = Convert.ToInt32(hexID, 16);

			int index = -1;

			if(IDToIndex.ContainsKey(tag.typeID))
				index = IDToIndex[tag.typeID];
			else
			{
				tag.isDirty = true;
				return true;
			}

			Tag referenced = allTags[index];

			// Pointer: 4
			// Ref to pointer: 4
			// Ref to array (pointer): 4
			// Flexible array: 4
			if(tag.isPointer)
				tag.size = 4; // Includes ref to pointer
			else if(tag.isReference)
			{
				if(referenced.tagType == TagType.ArrayType)
					tag.size = 4;
			}
			else
				tag.size = referenced.length < 0
					? 4
					: referenced.size * referenced.length;

			return true;
		}

		return false;
	}

	private bool ParseTypes(string line, Tag tag)
	{
		if(!ParseFundamentalType(line, tag)
		   && !ParseFundamentalTypeMod(line, tag)
		   && !ParseUserType(line, tag)
		   && !ParseUserTypeMod(line, tag))
			return false;

		return true;
	}

	private static bool ParseLocation(string line, Tag tag)
	{
		if(line.StartsWith("AT_location"))
		{
			int numIndex = line.IndexOf("0x");
			int numEnd = line.IndexOf(')');

			tag.location = Convert.ToInt32(
				line.Substring(numIndex, numEnd - numIndex),
				16);

			return true;
		}

		return false;
	}

	private static bool ParseLoUser(string line, Tag tag)
	{
		if(line.StartsWith("AT_lo_user"))
		{
			// It's a static class member
			// OR it's a static function/variable local to the CU
			// BUT it could also be a class member function, fix this up in a
			// second pass...
			tag.isStatic = true;

			tag.comment = line.Substring(12, line.Length - 14);

			return true;
		}

		return false;
	}

	private static bool ParseReference(string line, Tag tag)
	{
		if(line.StartsWith('('))
		{
			int reference = Convert.ToInt32(
				line.Substring(1, line.Length - 2),
				16);

			tag.references.Add(reference);

			return true;
		}

		return false;
	}
}
}
