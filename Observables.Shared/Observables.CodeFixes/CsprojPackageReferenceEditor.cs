using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Observables.CodeFixes;

internal static class CsprojPackageReferenceEditor
{
    static readonly XmlWriterSettings WriterSettings = new()
    {
        OmitXmlDeclaration = true,
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\n",
    };

    static readonly Regex TargetFrameworkEquality = new(
        """^['"]\$\(TargetFramework\)['"]\s*==\s*['"]([a-z0-9_.-]+)['"]$""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool ContainsPackageReference(string content, string packageId) =>
        FindReferences(LoadDocument(content), packageId).Any();

    public static string? TryGetPackageVersion(string content, string packageId)
    {
        var versions = FindReferences(LoadDocument(content), packageId)
            .Select(reference => ReadVersion(reference, "Version"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return versions.Length == 1 ? versions[0] : null;
    }

    public static string AddPackageReferenceIfMissing(string content, string packageId, string? version)
    {
        var document = LoadDocument(content);
        if (FindReferences(document, packageId).Any())
            return content;

        var root = document.DocumentElement!;
        var group = ChildElements(root, "ItemGroup")
            .FirstOrDefault(itemGroup => Condition(itemGroup).Length == 0
                && ChildElements(itemGroup, "PackageReference").Any());
        if (group is null)
        {
            group = document.CreateElement("ItemGroup", root.NamespaceURI);
            AppendIndented(root, group);
        }

        var reference = document.CreateElement("PackageReference", root.NamespaceURI);
        reference.SetAttribute("Include", packageId);
        if (version is not null)
            reference.SetAttribute("Version", version);

        AppendIndented(group, reference);
        return SaveDocument(document);
    }

    public static string ReplacePackageReference(string content, string oldPackageId, string newPackageId, string? version) =>
        ReplacePackageReferences(content, [oldPackageId], newPackageId, version);

    public static string ReplacePackageReferences(
        string content, IReadOnlyCollection<string> oldPackageIds, string newPackageId, string? fallbackVersion)
    {
        var document = LoadDocument(content);
        var oldReferences = oldPackageIds.SelectMany(id => FindReferences(document, id)).ToArray();
        if (oldReferences.Length == 0)
            return content;

        var newReferences = FindReferences(document, newPackageId).ToList();
        var hasProjectWideTarget = newReferences.Any(IsProjectWide);
        if (!hasProjectWideTarget && newReferences.Count > 0
            && oldReferences.Any(old => !newReferences.Any(current => SameScope(old, current))
                && newReferences.Any(current => MayOverlap(old, current))))
        {
            // Unmatched conditions could overlap. Leave those declarations for an explicit project edit.
            return content;
        }

        foreach (var reference in oldReferences)
        {
            if (hasProjectWideTarget || newReferences.Any(current => SameScope(reference, current)))
            {
                reference.ParentNode!.RemoveChild(reference);
                continue;
            }

            reference.SetAttribute("Include", newPackageId);
            if (fallbackVersion is not null && ReadVersion(reference, "Version") is null)
                reference.SetAttribute("Version", fallbackVersion);

            newReferences.Add(reference);
        }

        return SaveDocument(document);
    }

    internal static XmlDocument LoadDocument(string content)
    {
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        document.LoadXml(content);
        return document;
    }

    internal static IEnumerable<XmlElement> ChildElements(XmlNode node, string localName) =>
        node.ChildNodes.OfType<XmlElement>().Where(element => element.LocalName == localName);

    internal static string? ReadVersion(XmlElement reference, string name)
    {
        var value = reference.HasAttribute(name) ? reference.GetAttribute(name) :
            ChildElements(reference, name).FirstOrDefault()?.InnerText;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    internal static string Condition(XmlElement element) => element.GetAttribute("Condition").Trim();

    static bool IsProjectWide(XmlElement reference) =>
        reference.ParentNode is XmlElement group && group.ParentNode == reference.OwnerDocument?.DocumentElement
        && Condition(reference).Length == 0 && Condition(group).Length == 0;

    static bool SameScope(XmlElement left, XmlElement right)
    {
        if (left.ParentNode is not XmlElement leftGroup || right.ParentNode is not XmlElement rightGroup
            || leftGroup.ParentNode != rightGroup.ParentNode)
        {
            return false;
        }

        return (Condition(leftGroup) == Condition(rightGroup) && Condition(left) == Condition(right))
            || (GetTargetFramework(left) is { } target
                && string.Equals(target, GetTargetFramework(right), StringComparison.OrdinalIgnoreCase));
    }

    static bool MayOverlap(XmlElement left, XmlElement right) =>
        GetTargetFramework(left) is not { } leftTarget
        || GetTargetFramework(right) is not { } rightTarget
        || string.Equals(leftTarget, rightTarget, StringComparison.OrdinalIgnoreCase);

    static string? GetTargetFramework(XmlElement reference)
    {
        if (reference.ParentNode is not XmlElement group || group.ParentNode != reference.OwnerDocument?.DocumentElement)
            return null;

        var groupCondition = Condition(group);
        var itemCondition = Condition(reference);
        if (groupCondition.Length > 0 && itemCondition.Length > 0)
            return null;

        var match = TargetFrameworkEquality.Match(groupCondition.Length == 0 ? itemCondition : groupCondition);
        return match.Success ? match.Groups[1].Value : null;
    }

    static IEnumerable<XmlElement> FindReferences(XmlDocument document, string packageId) =>
        document.GetElementsByTagName("PackageReference", "*").OfType<XmlElement>()
            .Where(reference => reference.ParentNode?.LocalName == "ItemGroup"
                && string.Equals(reference.GetAttribute("Include"), packageId, StringComparison.OrdinalIgnoreCase));

    static void AppendIndented(XmlElement parent, XmlElement child)
    {
        var closingSpace = parent.LastChild as XmlWhitespace;
        if (closingSpace?.Value?.Contains("\n") == true)
        {
            parent.InsertBefore(parent.OwnerDocument!.CreateWhitespace(closingSpace.Value + "  "), closingSpace);
            parent.InsertBefore(child, closingSpace);
        }
        else
        {
            parent.AppendChild(child);
        }
    }

    static string SaveDocument(XmlDocument document)
    {
        var builder = new StringBuilder();
        using var writer = XmlWriter.Create(builder, WriterSettings);
        document.Save(writer);
        return builder.ToString();
    }
}
