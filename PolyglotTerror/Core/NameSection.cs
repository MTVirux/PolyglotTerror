namespace PolyglotTerror.Core;

/// <summary>One line of a section, tagged with the field it came from.</summary>
public readonly record struct NameLine(string Tag, string Text, bool Emphasised = false);

/// <summary>One language's block of text, as shown in the name panel.</summary>
public readonly record struct NameSection(string Language, NameLine[] Lines);
