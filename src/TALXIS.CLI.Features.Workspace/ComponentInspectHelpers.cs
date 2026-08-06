using TALXIS.CLI.Core;
using TALXIS.Platform.Metadata;
using TALXIS.Platform.Metadata.Components;
using TALXIS.Platform.Metadata.Merging;

namespace TALXIS.CLI.Features.Workspace;

/// <summary>
/// Pure projection logic for <see cref="ComponentInspectCliCommand"/> -
/// turns metadata objects into serializable inspection results.
/// </summary>
internal static class ComponentInspectHelpers
{
    private const int EnglishLanguageCode = 1033;

    public static FormInspectionResult BuildFormResult(FormMetadata form, int? depth)
    {
        var tabs = new List<FormTabNode>();
        foreach (var tab in FindDescendants(form.Body, "tab"))
        {
            List<FormSectionNode>? sections = null;
            if (depth is null || depth >= 2)
            {
                sections = new List<FormSectionNode>();
                foreach (var section in FindDescendants(tab, "section"))
                {
                    List<FormControlNode>? controls = null;
                    if (depth is null || depth >= 3)
                    {
                        controls = new List<FormControlNode>();
                        foreach (var cell in FindDescendants(section, "cell"))
                        {
                            var cellLabel = PickNodeLabel(cell);
                            foreach (var control in FindDescendants(cell, "control"))
                            {
                                controls.Add(new FormControlNode(
                                    control.GetAttribute("id"),
                                    control.GetAttribute("datafieldname"),
                                    control.GetAttribute("classid"),
                                    cellLabel));
                            }
                        }
                    }

                    sections.Add(new FormSectionNode(
                        section.GetAttribute("id"),
                        section.GetAttribute("name"),
                        PickNodeLabel(section),
                        controls));
                }
            }

            tabs.Add(new FormTabNode(
                tab.GetAttribute("id"),
                tab.GetAttribute("name"),
                PickNodeLabel(tab),
                sections));
        }

        return new FormInspectionResult(
            form.FormId,
            form.FormType,
            form.EntityLogicalName,
            PickLabel(form.DisplayName),
            PickLabel(form.Description),
            tabs);
    }

    public static EntityInspectionResult BuildEntityResult(EntityMetadata entity)
    {
        var attributes = entity.Attributes
            .Select(a => new EntityAttributeNode(
                a.LogicalName,
                a.AttributeType.ToString(),
                PickLabel(a.DisplayName),
                a.RequiredLevel.ToString(),
                a.RequiredLevel is RequiredLevel.Required or RequiredLevel.ApplicationRequired or RequiredLevel.SystemRequired,
                a.IsCustomAttribute))
            .ToList();

        return new EntityInspectionResult(
            entity.LogicalName,
            entity.SchemaName,
            PickLabel(entity.DisplayName),
            entity.PrimaryIdAttribute,
            entity.PrimaryNameAttribute,
            entity.Ownership.ToString(),
            entity.IsCustomEntity,
            attributes);
    }

    /// <summary>
    /// Recursively finds all descendants with the given element name.
    /// Does not descend into a match - nested same-name elements are not expected in FormXml.
    /// </summary>
    public static IEnumerable<MergeableNode> FindDescendants(MergeableNode? node, string name)
    {
        if (node is null)
            yield break;

        foreach (var child in node.Children)
        {
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                yield return child;
            }
            else
            {
                foreach (var match in FindDescendants(child, name))
                    yield return match;
            }
        }
    }

    /// <summary>
    /// Reads a node's display label from its <c>labels/label@description</c> children,
    /// preferring English (1033) and falling back to the first label found.
    /// </summary>
    public static string? PickNodeLabel(MergeableNode node)
    {
        var labels = node.Children.FirstOrDefault(c => string.Equals(c.Name, "labels", StringComparison.OrdinalIgnoreCase));
        if (labels is null)
            return null;

        string? first = null;
        foreach (var label in labels.Children.Where(c => string.Equals(c.Name, "label", StringComparison.OrdinalIgnoreCase)))
        {
            var description = label.GetAttribute("description");
            if (description is null)
                continue;
            if (label.GetAttribute("languagecode") == EnglishLanguageCode.ToString())
                return description;
            first ??= description;
        }
        return first;
    }

    public static string? PickLabel(Label? label)
    {
        if (label is null)
            return null;
        if (label.LocalizedLabels.TryGetValue(EnglishLanguageCode, out var english))
            return english;
        return label.Default;
    }

    /// <summary>Normalizes a GUID string (with or without braces) for comparison; null if not a GUID.</summary>
    public static string? NormalizeGuid(string? value)
        => Guid.TryParse(value?.Trim('{', '}'), out var guid) ? guid.ToString("D") : null;

    public static void RenderEntityText(EntityInspectionResult entity)
    {
        OutputWriter.WriteLine($"Entity: {entity.LogicalName} ({entity.DisplayName ?? "no display name"})");
        OutputWriter.WriteLine($"Schema: {entity.SchemaName ?? "?"} | Ownership: {entity.Ownership} | Custom: {(entity.IsCustomEntity ? "yes" : "no")}");
        OutputWriter.WriteLine($"Primary id: {entity.PrimaryIdAttribute} | Primary name: {entity.PrimaryNameAttribute}");
        OutputWriter.WriteLine(string.Empty);

        if (entity.Attributes.Count == 0)
        {
            OutputWriter.WriteLine("No attributes found.");
            return;
        }

        int nameWidth = Math.Clamp(entity.Attributes.Max(a => a.LogicalName?.Length ?? 0), 12, 40);
        int typeWidth = Math.Clamp(entity.Attributes.Max(a => a.Type.Length), 4, 20);
        int requiredWidth = Math.Clamp(entity.Attributes.Max(a => RequiredDisplay(a).Length), 8, 20);

        string header = $"{"Logical Name".PadRight(nameWidth)} | {"Type".PadRight(typeWidth)} | {"Required".PadRight(requiredWidth)} | {"Custom",-6} | Display Name";
        OutputWriter.WriteLine(header);
        OutputWriter.WriteLine(new string('-', header.Length));
        foreach (var attribute in entity.Attributes)
        {
            OutputWriter.WriteLine(
                $"{(attribute.LogicalName ?? "?").PadRight(nameWidth)} | " +
                $"{attribute.Type.PadRight(typeWidth)} | " +
                $"{RequiredDisplay(attribute).PadRight(requiredWidth)} | " +
                $"{(attribute.IsCustomAttribute ? "yes" : ""),-6} | " +
                $"{attribute.DisplayName ?? ""}");
        }
        OutputWriter.WriteLine($"\n{entity.Attributes.Count} attribute(s).");
    }

    private static string RequiredDisplay(EntityAttributeNode attribute)
        => attribute.RequiredLevel == nameof(RequiredLevel.None) ? string.Empty : attribute.RequiredLevel;

    public static void RenderFormText(FormInspectionResult form)
    {
        OutputWriter.WriteLine($"Form: {form.DisplayName ?? "unnamed"} {form.FormId}");
        OutputWriter.WriteLine($"Type: {form.FormType ?? "?"} | Entity: {form.EntityLogicalName ?? "?"}");
        OutputWriter.WriteLine(string.Empty);

        for (int t = 0; t < form.Tabs.Count; t++)
        {
            var tab = form.Tabs[t];
            bool lastTab = t == form.Tabs.Count - 1;
            OutputWriter.WriteLine($"{Branch(lastTab)}Tab: {FirstNonEmpty(tab.Label, tab.Name, tab.Id)}");

            var sections = tab.Sections;
            if (sections is null)
                continue;

            var tabIndent = Indent(lastTab);
            for (int s = 0; s < sections.Count; s++)
            {
                var section = sections[s];
                bool lastSection = s == sections.Count - 1;
                OutputWriter.WriteLine($"{tabIndent}{Branch(lastSection)}Section: {FirstNonEmpty(section.Label, section.Name, section.Id)}");

                var controls = section.Controls;
                if (controls is null)
                    continue;

                var sectionIndent = tabIndent + Indent(lastSection);
                for (int c = 0; c < controls.Count; c++)
                {
                    var control = controls[c];
                    bool lastControl = c == controls.Count - 1;
                    var label = !string.IsNullOrWhiteSpace(control.Label) ? $"  ({control.Label})" : string.Empty;
                    OutputWriter.WriteLine($"{sectionIndent}{Branch(lastControl)}{FirstNonEmpty(control.DataFieldName, control.Id)}{label}");
                }
            }
        }

        static string Branch(bool last) => last ? "└─ " : "├─ ";
        static string Indent(bool last) => last ? "   " : "│  ";
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "?";
}

internal sealed record FormInspectionResult(
    string? FormId,
    string? FormType,
    string? EntityLogicalName,
    string? DisplayName,
    string? Description,
    IReadOnlyList<FormTabNode> Tabs);

internal sealed record FormTabNode(
    string? Id,
    string? Name,
    string? Label,
    IReadOnlyList<FormSectionNode>? Sections);

internal sealed record FormSectionNode(
    string? Id,
    string? Name,
    string? Label,
    IReadOnlyList<FormControlNode>? Controls);

internal sealed record FormControlNode(
    string? Id,
    string? DataFieldName,
    string? ClassId,
    string? Label);

internal sealed record EntityInspectionResult(
    string? LogicalName,
    string? SchemaName,
    string? DisplayName,
    string? PrimaryIdAttribute,
    string? PrimaryNameAttribute,
    string Ownership,
    bool IsCustomEntity,
    IReadOnlyList<EntityAttributeNode> Attributes);

internal sealed record EntityAttributeNode(
    string? LogicalName,
    string Type,
    string? DisplayName,
    string RequiredLevel,
    bool IsRequired,
    bool IsCustomAttribute);
