using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DwarfOne2C
{
public partial class DumpParser
{
	private string[] lines;
	private int start, current;

	public List<Tag> allTags = new();
	public Dictionary<int /* ID */, int /* tagIndex */> IDToIndex = new();

	public DumpParser(string fileName)
	{
		lines = File.ReadAllLines(fileName, Encoding.UTF8);

		for(int i = 0; i < Math.Min(100, lines.Length); ++i)
		{
			if(lines[i].StartsWith("DWARF v1 dump -"))
			{
				start = i + 5;
				break;
			}
		}
	}

	public void ListCompilationUnits()
	{
		for(current = start; current < lines.Length; ++current)
		{
			if(lines[current].EndsWith("TAG_compile_unit"))
			{
				for(;
					current < lines.Length && lines[current] != string.Empty;
					++current)
				{
					string line = lines[current].TrimStart();

					if(line.StartsWith("AT_name"))
					{
						string unitName = line.Substring(9, line.Length - 11);
						Console.WriteLine(unitName);
					}
				}
			}
		}
	}

	public List<RootTag> Parse()
	{
		List<CompilationUnit> units = new(100);

		Parse(lines, current);

		ApplyInitialFixes();

		for(Tag CU = allTags[0];
			CU.sibling != Tag.NoSibling;
			CU = allTags[IDToIndex[CU.sibling]])
		{
			CompilationUnit unit = new(allTags, IDToIndex, CU);
			units.Add(unit);
		}

		return new();
	}

	private void Parse(string[] lines, int current)
	{
		for(; current < lines.Length; ++current)
		{
			if(lines[current].Contains("TAG_"))
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
					case "TAG_compile_unit":
					{
						RootTag unit = ParseCompileUnit(
							lines,
							ref current,
							ID,
							sibling);

						allTags.Add(unit);

						Console.Error.WriteLine(unit.name);
						Console.Error.Flush();
					} break;
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

				// If prev->sibling != ID
				int prevSibling = allTags[allTags.Count - 2].sibling;

				if(prevSibling != ID && prevSibling != sibling)
					allTags[allTags.Count - 2].firstChild = ID;
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
			}
		}
	}

	private void ApplyInitialFixes()
	{
		// Fix the last CU not having a sibling.
		for(Tag CU = allTags[0]; ;)
		{
			int nextIdx;
			if(!IDToIndex.TryGetValue(CU.sibling, out nextIdx))
			{
				CU.sibling = Tag.NoSibling;
				break;
			}

			CU = allTags[IDToIndex[CU.sibling]];
		}

		void FixDirtyTag(Tag tag)
		{
			// Fixup size
			Tag referenced = allTags[IDToIndex[tag.typeID]];

			if(referenced.isDirty)
				FixDirtyTag(referenced);

			int referencedSize = referenced.size * tag.length;
			tag.size = tag.isPointer
				? 4
				: tag.isReference
					? referencedSize
					: tag.size;

			tag.isDirty = false;
		}

		foreach(Tag tag in allTags)
		{
			// Make sure modifiers are in the correct order when writing code.
			tag.modifiers.Reverse();

			// Fixup tags not having a size (yet)
			if(tag.isDirty)
				FixDirtyTag(tag);

			// Fixup sizes of unknown types
			if(tag.typeID == (int)Type.BuiltInType.Unknown)
			{
				Tag sibling = allTags[IDToIndex[tag.sibling]];

				if(sibling.tagType != TagType.End)
					tag.size = sibling.location - tag.location;
			}

			// Fixup weird invalid names
			if(tag.name != null && tag.name.StartsWith('@'))
				tag.name = $"__anon_0x{tag.ID:X}";
		}
	}
}
}
