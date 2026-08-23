using System.Text;
using System.Xml;

namespace Observables.CodeFixes;

/// <summary>
/// Adds / replaces / removes <c>&lt;PackageReference /&gt;</c> entries in a csproj using a real XML writer
/// (preserves whitespace and formatting) instead of ad-hoc regex on the text.
/// </summary>
internal static class CsprojPackageReferenceEditor
{
    static readonly XmlWriterSettings WriterSettings = new()
    {
        OmitXmlDeclaration = true,
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\n",
    };

    public static bool ContainsPackageReference(string csprojContent, string packageId) =>
        FindPackageReference(LoadDocument(csprojContent), packageId) is not null;

    public static string? TryGetPackageVersion(string csprojContent, string packageId) =>
        FindPackageReference(LoadDocument(csprojContent), packageId)?.Attributes?["Version"]?.Value;

    public static string AddPackageReferenceIfMissing(string csprojContent, string packageId, string? version)
    {
        var document = LoadDocument(csprojContent);
        if (FindPackageReference(document, packageId) is not null)
        {
            return csprojContent;
        }

        var packageRef = CreatePackageReference(document, packageId, version);
        var itemGroup = FindItemGroupWithPackageReferences(document);
        if (itemGroup is not null)
        {
            itemGroup.AppendChild(packageRef);
        }
        else
        {
            var newGroup = document.CreateElement("ItemGroup");
            newGroup.AppendChild(packageRef);
            document.DocumentElement!.AppendChild(newGroup);
        }

        return SaveDocument(document);
    }

    public static string ReplacePackageReference(string csprojContent, string oldPackageId, string newPackageId, string? version)
    {
        var document = LoadDocument(csprojContent);
        var existingNew = FindPackageReference(document, newPackageId);
        var existingOld = FindPackageReference(document, oldPackageId);
        if (existingNew is not null)
        {
            existingOld?.ParentNode?.RemoveChild(existingOld);
            return SaveDocument(document);
        }

        if (existingOld is not null)
        {
            existingOld.Attributes!["Include"]!.Value = newPackageId;
            if (version is not null)
            {
                if (existingOld.Attributes["Version"] is null)
                {
                    existingOld.Attributes.Append(CreateAttribute(document, "Version", version));
                }
                else
                {
                    existingOld.Attributes["Version"]!.Value = version;
                }
            }

            return SaveDocument(document);
        }

        return AddPackageReferenceIfMissing(csprojContent, newPackageId, version);
    }

    public static string RemovePackageReference(string csprojContent, string packageId)
    {
        var document = LoadDocument(csprojContent);
        var existing = FindPackageReference(document, packageId);
        existing?.ParentNode?.RemoveChild(existing);
        return SaveDocument(document);
    }

    static XmlDocument LoadDocument(string csprojContent)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml(csprojContent);
        return document;
    }

    static string SaveDocument(XmlDocument document)
    {
        var builder = new StringBuilder();
        using var writer = XmlWriter.Create(builder, WriterSettings);
        document.Save(writer);
        return builder.ToString();
    }

    static XmlNode? FindPackageReference(XmlDocument document, string packageId)
    {
        foreach (XmlNode node in document.GetElementsByTagName("PackageReference"))
        {
            if (string.Equals(node.Attributes?["Include"]?.Value, packageId, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    static XmlNode? FindItemGroupWithPackageReferences(XmlDocument document)
    {
        foreach (XmlNode node in document.GetElementsByTagName("ItemGroup"))
        {
            if (node.SelectSingleNode("PackageReference") is not null)
            {
                return node;
            }
        }

        return null;
    }

    static XmlElement CreatePackageReference(XmlDocument document, string packageId, string? version)
    {
        var element = document.CreateElement("PackageReference");
        element.SetAttribute("Include", packageId);
        if (version is not null)
        {
            element.SetAttribute("Version", version);
        }

        return element;
    }

    static XmlAttribute CreateAttribute(XmlDocument document, string localName, string value)
    {
        var attribute = document.CreateAttribute(localName);
        attribute.Value = value;
        return attribute;
    }
}
