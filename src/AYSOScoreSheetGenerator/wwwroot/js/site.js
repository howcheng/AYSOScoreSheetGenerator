// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

var spreadsheetId;

var connection = new signalR.HubConnectionBuilder().withUrl("/hub").build();
connection.serverTimeoutInMilliseconds = 300000;

connection.on("ReceiveMessage", function (message) {
	if ($('#log-messages').length === 0)
		return;

	var value = $('#log-messages').val();
	value += "\n" + message;
	$('#log-messages').val(value);

	// grab the spreadsheet ID when it shows up
	const searchPhrase = "spreadsheet with ID";
	if (!spreadsheetId && message.indexOf(searchPhrase) > -1) {
		var idx = value.indexOf(searchPhrase) + searchPhrase.length + 1;
		var idx2 = value.indexOf(" ", idx);
		spreadsheetId = value.substr(idx, idx2 - idx1);
	}

	// look for the phrase signalling that we are all done or if there was a problem
	if (message === "All done!" || message.startsWith("Uh-oh")) {
		$('#spreadsheet-link').prop('href', 'https://docs.google.com/spreadsheets/d/' + spreadsheetId + '/edit#gid=0');
		$('#spreadsheet-link-para').show();
		$('#spinner').hide();
	}
});

//connection.start().catch(function (err) { console.log(err.toString()); });