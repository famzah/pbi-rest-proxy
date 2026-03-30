namespace PbiRestProxy.Dax;

public sealed record DaxResultColumn(
    int Ordinal,
    string Name,
    string DataTypeName,
    string ClrTypeName,
    string? ClrTypeFullName);
