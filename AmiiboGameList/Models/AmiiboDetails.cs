namespace AmiiboGameList.Models;

public class AmiiboDetails(
	Hex id,
	string originalName,
	Func<string> normalizedNameFactory,
	Func<string> characterFactory,
	Func<string> amiiboSeriesFactory,
	Func<string> figureTypeFactory,
	Func<string> amiiboLifePageUrlFactory)
{
	public Hex Id { get; } = id;
	public string OriginalName { get; } = originalName;
	public string NormalizedName { get; } = normalizedNameFactory();
	public string Character { get; } = characterFactory();
	public string AmiiboSeries { get; } = amiiboSeriesFactory();
	public string FigureType { get; } = figureTypeFactory();
	public string AmiiboLifePageUrl { get; } = amiiboLifePageUrlFactory();
}