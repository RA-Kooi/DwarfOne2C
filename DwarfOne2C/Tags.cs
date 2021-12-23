using System;
using System.Collections.Generic;

namespace DwarfOne2C
{
public enum AccessLevel
{
	None, // union
	Public,
	Private,
	Protected,
}

public class Tag
{
	public int ID;
	public int sibling;

	// Used for Tags with child tags
	// CompilationUnit
	// Class
	// Struct
	// Union
	public int firstChild = -1;

	// Empty tag ID
	public static readonly int NoSibling = -1;

	public enum TagType
	{
		Tag,
		End,
		ArrayType,
		CULocalFunc,
		Class,
		Enum,
		FunctionPointer,
		GlobalFunc,
		GlobalVar, // In reverse order unless class var?
		Inheritance,
		LocalVar, // NOT in reverse order
		Member, // Class, struct, or union member
		Param,
		PtrToMemberFunc,
		Struct,
		TypeDef, // In reverse order
		Union,
		Padding, // Local variable OR member...
		MemberFunc,
		VariadicParam,
	}

	public TagType tagType;

	// TAG_*
	// Except: TAG_array_type, TAG_subroutine_type
	public string name;

	public string comment; // AT_lo_user value

	// Class
	// Enum
	// Struct
	// Typedef: For class member ordering (if referenced by class (static))
	// Union
	// Member: For class member ordering
	public int size;

	// ArrayType
	public int length = 1;

	// ArrayType
	// Parameter
	// Typedef
	// Member
	// Inheritance: We don't care about the typeID since the name is sufficient
	public int typeID;

	// Enum
	public List<string> elements = new(); // [FOO = 1,][BAR = 2,]

	// Inheritance
	// Member
	public AccessLevel accessLevel = AccessLevel.None;

	// Function: If memberOfID > -1
	// GlobalVariable: If memberOfID > -1
	// LocalVariable: This depends on who owns it and if it is local or not
	// LocalVariable: If the owner is a function -> not static
	// LocalVariable: If the owner is a CU -> static
	// Typedef: If the owner is a class or struct, it is static
	public bool isStatic = false;

	// Function
	// If it's a static class function this will be set
	public int memberOfID = -1;

	// Parameter
	// Member
	// Typedef
	// NOT in reverse, reversed in second pass
	public List<Type.Modifier> modifiers = new();

	// Parameter
	// Member
	// Typedef
	public bool isPointer = false; // Size helper
	public bool isReference = false; // size helper
	
	// ArrayType
	// 
	public bool isMultidimArray = false;
	public List<int> arrayDimLengths = new();

	// Inheritance
	// Member
	// Typedef (indirect)
	public int location = -1; // for class member ordering

	// Member (union)
	public int bitSize = -1, bitOffset = -1;

	// Member
	// Classes may have anonymous unions in them, so we have to detect that.
	public bool isAnonUnionMember = false;

	// Size could not be calculated yet.
	public bool isDirty = false;

	// Padding
	public bool isFunction = true;
}

public class Type
{
	public enum BuiltInType
	{
		Boolean          = -01, // FT_boolean; bool

		Char             = -02, // FT_char; char
		SignedChar       = -03, // FT_signed_char; signed char
		UnsignedChar     = -04, // FT_unsigned_char; unsigned char

		Short            = -05, // FT_short; short
		SignedShort      = -06, // FT_signed_short; signed short
		UnsignedShort    = -07, // FT_unsigned_short; unsigned short

		Integer          = -08, // FT_integer; int
		SignedInt        = -09, // FT_signed_integer; signed int
		UnsignedInt      = -10, // FT_unsigned_integer; unsigned int

		Long             = -11, // FT_long; long
		SignedLong       = -12, // FT_signed_long; signed long
		UnsignedLong     = -13, // FT_unsigned_long; unsigned long

		Float            = -14, // FT_float; float
		DoublePrecFloat  = -15, // FT_dbl_prec_float; long float
		ExtPrecFloat     = -16, // FT_ext_prec_float; long long float

		Double           = -17, // FT_complex; double
		DoublePrecDouble = -18, // FT_dbl_prec_complex; long double
		ExtPrecDouble    = -19, // FT_ext_prec_complex; long long double

		Void             = -20, // FT_void; void
		Pointer          = -21, // FT_pointer; void*

		Label            = -22, // FT_label; Unused (fortran)

		Unknown          = -23, // For when AT_fund_type() is empty...
	}

	public enum Modifier
	{
		Const,
		Volatile,
		Pointer,
		Reference
	}

	public static string BuiltInToString(BuiltInType builtInType)
	{
		switch(builtInType)
		{
		case BuiltInType.Boolean:
			return "bool";
		case BuiltInType.Char:
			return "char";
		case BuiltInType.SignedChar:
			return "signed char";
		case BuiltInType.UnsignedChar:
			return "unsigned char";
		case BuiltInType.Short:
			return "short";
		case BuiltInType.SignedShort:
			return "signed short";
		case BuiltInType.UnsignedShort:
			return "unsigned short";
		case BuiltInType.Integer:
			return "int";
		case BuiltInType.SignedInt:
			return "signed int";
		case BuiltInType.UnsignedInt:
			return "unsigned int";
		case BuiltInType.Long:
			return "long";
		case BuiltInType.SignedLong:
			return "signed long";
		case BuiltInType.UnsignedLong:
			return "unsigned long";
		case BuiltInType.Float:
			return "float";
		case BuiltInType.DoublePrecFloat:
			return "long float";
		case BuiltInType.ExtPrecFloat:
			return "long long float";
		case BuiltInType.Double:
			return "double";
		case BuiltInType.DoublePrecDouble:
			return "long double";
		case BuiltInType.ExtPrecDouble:
			return "long long double";
		case BuiltInType.Void:
			return "void";
		case BuiltInType.Pointer:
			return "void *";
		case BuiltInType.Unknown:
			return "UnknownType";
		}

		throw new ArgumentException(
			string.Format("{0} is not a builtInType", builtInType));
	}

	public static BuiltInType FTToBuiltInType(string FT)
	{
		switch(FT)
		{
		case "FT_boolean":
			return BuiltInType.Boolean;
		case "FT_char":
			return BuiltInType.Char;
		case "FT_signed_char":
			return BuiltInType.SignedChar;
		case "FT_unsigned_char":
			return BuiltInType.UnsignedChar;
		case "FT_short":
			return BuiltInType.Short;
		case "FT_signed_short":
			return BuiltInType.SignedShort;
		case "FT_unsigned_short":
			return BuiltInType.UnsignedShort;
		case "FT_integer":
			return BuiltInType.Integer;
		case "FT_signed_integer":
			return BuiltInType.SignedInt;
		case "FT_unsigned_integer":
			return BuiltInType.UnsignedInt;
		case "FT_long":
			return BuiltInType.Long;
		case "FT_signed_long":
			return BuiltInType.SignedLong;
		case "FT_unsigned_long":
			return BuiltInType.UnsignedLong;
		case "FT_float":
			return BuiltInType.Float;
		case "FT_dbl_prec_float":
			return BuiltInType.DoublePrecFloat;
		case "FT_ext_prec_float":
			return BuiltInType.ExtPrecFloat;
		case "FT_complex":
			return BuiltInType.Double;
		case "FT_dbl_prec_complex":
			return BuiltInType.DoublePrecDouble;
		case "FT_ext_prec_complex":
			return BuiltInType.ExtPrecDouble;
		case "FT_void":
			return BuiltInType.Void;
		case "FT_pointer":
			return BuiltInType.Pointer;
		}

		throw new ArgumentException(FT + " is not a built-in type.");
	}

	public static int BuiltInTypeSize(BuiltInType builtInType)
	{
		switch(builtInType)
		{
		case BuiltInType.Boolean:
		case BuiltInType.Char:
		case BuiltInType.SignedChar:
		case BuiltInType.UnsignedChar:
			return 1;
		case BuiltInType.Short:
		case BuiltInType.SignedShort:
		case BuiltInType.UnsignedShort:
			return 2;
		case BuiltInType.Integer:
		case BuiltInType.SignedInt:
		case BuiltInType.UnsignedInt:
		case BuiltInType.Long:
		case BuiltInType.SignedLong:
		case BuiltInType.UnsignedLong:
		case BuiltInType.Float:
			return 4;
		case BuiltInType.DoublePrecFloat:
			return 8;
		case BuiltInType.ExtPrecFloat:
			return 12;
		case BuiltInType.Double:
			return 8;
		case BuiltInType.DoublePrecDouble:
			return 12;
		case BuiltInType.ExtPrecDouble:
			return 16;
		case BuiltInType.Void:
			return 0;
		case BuiltInType.Pointer:
			return 4;
		}

		throw new ArgumentException(
			string.Format("{0} is not a builtInType", builtInType));
	}
}
}
