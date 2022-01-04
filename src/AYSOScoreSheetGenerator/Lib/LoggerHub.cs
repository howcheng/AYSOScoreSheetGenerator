using Microsoft.AspNetCore.SignalR;

namespace AYSOScoreSheetGenerator.Lib
{
	public interface IScoreSheetLogger
	{
		Task ReceiveMessage(string message);
	}

	public class LoggerHub : Hub<IScoreSheetLogger>
	{
		public async Task SendMessage(string message) => await Clients.All.ReceiveMessage(message);
	}
}
