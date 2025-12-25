using System.Text;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Services
{
	public class TotalPointsRequestCreator : StandingsRequestCreator, IStandingsRequestCreator
	{
		public TotalPointsRequestCreator(FormulaGenerator formGen) 
			: base(formGen, Constants.HDR_TOTAL_PTS)
		{
		}

		protected override string GenerateFormula(StandingsRequestCreatorConfig config)
		{
			string? gamePtsCol = _formulaGenerator.SheetHelper.GetColumnNameByHeader(Constants.HDR_GAME_PTS);
			if (gamePtsCol == null)
				throw new InvalidOperationException($"Column '{Constants.HDR_GAME_PTS}' not found in sheet helper");
			
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("={0}{1}", gamePtsCol, config.StartGamesRowNum);
			AddColumnToFormulaIfExists(sb, Constants.HDR_REF_PTS, config.StartGamesRowNum);
			AddColumnToFormulaIfExists(sb, Constants.HDR_VOL_PTS, config.StartGamesRowNum);
			AddColumnToFormulaIfExists(sb, Constants.HDR_SPORTSMANSHIP_PTS, config.StartGamesRowNum);
			AddColumnToFormulaIfExists(sb, Constants.HDR_PTS_DEDUCTION, config.StartGamesRowNum);
			return sb.ToString();
		}

		private void AddColumnToFormulaIfExists(StringBuilder sb, string columnHeader, int startRowNum)
		{
			string? col = _formulaGenerator.SheetHelper.GetColumnNameByHeader(columnHeader);
			if (col == null)
				return;

			sb.AppendFormat(" + {0}{1}", col, startRowNum);
		}
	}
}
