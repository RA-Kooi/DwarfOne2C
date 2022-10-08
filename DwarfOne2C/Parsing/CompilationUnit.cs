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
}
}
