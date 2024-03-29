using System.Collections.Generic;

namespace DwarfOne2C
{
public partial class CWriter
{
	private static List<string> GenerateFunction(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node functionNode,
		int depth)
	{
		Tag current = functionNode.tag;

		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		(string part1, string part2) = GetType(
			allTags,
			IDToIndex,
			CU,
			allCUs,
			functionNode);

		// E.g. (With function pointer)
		// static void (*Foo(int))(int, float);
		// (With member function pointer)
		// static int (Foo::*Bar(float, char))(int, int);
		string line = string.Format(
			"{0}{1}{2}{3}(",
			tabs,
			current.isStatic ? "static " : "",
			part1,
			current.name);

		bool firstLocal = true;

		if(functionNode.children.Count > 0)
		{
			bool hasParams = false;

			int i = 0;

			foreach(Node childNode in functionNode.children)
			{
				Tag child = childNode.tag;

				if(child.name == "this")
					continue;

				string name = child.name != null
					? child.name
					: $"unknown{i++}";

				(string pPart1, string pPart2) = GetType(
					allTags,
					IDToIndex,
					CU,
					allCUs,
					childNode);

				if(child.tagType == TagType.Param)
				{
					hasParams = true;
					line += pPart1 + name + pPart2 + ", ";
				}
				else if(child.tagType == TagType.VariadicParam)
				{
					hasParams = true;
					line += name + ", ";
				}
				else if(child.tagType == TagType.LocalVar)
				{
					if(firstLocal)
					{
						if(hasParams)
							line = line.Remove(line.Length - 2, 2);

						line += ")" + part2;
						code.Add(line);
						code.Add(tabs + "{");
						firstLocal = false;
					}

					line = string.Format(
						"{0}\t{1}{2}{3};",
						tabs,
						pPart1,
						child.name,
						pPart2);

					code.Add(line);
				}
			}

			// We didn't have a local variable;
			if(firstLocal)
			{
				if(hasParams)
					line = line.Remove(line.Length - 2, 2);

				line += ")" + part2;
			}
		}
		else
		{
			line += ")" + part2;
		}

		if(current.references.Count > 0)
		{
			if(firstLocal)
			{
				code.Add(line);
				code.Add("{");
			}

			foreach(int reference in current.references)
			{
				Tag referenced = allTags[IDToIndex[reference]];
				string name = referenced.name != null
					? referenced.name
					: $"unknown_0x{reference:X}";

				line = $"{tabs}\t// References: {name}";

				if(referenced.location != -1)
					line += $" (0x{referenced.location:X})";

				code.Add(line);
			}

			code.Add("}");
		}
		else if(firstLocal)
		{
			line += ';';
			code.Add(line);
		}
		else
		{
			code.Add("}");
		}

		code.Add("");

		return code;
	}

	private static List<string> GenerateMemberFunction(
		List<Tag> allTags,
		Dictionary<int, int> IDToIndex,
		Node CU,
		List<Node> allCUs,
		Node memberFuncNode,
		int depth)
	{
		Tag current = memberFuncNode.tag;

		List<string> code = new();

		string tabs = new('\t', depth);

		if(current.comment != null)
			code.Add(tabs + "// " + current.comment);

		Node referenced = CU.Find(current.typeID);

		if(referenced == null)
		{
			for(int i = 0; i < allCUs.Count; ++i)
			{
				Node unit = allCUs[i];

				if(i < allCUs.Count - 2)
				{
					Node nextUnit = allCUs[i + 1];

					if(nextUnit.tag.ID < current.typeID)
						continue;
				}

				referenced = unit.Find(current.typeID);

				if(referenced != null)
					break;
			}
		}

		// Reforge the tag to generate properly.
		referenced.tag.tagType = TagType.GlobalFunc;
		referenced.tag.name = current.name;

		code.AddRange(
			GenerateFunction(
				allTags,
				IDToIndex,
				CU,
				allCUs,
				referenced,
				depth));

		return code;
	}
}
}
