using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		// So method 1 turns out to break now that we parse all the information
		// at once. We'll go with method 2 until it breaks again.
		void FixChain()
		{
			bool CanHaveChildren(Tag tag)
			{
				return tag.tagType == TagType.CompileUnit
					|| tag.tagType == TagType.Class
					|| tag.tagType == TagType.GlobalFunc
					|| tag.tagType == TagType.Struct
					|| tag.tagType == TagType.CULocalFunc
					|| tag.tagType == TagType.FunctionPointer
					|| tag.tagType == TagType.Union;
			}

			Dictionary<int, int> refCounts = new();

			for(int i = 0; i < allTags.Count; ++i)
			{
				Tag current = allTags[i];

				if(current.tagType == TagType.End)
					continue;

				int value = 0;

				refCounts.TryGetValue(current.sibling, out value);
				refCounts[current.sibling] = value + 1;
			}

			refCounts.Where(kvp => kvp.Value <= 1)
				.Select(kvp => kvp.Key)
				.All(
					key =>
					{
						refCounts.Remove(key);
						return true;
					});

			foreach(KeyValuePair<int, int> tagRefCount in refCounts)
			{
				int refID = IDToIndex[tagRefCount.Key];
				Tag referenced = allTags[refID];

				int CUIndex = allTags.FindLastIndex(
					refID,
					tag => tag.tagType == TagType.CompileUnit);

				List<Tag> searchRange = allTags
					.GetRange(CUIndex, refID - CUIndex);

				List<Tag> referencingTags = searchRange
					.Where(tag => tag.sibling == tagRefCount.Key)
					.ToList();

				Tag left = referencingTags[0];
				Tag right = referencingTags[1];

				while(true)
				{
					do
					{
						Tag result = searchRange.Find(
							tag => tag.sibling == right.ID);

						if(result == null)
							break;

						right = result;
					} while(true);

					if(left.sibling < right.ID)
					{
						left = allTags[IDToIndex[left.sibling]];
						continue;
					}

					left.sibling = right.ID;

					left = right;
					referencingTags.RemoveAt(0);

					int rightIdx = Math.Min(1, referencingTags.Count - 1);
					right = referencingTags[rightIdx];

					if(rightIdx == 0)
						break;
				}
			}

			// Fix child tags
			for(int i = 1; i < allTags.Count; ++i)
			{
				Tag current = allTags[i];
				Tag prev = allTags[i - 1];

				if(current.tagType == TagType.End)
					continue;

				// Fixup the last compile unit having a sibling that
				// doesn't exist.
				if(current.tagType == TagType.CompileUnit
				   && !IDToIndex.ContainsKey(current.sibling))
					current.sibling = Tag.NoSibling;

				if(prev.sibling != current.ID
				   && CanHaveChildren(prev))
					prev.firstChild = current.ID;
			}
		}

		FixChain();

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
					Recurse(child, depth + 1);
				}
			}
		}

		for(Tag CU = allTags[0];
			CU.sibling != Tag.NoSibling;
			CU = allTags[IDToIndex[CU.sibling]])
		{
			Recurse(CU, 0);
		}

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

		for(Tag CU = allTags[0];
			CU.sibling != Tag.NoSibling;
			CU = allTags[IDToIndex[CU.sibling]])
		{
			// Discriminate global symbols with the same name
			List<string> sameNames = new();

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
