using AYSOScoreSheetGenerator.Objects;

namespace AYSOScoreSheetGenerator.Models
{
	public class UploadModel
	{
		public Dictionary<string, IList<Team>> Divisions { get; set; } = new Dictionary<string, IList<Team>>();
		public ScoreSheetConfiguration SpreadsheetConfiguration { get; set; } = null;
	}
}
