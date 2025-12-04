namespace Tidalarr.Integration.Annotations;

internal enum FieldType
{
    Textbox,
    Number,
    Path,
    Select,
    Checkbox
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class FieldDefinitionAttribute(int order) : Attribute
{
    public int Order { get; } = order; public string Label { get; set; } = string.Empty;
    public FieldType Type { get; set; } = FieldType.Textbox;
    public string? Unit { get; set; }
    public bool Advanced { get; set; }
    public Type? SelectOptions { get; set; }
    public string? HelpText { get; set; }
}

[AttributeUsage(AttributeTargets.Field)]
internal sealed class FieldOptionAttribute : Attribute
{
    public string? Label { get; set; }
}

