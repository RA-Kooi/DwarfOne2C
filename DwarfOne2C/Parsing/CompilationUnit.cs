using System;
using System.Collections.Generic;

namespace DwarfOne2C
{
public partial class CompilationUnit: Tag
{
	public enum Language
	{
		C,
		Cpp
	};

	public Language language;

	public List<Tag> childTags = new();

	private List<Tag> allTags;
	private Dictionary<int /* ID */, int /* tagIndex */> IDToIndex;

	public CompilationUnit(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		string[] lines,
		ref int current)
	{
		this.allTags = allTags;
		this.IDToIndex = IDToIndex;

		this.tagType = TagType.CompileUnit;

		ID = Convert.ToInt32(
			lines[current++].Split(
				':',
				StringSplitOptions.RemoveEmptyEntries)[0],
			16);

		string sibling = lines[current++].TrimStart();

		this.sibling = Convert.ToInt32(
			sibling.Substring(11, sibling.Length - 12),
			16);

		for(; current < lines.Length; ++current)
		{
			if(lines[current] == string.Empty)
				break;

			string line = lines[current].TrimStart();

			if(line.StartsWith("AT_name"))
			{
				name = line.Substring(9, line.Length - 11);
			}
			else if(line.StartsWith("AT_language"))
			{
				string language = line.Substring(12, line.Length - 13);
				if(language == "LANG_C_PLUS_PLUS")
					this.language = CompilationUnit.Language.Cpp;
				else if(language.StartsWith("LANG_C"))
					this.language = CompilationUnit.Language.C;
				else
					throw new NotImplementedException(
						"Unimplemented language tag.");
			}
		}

		allTags.Add(this);
		IDToIndex.Add(ID, allTags.Count - 1);
	}

	public void Parse(string[] lines, int current)
	{
		for(; current < lines.Length; ++current)
		{
			if(lines[current].EndsWith("TAG_compile_unit"))
			{
				break;
			}
			else if(lines[current].Contains("TAG_"))
			{
				string[] tagLine = lines[current++].Split(
					" ",
					StringSplitOptions.RemoveEmptyEntries);

				int ID = Convert.ToInt32(tagLine[0].TrimEnd(':'), 16);

				string siblingAttr = lines[current++].TrimStart();
				int sibling = Convert.ToInt32(
					siblingAttr.Substring(11, siblingAttr.Length - 12),
					16);

				switch(tagLine[2])
				{
					case "TAG_array_type":
					{
						allTags.Add(ParseArray(lines, ref current, ID, sibling));
					} break;
					case "TAG_class_type":
					{
						allTags.Add(
							ParseStruct(
								lines,
								ref current,
								ID,
								sibling,
								TagType.Class));
					} break;
					case "TAG_enumeration_type":
					{
						allTags.Add(ParseEnum(lines, ref current, ID, sibling));
					} break;
					case "TAG_formal_parameter":
					{
						allTags.Add(
							ParseParameter(lines, ref current, ID, sibling, false));
					} break;
					case "TAG_unspecified_parameters":
					{
						allTags.Add(
							ParseParameter(lines, ref current, ID, sibling, true));
					} break;
					case "TAG_global_subroutine":
					{
						// function
						Tag tag = ParseFunction(
							lines,
							ref current,
							ID,
							sibling,
							TagType.GlobalFunc);

						allTags.Add(tag);
					} break;
					case "TAG_global_variable":
					{
						// global var or static member
						allTags.Add(
							ParseGlobalVariable(lines, ref current, ID, sibling));
					} break;
					case "TAG_inheritance":
					{
						allTags.Add(
							ParseInheritance(lines, ref current, ID, sibling));
					} break;
					case "TAG_local_variable":
					{
						// Function/CU local var
						allTags.Add(
							ParseLocalVariable(lines, ref current, ID, sibling));
					} break;
					case "TAG_member":
					{
						// struct, class, or union member
						allTags.Add(ParseMember(lines, ref current, ID, sibling));
					} break;
					case "TAG_ptr_to_member_type":
					{
						allTags.Add(
							ParsePointerToMemberFunc(
								lines,
								ref current,
								ID,
								sibling));
					} break;
					case "TAG_structure_type":
					{
						allTags.Add(
							ParseStruct(
								lines,
								ref current,
								ID,
								sibling,
								TagType.Struct));
					} break;
					case "TAG_subroutine":
					{
						// (static) function local to CU
						Tag tag = ParseFunction(
								lines,
								ref current,
								ID,
								sibling,
								TagType.CULocalFunc);

						tag.isStatic = true;

						allTags.Add(tag);
					} break;
					case "TAG_subroutine_type":
					{
						// function pointer
						allTags.Add(
							ParseFunctionPointer(
								lines,
								ref current,
								ID,
								sibling));
					} break;
					case "TAG_typedef":
					{
						// static class member
						// Listed in reverse order from .h file
						// OR an actual typedef, but this looks to be unused
						// outside of classes
						allTags.Add(
							ParseTypedef(lines, ref current, ID, sibling));
					} break;
					case "TAG_union_type":
					{
						// union
						allTags.Add(
							ParseStruct(
								lines,
								ref current,
								ID,
								sibling,
								TagType.Union));
					} break;
					case "TAG_padding":
					{
						// What the fuck is wrong with Metrowerks's DWARF
						// implementation. This is either a global variable
						// OR a global function. Of course it can also be
						// static and we only have heuristics to know.
						// Guess who just came back 🤡
						// It turns out that this can also be a local member
						// variable. So we have to go and fix that up in the
						// second pass as well.
						allTags.Add(ParsePadding(lines, ref current, ID, sibling));
					} break;
					default:
					{
						Console.Error.WriteLine("Unknown TAG type: " + tagLine[2]);
					} break;
				}

				IDToIndex.Add(ID, allTags.Count - 1);
				childTags.Add(allTags[allTags.Count - 1]);
			}

			if(lines[current].EndsWith("<4>"))
			{
				int ID = Convert.ToInt32(
					lines[current].Split(
						":",
						StringSplitOptions.RemoveEmptyEntries)[0],
					16);

				Tag endTag = new();
				endTag.ID = ID;
				endTag.sibling = Tag.NoSibling;
				endTag.tagType = TagType.End;

				allTags.Add(endTag);
				IDToIndex.Add(ID, allTags.Count - 1);
				childTags.Add(allTags[allTags.Count - 1]);
			}
		}
	}
}
}
