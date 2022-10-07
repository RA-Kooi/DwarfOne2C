using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DwarfOne2C
{
class DumpParser
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

	public HashSet<CompilationUnit> Parse()
	{
		// Parse global variables
		//	if var has AT_lo_user -> static class/struct variable (C++)
		//	else append to globals list

		HashSet<CompilationUnit> units = new(100);

		for(current = start; current < lines.Length; ++current)
		{
			if(lines[current].EndsWith("TAG_compile_unit"))
			{
				CompilationUnit unit = new(
					allTags,
					IDToIndex,
					lines,
					ref current);

				Console.Error.WriteLine(unit.name);
				Console.Error.Flush();

				unit.Parse(lines, current);

				units.Add(unit);
			}
		}

		ApplyFixes();

		return units;
	}

	private void ApplyFixes()
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
		void FixChain(Tag parent, ref int i, int depth)
		{
			int lastIdx = 0;
			for(; i < allTags.Count; ++i)
			{
				Tag current = allTags[i];
				Tag prev = allTags[lastIdx];

				lastIdx = i;

				if(current.tagType == TagType.End && depth > 0)
					return;
				else if(current.tagType == TagType.End)
					continue;

				// Fixup the last compile unit having a sibling that doesn't
				// exist.
				if(current.tagType == TagType.CompileUnit
				   && !IDToIndex.ContainsKey(current.sibling))
					current.sibling = Tag.NoSibling;

				if(prev.sibling != current.ID
				   && prev.tagType != TagType.CompileUnit)
					prev.sibling = current.ID;

				if(current.firstChild >= 0)
				{
					++i;
					FixChain(current, ref i, depth + 1);
					continue;
				}
			}
		}

		int idx = 0;
		FixChain(allTags[0], ref idx, 0);

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

		void FixDirtyTag(Tag tag)
		{
			// Fixup size
			int index = IDToIndex[tag.typeID];
			Tag referenced = allTags[index];

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
			tag.modifiers.Reverse();

			if(tag.isDirty)
			{
				FixDirtyTag(tag);
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

		for(Tag CU = allTags[0];
			CU.sibling != Tag.NoSibling;
			CU = allTags[IDToIndex[CU.sibling]])
		{
			for(Tag tag = allTags[IDToIndex[CU.firstChild]];
				tag.sibling != Tag.NoSibling;
				tag = allTags[IDToIndex[tag.sibling]])
			{
				switch(tag.tagType)
				{
				case TagType.CULocalFunc:
				case TagType.Class:
				case TagType.Enum:
				case TagType.GlobalVar:
				case TagType.GlobalFunc:
				case TagType.Struct:
				case TagType.LocalVar:
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
}
