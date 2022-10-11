using System.Collections.Generic;
using System.Linq;

namespace DwarfOne2C
{
public class Node
{
	public Tag tag;
	public List<Node> children;

	public Node(Tag tag)
	{
		this.tag = tag;
		children = new();
	}

	public Node Find(int tagID)
	{
		Node result = children.Find(n => n.tag.ID == tagID);

		if(result != null)
			return result;

		foreach(Node child in children)
		{
			result = child.Find(tagID);

			if(result != null)
				return result;
		}

		return null;
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

		FindStrays(allTags, IDToIndex);

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

		// It can happen that the "child" is a stray tag, try to handle that
		// case here.
		//
		// However we want to skip this if the current node is a root node.
		if(current.tagType != TagType.CompileUnit)
		{
			for(Tag child = allTags[childIdx];
				child.sibling != Tag.NoSibling;
				child = allTags[IDToIndex[child.sibling]])
			{
				// The adjactent tag was a stray tag, skip it.
				if(IDToIndex[child.sibling] >= siblingIdx)
					return;
			}
		}

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

					// Fixup staticness of functions.
					if(child.isStatic && childNode.children.Count > 0)
					{
						Node firstChild = childNode.children[0];
						if(firstChild.tag.name == "this")
							child.isStatic = false;
					}
				}

				// Fixup missing type of functions.
				if(child.typeID == 0
				   && (child.tagType == TagType.GlobalFunc
					   || child.tagType == TagType.CULocalFunc))
					child.typeID = (int)Type.BuiltInType.Void;


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

	void FindStrays(List<Tag> allTags, Dictionary<int, int> IDToIndex)
	{
		Dictionary<int, int> refCounts = new();

		int end = root.tag.sibling == Tag.NoSibling
			? allTags.Count
			: IDToIndex[root.tag.sibling];

		for(int i = IDToIndex[root.tag.ID]; i < end; ++i)
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

		Node FindParent(Node parent, int tagID)
		{
			Node result = parent.children.Find(n => n.tag.ID == tagID);

			if(result != null)
				return parent;

			foreach(Node child in parent.children)
			{
				result = FindParent(child, tagID);

				if(result != null)
					return result;
			}

			return null;
		}

		foreach(KeyValuePair<int, int> tagRefCount in refCounts)
		{
			int refIdx = IDToIndex[tagRefCount.Key];
			Tag referenced = allTags[refIdx];

			int CUIdx = IDToIndex[root.tag.ID];

			List<Tag> searchRange = allTags.GetRange(CUIdx, refIdx - CUIdx);
			List<Tag> referencingTags = searchRange
				.Where(tag => tag.sibling == tagRefCount.Key)
				.ToList();

			System.Diagnostics.Debug.Assert(referencingTags.Count == 2);

			// Look for the referencing ID, as the current key can be an end tag.
			Node parent = FindParent(root, referencingTags[0].ID);

			int leftIdx = parent.children
				.FindIndex(n => n.tag.ID == referencingTags[0].ID);

			Node left = parent.children[leftIdx];

			Tag stray = referencingTags[1];

			while(true)
			{
				Tag result = null;

				// Faster backward search...
				for(int i = IDToIndex[stray.ID]; i > CUIdx; --i)
				{
					Tag current = allTags[i];

					if(current.sibling == stray.ID)
					{
						result = current;
						break;
					}
				}

				if(result == null)
					break;

				stray = result;
			}

			left.tag.sibling = stray.ID;

			List<Node> newChildren = new();

			while(stray.ID != tagRefCount.Key)
			{
				Node right = new(stray);
				AddNodeChildren(allTags, IDToIndex, right);

				newChildren.Add(right);

				stray = allTags[IDToIndex[stray.sibling]];
			}

			parent.children.InsertRange(leftIdx + 1, newChildren);
		}
	}
}
}
