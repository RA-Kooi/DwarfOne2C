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

	public List<Tag> allTags = new();
	public Dictionary<int /* ID */, int /* tagIndex */> IDToIndex = new();

	public void FirstPass(string[] lines, int current)
	{
		allTags.Add(this);

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
								Tag.TagType.Class));
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
							Tag.TagType.GlobalFunc);

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
								Tag.TagType.Struct));
					} break;
					case "TAG_subroutine":
					{
						// (static) function local to CU
						Tag tag = ParseFunction(
								lines,
								ref current,
								ID,
								sibling,
								Tag.TagType.CULocalFunc);

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
								Tag.TagType.Union));
					} break;
					case "TAG_padding":
					{
						// What the fuck is wrong with Metrowerks's DWARF
						// implementation. This is either a global variable
						// OR a global function. Of course it can also be
						// static and we only have heuristics to know.
						// Guess who just came back ????
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
				endTag.tagType = Tag.TagType.End;

				allTags.Add(endTag);
				IDToIndex.Add(ID, allTags.Count - 1);
			}
		}
	}

	public void SecondPass()
	{
		// It is possible that our linked list has gaps, thanks to MetroWerksn't
		// amazing DWARF implementation, and thus we need to detect these gaps
		// and fix them up by connecting them...
		// We essentially have 3 ways to fix this:
		// 1) We go through the list of tags and check if the previous tag has
		//    the current tag as its sibling, and then fix it up unless it's a
		//    child tag.
		// 2) We traverse the tags recursively and for each tag we check if it
		//    it is referenced more than once. If it is we backwalk until we
		//    find a tag that isn't referenced and then fix up the chain.
		// 3) We traverse the list of tags and for each tag we check if it has
		//    no references to it. If it doesn't, follow its chain downwards
		//    and check where the tag with multiple references is, and then fix
		//    up the chain.
		// Method 2 is probably the most correct method, but that has a
		// computational explosion issue. Method 3 is the easiest to implement,
		// but it isn't as thorough.
		// Method 1 is the fastest, but is the least thorough.
		// Going with method 1 for now until I find it breaks again.
		int idx = 1;
		void FixChain(Tag parent, ref int i, int depth)
		{
			int lastIdx = 0;
			for(; i < allTags.Count; ++i)
			{
				Tag current = allTags[i];
				Tag prev = allTags[lastIdx];

				lastIdx = i;

				if(current.tagType == TagType.End)
					return;

				if(prev.sibling != current.ID)
					prev.sibling = current.ID;

				if(current.firstChild >= 0)
				{
					++i;
					FixChain(current, ref i, depth + 1);
					continue;
				}
			}
		}

		FixChain(this, ref idx, 0);

		void Recurse(Tag parent, int depth)
		{
			if(parent.firstChild == -1)
				return;

			for(Tag child = allTags[IDToIndex[parent.firstChild]];
				child.sibling != Tag.NoSibling;
				child = allTags[IDToIndex[child.sibling]])
			{
				// Fixup TAG_padding being member variables, global variables,
				// and global functions at the same time...
				if(child.tagType == TagType.Padding)
				{
					if(depth == 0)
						child.tagType = child.isFunction
							? TagType.GlobalFunc
							: TagType.GlobalVar;
					else
						child.tagType = child.isFunction
							? TagType.GlobalFunc
							: TagType.Member;

					if(child.sibling == parent.sibling)
					{
						child.sibling = Tag.NoSibling;
						break;
					}
				}
				else if(child.tagType == TagType.GlobalFunc)
				{
					if(depth > 0)
					{
						// Remove staticness from functions that are
						// not top level
						child.isStatic = false;

						// It is also a class/struct member function
						// in this case
						child.tagType = TagType.MemberFunc;
					}
				}

				if((child.tagType == TagType.Class
					|| child.tagType == TagType.Struct)
				   && child.firstChild != -1)
				{
					Recurse(child, 1);
				}
			}
		}

		Recurse(allTags[0], 0);

		foreach(Tag tag in allTags)
		{
			tag.modifiers.Reverse();

			if(tag.isDirty)
			{
				// Fixup size
				int index = IDToIndex[tag.typeID];
				Tag referenced = allTags[index];

				if(referenced.isDirty)
					throw new NotImplementedException("referenced is dirty");

				int referencedSize = referenced.size * length;
				tag.size = tag.isPointer
					? 4
					: tag.isReference
					? referencedSize
					: tag.size;
			}

			// Fixup staticness of functions
			if(tag.isStatic
			   && tag.tagType == TagType.GlobalFunc
			   && tag.firstChild > -1)
			{
				Tag child = allTags[IDToIndex[tag.firstChild]];
				if(child.name == "this")
					tag.isStatic = false;
			}

			// Fixup missing type of functions
			if((tag.tagType == TagType.GlobalFunc
				|| tag.tagType == TagType.CULocalFunc)
			   && tag.typeID == 0)
			{
				tag.typeID = (int)Type.BuiltInType.Void;
			}

			// Fixup sizes of unknown types
			if(tag.typeID == (int)Type.BuiltInType.Unknown)
			{
				Tag sibling = allTags[IDToIndex[tag.sibling]];
				if(sibling.tagType != TagType.End)
				{
					tag.size = sibling.location - tag.location;
				}
			}

			// Fixup weird invalid names
			if(tag.name != null && tag.name.StartsWith('@'))
			{
				tag.name = $"__anon_0x{tag.ID:X}";
			}
		}

		// Discriminate global symbols with the same name
		List<string> sameNames = new();

		for(Tag tag = allTags[IDToIndex[this.firstChild]];
			tag.sibling != Tag.NoSibling;
			tag = allTags[IDToIndex[tag.sibling]])
		{
			switch(tag.tagType)
			{
			case Tag.TagType.CULocalFunc:
			case Tag.TagType.Class:
			case Tag.TagType.Enum:
			case Tag.TagType.GlobalVar:
			case Tag.TagType.GlobalFunc:
			case Tag.TagType.Struct:
			case Tag.TagType.LocalVar:
			{
				if(sameNames.Contains(tag.name))
					tag.name = $"{tag.name}_0x{tag.ID:X}";
				else
					sameNames.Add(tag.name);
			} break;
			default:
				continue;
			}
		}
	}
}
}
