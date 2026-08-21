namespace PolyglotTerror.Core;

/// <summary>One language's block of text, as shown in the name panel.</summary>
public readonly record struct NameSection(string Language, string[] Lines);
