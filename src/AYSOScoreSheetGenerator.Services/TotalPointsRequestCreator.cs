using System.Text;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Services
{
	public class TotalPointsRequestCreator : StandingsRequestCreator, IStandingsRequestCreator
	{
		public TotalPointsRequestCreator(FormulaGenerator formGen) 
			: base(formGen, Constants.HDR_TOTAL_PTS)
		{
		}

		public Request CreateRequest(StandingsRequestCreatorConfig config)
		{
			string gamePtsCol = $"{_formulaGenerator.SheetHelper.GetColumnNameByHeader(Constants.HDR_GAME_PTS)}{config.StartGamesRowNum}";
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("={0}", gamePtsCol);
			AddColumnToFormulaIfExists(sb, Constants.HDR_REF_PTS, config.StartGamesRowNum);
			AddColumnToFormulaIfExists(sb, Constants.HDR_VOL_PTS, config.StartGamesRowNum);
			AddColumnToFormulaIfExists(sb, Constants.HDR_SPORTSMANSHIP_PTS, config.StartGamesRowNum);
			AddColumnToFormulaIfExists(sb, Constants.HDR_PTS_DEDUCTION, config.StartGamesRowNum);

			return RequestCreator.CreateRepeatedSheetFormulaRequest(config.SheetId, config.SheetStartRowIndex, _columnIndex, config.NumTeams,
				sb.ToString());
		}

		private void AddColumnToFormulaIfExists(StringBuilder sb, string columnHeader, int startRowNum)
		{
			string col = _formulaGenerator.SheetHelper.GetColumnNameByHeader(columnHeader);
			if (col == null)
				return;

			sb.AppendFormat(" + {0}{1}", col, startRowNum);
		}
	}
}
