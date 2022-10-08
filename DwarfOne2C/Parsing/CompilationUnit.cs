using System.Collections.Generic;

namespace DwarfOne2C
{
public class Node
{
	public Tag tag;
	public List<Node> children;

	public Node(Tag tag)
	{
		this.tag = tag;
	}
}

public class CompilationUnit
{
	public Node root;

	public CompilationUnit(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Tag CURoot)
	{
		root = new(CURoot);

		AddNodeChildren(allTags, IDToIndex, root);

		ApplyFixes();
	}

	private void AddNodeChildren(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node node)
	{
		Tag current = node.tag;

		if(current.sibling == Tag.NoSibling)
			return;

		int currentIdx = IDToIndex[current.ID];
		int siblingIdx = IDToIndex[current.sibling];

		if(currentIdx == siblingIdx - 1)
			return;

		int childIdx = currentIdx + 1;

		for(Tag child = allTags[childIdx];
			child.sibling != Tag.NoSibling;
			child = allTags[IDToIndex[child.sibling]])
		{
			if(child.tagType == TagType.End)
				break;

			Node childNode = new(child);

			node.children.Add(childNode);
		}

		// Recurse later, for easier debugging. This is not that expensive
		// anyway.
		foreach(Node child in node.children)
		{
			AddNodeChildren(allTags, IDToIndex, child);
		}
	}

	private void ApplyFixes()
	{
		void FixFunclet(Node parent, int depth)
		{
			foreach(Node childNode in parent.children)
			{
				Tag child = childNode.tag;

				// Fix TAG_padding being member variables, global variables,
				// and global functions at the same time.
				if(child.tagType == TagType.Padding)
				{
					child.tagType = child.isFunction
						? TagType.GlobalFunc
						: depth == 0
							? TagType.GlobalVar
							: TagType.MemberFunc;
				}
				else if(child.tagType == TagType.GlobalFunc)
				{
					if(depth > 0)
					{
						// Remove staticness from functions that are not top
						// level.
						child.isStatic = false;

						// It is also a class/struct member function in this
						// case.
						child.tagType = TagType.MemberFunc;
					}

					// Fixup missing type of functions.
					if(child.typeID == 0)
						child.typeID = (int)Type.BuiltInType.Void;

					// Fixup staticness of functions.
					if(child.isStatic && childNode.children.Count > 0)
					{
						Node firstChild = childNode.children[0];
						if(firstChild.tag.name == "this")
							child.isStatic = false;
					}
				}

				FixFunclet(childNode, depth + 1);
			}
		}

		FixFunclet(root, 0);

		List<string> sameNames = new();

		foreach(Node childNode in root.children)
		{
			Tag child = childNode.tag;

			switch(child.tagType)
			{
			case TagType.CULocalFunc:
			case TagType.Class:
			case TagType.Struct:
			case TagType.Union:
			case TagType.Enum:
			case TagType.GlobalVar:
			case TagType.GlobalFunc:
			case TagType.LocalVar:
			{
				if(sameNames.Contains(child.name))
					child.name = $"{child.name}_0x{child.ID:X}";
				else
					sameNames.Add(child.name);
			} break;
			default:
				continue;
			}
		}
	}
}
}
