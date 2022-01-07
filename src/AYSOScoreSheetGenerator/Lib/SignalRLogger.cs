using Microsoft.AspNetCore.SignalR;

namespace AYSOScoreSheetGenerator.Lib
{
	/// <summary>
	/// Logger class for sending messages to the web browser via SignalR
	/// </summary>
	public class SignalRLogger : ILogger
	{
		private readonly IServiceProvider _provider;

		public SignalRLogger(IServiceProvider provider)
		{
			_provider = provider;
		}

		public IDisposable BeginScope<TState>(TState state) => new LogScope();

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			// Yes, this is the Service Locator anti-pattern, but because SignalR hubs are transient, it can't be helped
			var hub = _provider.GetService<IHubContext<LoggerHub, IScoreSheetLogger>>();
			string msg = formatter(state, exception);
			hub.Clients.All.ReceiveMessage(msg);
		}
	}

	internal class LogScope : IDisposable
	{
		public void Dispose() { }
	}

	public class SignalRLoggerProvider : ILoggerProvider
	{
		private SignalRLogger _logger;
		private readonly IServiceCollection _services;

		public SignalRLoggerProvider(IServiceCollection services)
		{
			_services = services; ;
		}

		public ILogger CreateLogger(string categoryName)
		{
			if (_logger == null)
			{
				_logger = new SignalRLogger(_services.BuildServiceProvider());
			}

			return _logger;
		}

		public void Dispose() { }
	}
	
	public static class ILoggingBuilderExtensions
	{
		public static ILoggingBuilder AddSignalRLogging(this ILoggingBuilder builder)
			=> builder.AddProvider(new SignalRLoggerProvider(builder.Services))
				.AddFilter<SignalRLoggerProvider>($"{nameof(AYSOScoreSheetGenerator)}.{nameof(Services)}", LogLevel.Information)
				.AddFilter<SignalRLoggerProvider>(nameof(GoogleSheetsHelper), LogLevel.Information);
	}
}
