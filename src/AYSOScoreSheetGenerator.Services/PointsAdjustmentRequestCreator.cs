using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Services
{
	/// <summary>
	/// Common configuration for all <see cref="PointsAdjustmentRequestCreator"/> classes
	/// </summary>
	public class PointsAdjustmentRequestCreatorConfig : ScoreBasedStandingsRequestCreatorConfig
	{
		/// <summary>
		/// Name of the sheet that holds the points adjustment values
		/// </summary>
		public string? SheetName { get; set; }
		/// <summary>
		/// The current round number
		/// </summary>
		public int RoundNumber { get; set; }
		/// <summary>
		/// Indicates that the value in the sheet is cumulative
		/// </summary>
		public bool ValueIsCumulative { get; set; }
	}

	/// <summary>
	/// Base class for classes that create a <see cref="Request"/> to build a column that applies an adjustment to the game points (e.g., referee or volunteer points); a new instance should be created for every round of games
	/// </summary>
	public abstract class PointsAdjustmentRequestCreator : StandingsRequestCreator, IStandingsRequestCreator
	{
		private PointsAdjustmentRequestCreatorConfig? _config = null;

		protected PointsAdjustmentRequestCreator(FormulaGenerator formGen, string columnHeader) : base(formGen, columnHeader)
		{
		}

		public Request CreateRequest(StandingsRequestCreatorConfig config)
		{
			_config = (PointsAdjustmentRequestCreatorConfig)config;
			string ptsAdjColumn = Utilities.ConvertIndexToColumnName((_config.RoundNumber - 1) + 1); // the column increases every round
			System.Text.RegularExpressions.Regex reTeamSheetCell = new System.Text.RegularExpressions.Regex($@"{_formulaGenerator.SheetHelper.HomeTeamColumnName}(\d+)"); // "A2"
			System.Text.RegularExpressions.Match teamSheetCellMatch = reTeamSheetCell.Match(_config.FirstTeamsSheetCell);
			int firstPtsAdjSheetRowNum = int.Parse(teamSheetCellMatch.Groups[1].Value) + 1; // because in PointsAdjustmentSheetService, we added a row to the top with a note
			string firstPtsAdjCell = $"'{_config.SheetName}'!{ptsAdjColumn}{firstPtsAdjSheetRowNum}"; // "'Ref Pts'!B3"

			string formula;
			if (_config.ValueIsCumulative)
			{
				// =IF(Teams!$C2="",L3,Teams!$C2)
				// when the points for the current round haven't been entered, use the value from last round (or 0 for round 1)
				string ptsToShowIfEmpty = _config.RoundNumber == 1 ? "0" : $"{_columnName}{_config.LastRoundStartRowNum}";
				formula = $"=IF({firstPtsAdjCell}=\"\", {ptsToShowIfEmpty}, {firstPtsAdjCell})";
			}
			else
			{
				// ='Ref Pts'!C2 + L3 (this week's points + the total as of last week)
				string addLastRoundValueFormula = GetAddLastRoundValueFormula(_config.LastRoundStartRowNum);
				formula = $"={firstPtsAdjCell}{addLastRoundValueFormula}";
			}

			Request request = RequestCreator.CreateRepeatedSheetFormulaRequest(_config.SheetId, _config.SheetStartRowIndex, _columnIndex, _config.NumTeams, formula);
			return request;
		}
	}

	/// <summary>
	/// Creates a <see cref="Request"/> for building the column for referee points; a new instance should be created for every round of games
	/// </summary>
	public class RefPointsRequestCreator : PointsAdjustmentRequestCreator
	{
		public RefPointsRequestCreator(FormulaGenerator formGen) : base(formGen, Constants.HDR_REF_PTS)
		{
		}
	}

	/// <summary>
	/// Creates a <see cref="Request"/> for building the column for volunteer points; a new instance should be created for every round of games
	/// </summary>
	public class VolunteerPointsRequestCreator : PointsAdjustmentRequestCreator
	{
		public VolunteerPointsRequestCreator(FormulaGenerator formGen) : base(formGen, Constants.HDR_VOL_PTS)
		{
		}
	}

	/// <summary>
	/// Creates a <see cref="Request"/> for building the column for sportsmanship points; a new instance should be created for every round of games
	/// </summary>
	public class SportsmanshipPointsRequestCreator : PointsAdjustmentRequestCreator
	{
		public SportsmanshipPointsRequestCreator(FormulaGenerator formGen) : base(formGen, Constants.HDR_SPORTSMANSHIP_PTS)
		{
		}
	}

	/// <summary>
	/// Creates a <see cref="Request"/> for building the column for points deductions; a new instance should be created for every round of games
	/// </summary>
	public class PointsDeductionPointsRequestCreator : PointsAdjustmentRequestCreator
	{
		public PointsDeductionPointsRequestCreator(FormulaGenerator formGen) : base(formGen, Constants.HDR_PTS_DEDUCTION)
		{
		}
	}
}
