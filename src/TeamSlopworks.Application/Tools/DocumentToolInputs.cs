namespace TeamSlopworks.Application.Tools;

public sealed record OcrToolInput(Stream PdfStream, string Language);

public sealed record StatisticsToolInput(string Text);

public sealed record StatisticsToolOutput(long WordCount, long CharacterCount);
