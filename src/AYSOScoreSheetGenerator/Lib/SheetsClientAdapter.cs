using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Lib
{
	/// <summary>
	/// Because we have to use async methods to get the GoogleCredential, we can't register the <see cref="SheetsClient"/> with dependency injection, so this is a wrapper
	/// </summary>
	public class SheetsClientAdapter : ISheetsClient
	{
		private readonly GoogleCredential _credential;
		private readonly ILogger<SheetsClient> _log;

		private Lazy<SheetsClient> _sheetsClient;

		public SheetsClientAdapter(GoogleCredential credential, ILogger<SheetsClient> log)
		{
			_credential = credential;
			_log = log;

			_sheetsClient = new Lazy<SheetsClient>(() => new SheetsClient(_credential, _log));
		}

		public string SpreadsheetId { get => _sheetsClient.Value.SpreadsheetId; }

		public async Task<Sheet> AddSheet(string title, int? columnCount = null, int? rowCount = null, CancellationToken ct = default)
			=> await _sheetsClient.Value.AddSheet(title, columnCount, rowCount, ct);

		public async Task Append(IList<AppendRequest> data, CancellationToken ct = default)
			=> await _sheetsClient.Value.Append(data, ct);

		public async Task<int> AutoResizeColumn(string sheetName, int columnIndex)
			=> await _sheetsClient.Value.AutoResizeColumn(sheetName, columnIndex);

		public async Task ClearSheet(string sheetName, CancellationToken ct = default)
			=> await _sheetsClient.Value.ClearSheet(sheetName, ct);

		public async Task ClearValues(string range, CancellationToken ct = default)
			=> await _sheetsClient.Value.ClearValues(range, ct);

		public async Task<Spreadsheet> CreateSpreadsheet(string title, CancellationToken ct = default)
			=> await _sheetsClient.Value.CreateSpreadsheet(title, ct);

		public async Task<Spreadsheet> DeleteSheet(string sheetName, CancellationToken ct = default)
			=> await _sheetsClient.Value.DeleteSheet(sheetName, ct);

		public async Task ExecuteRequests(IEnumerable<Request> requests, CancellationToken ct = default)
			=> await _sheetsClient.Value.ExecuteRequests(requests, ct);

		public async Task<Sheet> GetOrAddSheet(string sheetName, int? columnCount = null, int? rowCount = null, CancellationToken ct = default)
			=> await _sheetsClient.Value.GetOrAddSheet(sheetName, columnCount, rowCount, ct);

		public async Task<IList<string>> GetSheetNames(CancellationToken ct = default)
			=> await _sheetsClient.Value.GetSheetNames(ct);

		public async Task<IList<IList<object>>> GetValues(string range, CancellationToken ct = default)
			=> await _sheetsClient.Value.GetValues(range, ct);

		public async Task<Spreadsheet> LoadSpreadsheet(string spreadsheetId, CancellationToken ct = default)
			=> await _sheetsClient.Value.LoadSpreadsheet(spreadsheetId, ct);

		public async Task<Sheet> RenameSheet(string oldName, string newName, CancellationToken ct = default)
			=> await _sheetsClient.Value.RenameSheet(oldName, newName, ct);

		public async Task RenameSpreadsheet(string newName, CancellationToken ct = default)
			=> await _sheetsClient.Value.RenameSpreadsheet(newName, ct);

		public async Task Update(IList<UpdateRequest> data, CancellationToken ct = default)
			=> await _sheetsClient.Value.Update(data, ct);
	}
}
