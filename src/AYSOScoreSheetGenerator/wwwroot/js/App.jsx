const AppContext = React.createContext({
	divisions: {},
	divisionConfigurations: {},
	setValue: () => { },
	spreadsheetConfiguration: {}
});

class App extends React.Component {
	constructor(props) {
		super(props);
		this.setValue = (key, value) => {
			if (typeof (value) === "object")
				console.log(key + " = " + JSON.stringify(value));
			else
				console.log(key + " = " + value);	
			this.setState(state => ({
				[key]: value
			}));
		}
		this.getAccordionButton = (step, text, target) => {
			// if the user hasn't gotten to the current step yet, they aren't allowed to expand the section
			const expanded = step === this.state.currentStep ? "true" : "false";
			const buttonAttrs = {
				'aria-expanded': expanded,
				'aria-controls': target
			};
			return (
				<button className={`accordion-button ${step === this.state.currentStep ? "" : "collapsed"}`} type="button" {...buttonAttrs}>
					{text}
				</button>
			);
		};
		this.getAccordionClass = (step) => { return `accordion-collapse collapse ${step === this.state.currentStep ? "show" : ""}`; };

		this.prevStep = this.prevStep.bind(this);
		this.nextStep = this.nextStep.bind(this);
		this.handleChange = this.handleChange.bind(this);
		this.handleSubmit = this.handleSubmit.bind(this);

		this.state = {
			currentStep: 1,
			setValue: this.setValue,
			getAccordionButton: this.getAccordionButton,
			getAccordionClass: this.getAccordionClass,
			divisionConfigurations: [],
			spreadsheetConfiguration: {},
			formSubmitted: false
		};
	}

	prevStep() {
		const currentStep = this.state.currentStep;
		const newStep = currentStep - 1;
		this.setState({ currentStep: newStep });
	}

	nextStep() {
		const currentStep = this.state.currentStep;
		const newStep = currentStep + 1;
		this.setState({ currentStep: newStep });
	}

	handleChange(input, value) {
		this.setState({ [input]: value });
	}

	handleSubmit() {
		connection.start().catch(function (err) { console.log(err.toString()); });

		// massage the data to match our POCOs
		const gameDatesArr = this.state.spreadsheetConfiguration.GameDates.split(', ').map((date) => $.datepicker.formatDate('yy-mm-ddT00:00:00Z', $.datepicker.parseDate('m/d/yy', date)));

		const divisionNames = Object.keys(this.state.divisions);
		const divisionConfigurations = [];
		const divisions = {};
		divisionNames.map((divisionName) => {
			// teams
			const divisionTeams = this.state.divisions[divisionName].teams;
			divisions[divisionName] = [];
			divisionTeams.map((team) => {
				divisions[divisionName].push({
					teamName: team.name,
					divisionName: divisionName,
					programName: team.program
				});
			});

			// division configurations
			let divisionConfig = this.state.divisionConfigurations[divisionName];
			if (divisionConfig === null)
				divisionConfig = {
					PracticeRounds: 0
				};
			const formFriendlyName = divisionName.convertToFormFriendly();

			let otherProgramName = null;
			if (this.state.spreadsheetConfiguration.HasOtherPrograms) {
				for (var i = 0; i < divisionTeams.length; i++) {
					if (divisionTeams[i].program !== this.state.spreadsheetConfiguration.ProgramName) {
						otherProgramName = divisionTeams[i].program;
						break;
					}
				}
			}

			const config = {
				divisionName: divisionName,
				hasFriendlyGamesEachRound: String(true) === divisionConfig[`HasFriendlyGamesEachRound-${formFriendlyName}`],
				roundsThatCountTowardsStandings: gameDatesArr.length - (typeof (divisionConfig.PracticeRounds) === "undefined" ? 0 : divisionConfig.PracticeRounds),
				programNameForOtherRegions: otherProgramName,
				includeOtherRegionsInStandings: String(true) === divisionConfig[`IncludeOtherRegionsInStandings-${formFriendlyName}`]
			};
			divisionConfigurations.push(config);
		});

		// score sheet configuration
		const scoreSheetConfiguration = {
			spreadsheetId: this.state.spreadsheetConfiguration.SpreadsheetId,
			spreadsheetTitle: this.state.spreadsheetConfiguration.SpreadsheetTitle,
			programName: this.state.spreadsheetConfiguration.ProgramName,
			divisions: divisionNames,
			gameDates: gameDatesArr,
			divisionConfigurations: divisionConfigurations
		};
		if (this.state.spreadsheetConfiguration.RefereePoints) {
			scoreSheetConfiguration.refPointsSheetConfiguration = {
				valueIsCumulative: String(true) === this.state.spreadsheetConfiguration.RefereePointsAreCumulative,
				affectsStandings: this.state.spreadsheetConfiguration.RefereePointsAffectStandings
			};
		}
		if (this.state.spreadsheetConfiguration.VolunteerPoints) {
			scoreSheetConfiguration.volunteerPointsSheetConfiguration = {
				valueIsCumulative: String(true) === this.state.spreadsheetConfiguration.VolunteerPointsAreCumulative,
				affectsStandings: this.state.spreadsheetConfiguration.VolunteerPointsAffectStandings
			};
		}
		if (this.state.spreadsheetConfiguration.SportsmanshipPoints) {
			scoreSheetConfiguration.sportsmanshipPointsSheetConfiguration = {
				valueIsCumulative: String(true) === this.state.spreadsheetConfiguration.SportsmanshipPointsAreCumulative,
				affectsStandings: this.state.spreadsheetConfiguration.SportsmanshipPointsAffectStandings
			};
		}
		if (this.state.spreadsheetConfiguration.PointsDeductions) {
			scoreSheetConfiguration.pointsDeductionSheetConfiguration = {
				valueIsCumulative: String(true) === this.state.spreadsheetConfiguration.PointsDeductionsAreCumulative,
				affectsStandings: this.state.spreadsheetConfiguration.PointsDeductionsAffectStandings
			};
		}

		const data = {
			divisions: divisions,
			spreadsheetConfiguration: scoreSheetConfiguration
		};
		$.ajax({
			type: 'POST',
			url: '/api/Services',
			data: JSON.stringify(data),
			contentType: 'application/json',
			dataType: 'json'
		});

		this.setState({ formSubmitted: true });
	}

	render() {
		if (this.state.formSubmitted) {
			return (
				<div className="container-fluid">
					<h1 className="display-1 text-center">Working... <span id="spinner" className="spinner-border" role="status"></span></h1>
					<div className="row">
						<div className="col-12 text-center">
							<textarea id="log-messages" disabled readOnly className="form-control" style={{ width: "70%", height: "50%", minHeight: "500px", margin: "0 auto" }}></textarea>
						</div>
						<div className="col-12">
							<p id="spreadsheet-link-para" style={{ display: "none" }} className="text-center mt-3"><a id="spreadsheet-link" className="btn btn-primary" target="_blank">View spreadsheet</a></p>
						</div>
					</div>
				</div>
			);
		} else {
			return (
				<div id="App" className="accordion">
					<AppContext.Provider value={this.state}>
						<UploadFile stepNum={1} nextStep={this.nextStep} />
						<FileLoadConfirmation stepNum={2} prevStep={this.prevStep} nextStep={this.nextStep} />
						<DivisionConfiguration stepNum={3} prevStep={this.prevStep} nextStep={this.nextStep} handleChange={(e) => this.handleChange('divisionConfigurations', e)} />
						<SpreadsheetConfiguration stepNum={4} prevStep={this.prevStep} nextStep={this.nextStep} handleChange={(e) => this.handleChange('spreadsheetConfiguration', e)} />
						<ReviewAndSubmit stepNum={5} prevStep={this.prevStep} handleSubmit={this.handleSubmit} />
					</AppContext.Provider>
				</div>
			);
		}
	}
}

String.prototype.trimQuoteMarks = function () {
	return this.trim().replaceAll('"', '');
}
String.prototype.convertToFormFriendly = function () {
	return this.replaceAll(' ', '-');
}

class AccordionItem extends React.Component {
	static contextType = AppContext;
	render() {
		const headerId = `step${this.props.stepNum}`;
		const collapseId = `collapse${this.props.stepNum}`;
		return (
			<div className="accordion-item" id={`accordion${this.props.stepNum}`}>
				<h2 className="accordion-header" id={headerId}>
					{this.context.getAccordionButton(this.props.stepNum, `Step ${this.props.stepNum}: ${this.props.headerText}`, collapseId)}
				</h2>
				<div id={collapseId} className={this.context.getAccordionClass(this.props.stepNum)} aria-labelledby={headerId} data-bs-parent="#App">
					{this.props.content}
				</div>
			</div>
		);
	}
}

class UploadFile extends React.Component {
	static contextType = AppContext;
	constructor(props) {
		super(props);
		this.handleSubmit = this.handleSubmit.bind(this);
		this.fileInput = React.createRef();
	}

	handleSubmit(event) {
		event.preventDefault();

		let divisions = {}, self = this;
		const reader = new FileReader();
		reader.onload = function (e) {
			const contents = e.target.result;
			const lines = contents.split('\r\n');
			const programNames = [];
			lines.map(line => {
				if (line) {
					const lineArr = line.split(',');
					const programName = lineArr[0].trimQuoteMarks();
					if (programName !== "Program Name") { // indicates the header row
						const divisionName = lineArr[1].trimQuoteMarks();
						const teamName = lineArr[2].trimQuoteMarks();

						let division = null;
						if (typeof (divisions[divisionName]) === "undefined") {
							division = { teams: [] };
							divisions[divisionName] = division;
						} else {
							division = divisions[divisionName];
						}

						const team = {
							name: teamName,
							division: divisionName,
							program: programName
						};
						division.teams.push(team);
						if (programNames.indexOf(programName) === -1)
							programNames.push(programName);
					}
				}
			});
			self.context.setValue('divisions', divisions);

			// if there is only one program name in the file, then there is no interregional play and we won't need to collect this later
			const spreadsheetConfiguration = {
				HasOtherPrograms: programNames.length > 1
			};
			if (programNames.length === 1)
				spreadsheetConfiguration.ProgramName = programNames[0];
			self.context.setValue('spreadsheetConfiguration', spreadsheetConfiguration);
			self.props.nextStep();
		}
		reader.readAsText(this.fileInput.current.files[0]);
	}

	render() {
		const content = (
			<form onSubmit={this.handleSubmit}>
				<div className="accordion-body">
					<div className="row">
						<div className="col-auto">
							<label htmlFor="file" className="col-form-label">Upload file:</label>
						</div>
						<div className="col-auto">
							<input type="file" ref={this.fileInput} id="file" className="form-control" />
						</div>
						<div className="col-auto">
							<button type="submit" className="btn btn-primary">Submit</button>
						</div>
					</div>
					<p>Instructions:</p>
					<ul>
						<li>In SportsConnect, generate the &quot;Team Detail Report&quot; and export it as a CSV file.</li>
						<li>Using a text editor like Notepad, delete all the lines for the teams that will be excluded from the score sheet (e.g., 8U and under).
							You can also use Excel for this, but be sure to save the file as CSV and not in Excel format.
							(You can delete the header row if you want, but it&apos;s not necessary; the application will ignore it if you leave it in.)
						</li>
						<li>If teams in your region will be playing against opponents that are not in your program (e.g., teams from other regions) and you need to record
							scores from those games, you will need to add them into your file.
							<ul>
								<li>Be sure to enter them with a different program name; it doesn&apos;t matter what name you choose, but make it consistent (e.g., &quot;Interregional play&quot;, &quot;Area 10E&quot;)
									for all opponents in a given division. Only the first three columns are required: program name, division, and team name.</li>
								<li>Keep all of teams in your region grouped together. Don't intermix them with the teams from other regions.</li>
							</ul>
						</li>
						<li>After you are done manipulating your file, upload it here.</li>
					</ul>
				</div>

			</form>
		);
		return (
			<AccordionItem stepNum={this.props.stepNum} content={content} headerText="Select file" />
		);
	}
}

class TeamEntry extends React.Component {
	render() {
		const team = this.props.team;
		return (
			<li>{team.name} ({team.program})</li>
		);
	}
}

class TeamList extends React.Component {
	render() {
		let teams = this.props.teams;
		const divisionName = this.props.divisionName;
		const lines = [];

		teams.sort((a, b) => a.name > b.name ? 1 : -1);
		teams.map((team, index) => {
			lines.push(<TeamEntry key={index} team={team} />);
		});

		return (
			<div className="card">
				<div className="card-header">{divisionName} ({teams.length} teams)</div>
				<div className="card-body">
					<ul>
						{lines}
					</ul>
				</div>
			</div>
		);
	}
}

class FileLoadConfirmation extends React.Component {
	static contextType = AppContext;

	componentDidUpdate() {
		if (this.props.stepNum === this.context.currentStep)
			document.getElementById(`accordion${this.props.stepNum}`).scrollIntoView();
	}

	render() {
		const divisions = this.context.divisions;
		const lines = [];
		let keys = [];
		if (divisions) {
			keys = Object.keys(divisions);
			keys.sort();
			for (var i = 0; i < keys.length; i++) {
				const divisionName = keys[i];
				const teams = divisions[divisionName].teams;
				lines.push(<TeamList key={divisionName} divisionName={divisionName} teams={teams} />);
			}
		}
		const content = (
			<div className="accordion-body">
				<p>Found {keys.length} divisions in the uploaded file.</p>
				{lines}
				<p>If this looks good, click &quot;Next&quot; to continue, or &quot;Previous&quot; to reupload the file.</p>
				<p className="text-end">
					<button className="btn btn-link" onClick={this.props.prevStep}>Previous</button>
					<button className="btn btn-primary" onClick={this.props.nextStep}>Next</button>
				</p>
			</div>
		);
		return (
			<AccordionItem stepNum={this.props.stepNum} content={content} headerText="Confirm divisions and teams" />
		);
	}
}

class DivisionConfigurator extends React.Component {
	render() {
		const oddNumberOfTeams = (this.props.teams.length % 2) === 1;
		let friendlyGames = "";

		let programNames = [];
		for (var i = 0; i < this.props.teams.length; i++) {
			const team = this.props.teams[i];
			if (programNames.indexOf(team.program) > -1)
				continue;
			programNames.push(team.program);
		}
		let otherRegions = "";
		if (programNames.length > 1) {
			const fieldName = `IncludeOtherRegionsInStandings-${this.props.divisionName.convertToFormFriendly()}`;
			otherRegions = (
				<div className="mb-3">
					<p>This division contains teams from other regions. Should they be included in the standings table?</p>
					<div className="form-check">
						<input className="form-check-input" type="radio" name={fieldName} id={`${fieldName}1`} value="false" defaultChecked="true" onChange={this.props.handleChange} />
						<label className="form-check-label" htmlFor={`${fieldName}1`}>No</label>
					</div>
					<div className="form-check">
						<input className="form-check-input" type="radio" name={fieldName} id={`${fieldName}2`} value="true" onChange={this.props.handleChange} />
						<label className="form-check-label" htmlFor={`${fieldName}2`}>Yes</label>
					</div>
				</div>
			);
		}

		if (oddNumberOfTeams) {
			const fieldName = `HasFriendlyGamesEachRound-${this.props.divisionName.convertToFormFriendly()}`;
			friendlyGames = (
				<div className="mb-3">
					<p>There are an odd number of teams in this division. How will this be handled?</p>
					<div className="form-check">
						<input className="form-check-input" type="radio" name={fieldName} id={`${fieldName}1`} value="false" defaultChecked="true" onChange={this.props.handleChange} />
						<label className="form-check-label" htmlFor={`${fieldName}1`}>
							One team will have a &quot;bye&quot; week (no game)
						</label>
					</div>
					<div className="form-check">
						<input className="form-check-input" type="radio" name={fieldName} id={`${fieldName}2`} value="true" onChange={this.props.handleChange} />
						<label className="form-check-label" htmlFor={`${fieldName}2`}>
							One team will play a second game each round (a &quot;doubleheader&quot;) against the additional team, which will not count towards standings (a &quot;friendly&quot;)
						</label>
					</div>
				</div>
			);
		}
		return (
			<div className="card">
				<div className="card-header">{this.props.divisionName}</div>
				<div className="card-body">
					<div className="mb-3">
						<label htmlFor="PracticeRounds" className="form-label">How many rounds are practice games (scrimmages) and don&apos;t count for standings?</label>
						<input type="text" className="form-control" name="PracticeRounds" onChange={this.props.handleChange} />
						<div className="form-text">For example, if there are 10 rounds in the season and the first 2 are practice games, then enter 2 here. If all rounds count for standings, then leave this blank.</div>
					</div>
					{friendlyGames}
					{otherRegions}
				</div>
			</div>
		);
	}
}

class DivisionConfiguration extends React.Component {
	static contextType = AppContext;
	constructor(props) {
		super(props);
		this.handleChange = this.handleChange.bind(this);
		this.handleSubmit = this.handleSubmit.bind(this);
		this.alreadyScrolled = React.createRef();
		this.alreadyScrolled = false;

		let configs = {};
		for (var division in props.divisions) {
			configs[division] = {};
		}
		this.state = {
			divisionConfigurations: configs
		};
	}

	handleChange(divisionName, e) {
		const config = typeof (this.state.divisionConfigurations[divisionName]) === "undefined" ? {} : this.state.divisionConfigurations[divisionName];
		config[e.target.name] = e.target.value;
		const configs = this.state.divisionConfigurations;
		configs[divisionName] = config;
		this.setState({ divisionConfigurations: configs });
	}

	handleSubmit() {
		this.alreadyScrolled = false;
		this.context.setValue('divisionConfigurations', this.state.divisionConfigurations);
		this.props.nextStep();
	}

	componentDidUpdate() {
		if (this.props.stepNum === this.context.currentStep) {
			if (!this.alreadyScrolled) {
				document.getElementById(`accordion${this.props.stepNum}`).scrollIntoView();
			}
			this.alreadyScrolled = true;
		}
	}

	render() {
		const divisions = this.context.divisions;
		const divisionContent = [];
		let keys = [];
		if (divisions) {
			keys = Object.keys(divisions);
			keys.sort();
			for (var i = 0; i < keys.length; i++) {
				const divisionName = keys[i];
				const teams = divisions[divisionName].teams;
				divisionContent.push(<DivisionConfigurator key={divisionName} divisionName={divisionName} teams={teams} handleChange={(e) => this.handleChange(divisionName, e)} />);
			}
		}
		const content = (
			<div className="accordion-body">
				<form>
					{divisionContent}
				</form>
				<p className="text-end mt-3">
					<button className="btn btn-link" onClick={this.props.prevStep}>Previous</button>
					<button className="btn btn-primary" onClick={this.handleSubmit}>Next</button>
				</p>
			</div>
		);
		return (
			<AccordionItem stepNum={this.props.stepNum} content={content} headerText="Configure divisions" />
		);
	}
}

class SpreadsheetConfiguration extends React.Component {
	static contextType = AppContext;
	constructor(props) {
		super(props);
		this.handleChange = this.handleChange.bind(this);
		this.handleCheckbox = this.handleCheckbox.bind(this);
		this.handleSubmit = this.handleSubmit.bind(this);
		this.alreadyScrolled = React.createRef();
		this.alreadyScrolled = false;
		// these next two are for keeping track of things that were set in the UploadFile step
		this.hasOtherPrograms = React.createRef();
		this.hasOtherPrograms = false;
		this.programName = React.createRef();
		this.programName = null;

		this.state = {};
	}

	handleChange(e) {
		var value = e.target.value;
		this.setState({ [e.target.name]: value });
	}

	handleCheckbox(e) {
		var value = e.target.checked;
		this.setState({ [e.target.name]: value });
	}

	handleSubmit() {
		const spreadsheetConfiguration = this.state;
		if (this.hasOtherPrograms)
			spreadsheetConfiguration.HasOtherPrograms = true;
		if (this.programName)
			spreadsheetConfiguration.ProgramName = this.programName;
		this.alreadyScrolled = false;
		this.context.setValue('spreadsheetConfiguration', spreadsheetConfiguration);
		this.props.nextStep();
	}

	componentDidMount() {
		$('.datepicker').datepicker({
			dateFormat: "@", // Unix timestamp
			onSelect: function (dateText, inst) {
				// https://stackoverflow.com/questions/1452066/jquery-ui-datepicker-multiple-date-selections
				addOrRemoveDate(dateText);
				var dateStr = "";
				var first = true;
				for (var i = 0; i < dates.length; i++) {
					if (!first)
						dateStr += ", ";
					dateStr += $.datepicker.formatDate("m/d/yy", $.datepicker.parseDate("@", dates[i]));
					first = false;
				}

				// trigger the onchange event in React: https://stackoverflow.com/questions/23892547/what-is-the-best-way-to-trigger-onchange-event-in-react-js
				var input = document.getElementById('GameDates');
				var nativeInputValueSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value").set;
				nativeInputValueSetter.call(input, dateStr);
				var ev2 = new Event('input', { bubbles: true });
				input.dispatchEvent(ev2);
			},
			beforeShowDay: function (date) {
				var gotDate = $.inArray($.datepicker.formatDate($(this).datepicker('option', 'dateFormat'), date), dates);
				if (gotDate >= 0) {
					// Enable date so it can be deselected. Set style to be highlighted
					return [true, "ui-state-highlight"];
				}
				// Dates not in the array are left enabled, but with no extra style
				return [true, ""];
			}
		});
	}

	componentDidUpdate(prevProps, prevState, snapshot) {
		if (this.props.stepNum === this.context.currentStep) {
			if (!this.alreadyScrolled) {
				document.getElementById(`accordion${this.props.stepNum}`).scrollIntoView();
			}
			this.alreadyScrolled = true;
		}
		if (!this.hasOtherPrograms && this.context.spreadsheetConfiguration.HasOtherPrograms)
			this.hasOtherPrograms = this.context.spreadsheetConfiguration.HasOtherPrograms;
		if (typeof (this.programName) === "undefined" && this.context.spreadsheetConfiguration.ProgramName)
			this.programName = this.context.spreadsheetConfiguration.ProgramName;
	}

	render() {
		let programNameInput = null;
		if (this.hasOtherPrograms) {
			// tbis is only necessary when there are >1 program names
			programNameInput = (
				<div className="mb-3">
					<label htmlFor="ProgramName" className="form-label">Program name for this region (from SportsConnect)</label>
					<input type="text" className="form-control" name="ProgramName" onChange={this.handleChange} />
					<div className="form-text">e.g., 2021 Fall Core, 2021–22 Extra</div>
				</div>
			);
		}
		const content = (
			<div className="accordion-body">
				<form>
					<div className="mb-3">
						<label htmlFor="SpreadsheetId" className="form-label">Google Sheets document ID (leave blank to create a new document)</label>
						<input type="text" className="form-control" name="SpreadsheetId" onChange={this.handleChange} />
						<div className="form-text">Get it from the URL of the sheet: docs.google.com/spreadsheets/d/<span style={{ textDecoration: "underline" }}>SPREADHEET_ID</span>/edit#gid=0</div>
					</div>
					<div className="mb-3">
						<label htmlFor="SpreadsheetTitle" className="form-label">Google Sheets document title</label>
						<input type="text" className="form-control" name="SpreadsheetTitle" onChange={this.handleChange} />
						<div className="form-text">e.g., 2021 Region 42 Scores and Standings</div>
					</div>
					{programNameInput}
					<div className="mb-3">
						<label htmlFor="GameDates" className="form-label">Game dates (click to select)</label>
						<span className="datepicker">&nbsp;</span>
						<input type="text" className="form-control" name="GameDates" id="GameDates" onChange={this.handleChange} />
						<div className="form-text">Pick one date per round. For example, if you have half your games on Saturdays and half on Sundays, only select one of those, not both.</div>
					</div>
					<div className="mb-3">
						<label className="form-label">Do you use any of the following?</label>
						<div className="form-text">
							For all of these, you can specify whether or not they affect the team standings (except points deductions, for obvious reasons).
							You can also choose if you want to enter the values as weekly values or cumulative totals.
							<ul>
								<li>When entered as weekly values, the score sheet does the math for you.</li>
								<li>When entered as cumulative totals, you have to do the math, but if you have to make adjustments later, it&apos;s easier because you don&apos;t need to figure out which week they apply to.</li>
							</ul>
						</div>
						<div className="row">
							<div className="col-2">
								<div className="form-check">
									<input className="form-check-input" type="checkbox" name="RefereePoints" id="RefereePoints" onChange={this.handleCheckbox} />
									<label className="form-check-label" htmlFor="RefereePoints">Referee points</label>
								</div>
							</div>
							<div className="col">
								<div className="form-check form-switch">
									<input className="form-check-input" type="checkbox" name="RefereePointsAffectStandings" id="RefereePointsAffectStandings" onChange={this.handleCheckbox} />
									<label className="form-check-label" htmlFor="RefereePointsAffectStandings">Ref points affect standings</label>
								</div>
								<div className="form-check form-check-inline">
									<input className="form-check-input" type="radio" name="RefereePointsAreCumulative" id="RefereePointsAreCumulative1" value="false" onChange={this.handleChange} />
									<label className="form-check-label" htmlFor="RefereePointsAreCumulative1">Ref points are entered as weekly totals</label>
								</div>
								<div className="form-check form-check-inline">
									<input className="form-check-input" type="radio" name="RefereePointsAreCumulative" id="RefereePointsAreCumulative2" value="true" onChange={this.handleChange} />
									<label className="form-check-label" htmlFor="RefereePointsAreCumulative2">Ref points are entered cumulatively</label>
								</div>
							</div>
						</div>
						<div className="row">
							<div className="col-2">
								<div className="form-check">
									<input className="form-check-input" type="checkbox" name="VolunteerPoints" id="VolunteerPoints" onChange={this.handleCheckbox} />
									<label className="form-check-label" htmlFor="VolunteerPoints">Volunteer points</label>
								</div>
							</div>
							<div className="col">
								<div className="form-check form-switch">
									<input className="form-check-input" type="checkbox" name="VolunteerPointsAffectStandings" id="VolunteerPointsAffectStandings" onChange={this.handleCheckbox} />
									<label className="form-check-label" htmlFor="VolunteerPointsAffectStandings">Volunteer points affect standings</label>
								</div>
								<div className="form-check form-check-inline">
									<input className="form-check-input" type="radio" name="VolunteerPointsAreCumulative" id="VolunteerPointsAreCumulative1" value="false" onChange={this.handleChange} />
									<label className="form-check-label" htmlFor="VolunteerPointsAreCumulative1">Volunteer points are entered as weekly totals</label>
								</div>
								<div className="form-check form-check-inline">
									<input className="form-check-input" type="radio" name="VolunteerPointsAreCumulative" id="VolunteerPointsAreCumulative2" value="true" onChange={this.handleChange} />
									<label className="form-check-label" htmlFor="VolunteerPointsAreCumulative2">Volunteer points are entered cumulatively</label>
								</div>
							</div>
						</div>
						<div className="row">
							<div className="col-2">
								<div className="form-check">
									<input className="form-check-input" type="checkbox" name="SportsmanshipPoints" id="SportsmanshipPoints" onChange={this.handleCheckbox} />
									<label className="form-check-label" htmlFor="SportsmanshipPoints">Sportsmanship points</label>
								</div>
							</div>
							<div className="col">
								<div className="form-check form-switch">
									<input className="form-check-input" type="checkbox" name="SportsmanshipPointsAffectStandings" id="SportsmanshipPointsAffectStandings" onChange={this.handleCheckbox} />
									<label className="form-check-label" htmlFor="SportsmanshipPointsAffectStandings">Sportsmanship points affect standings</label>
								</div>
								<div className="form-check form-check-inline">
									<input className="form-check-input" type="radio" name="SportsmanshipPointsAreCumulative" id="SportsmanshipPointsAreCumulative1" value="false" onChange={this.handleChange} />
									<label className="form-check-label" htmlFor="SportsmanshipPointsAreCumulative1">Sportsmanship points are entered as weekly totals</label>
								</div>
								<div className="form-check form-check-inline">
									<input className="form-check-input" type="radio" name="SportsmanshipPointsAreCumulative" id="SportsmanshipPointsAreCumulative2" value="true" onChange={this.handleChange} />
									<label className="form-check-label" htmlFor="SportsmanshipPointsAreCumulative2">Sportsmanship points are entered cumulatively</label>
								</div>
							</div>
						</div>
						<div className="row">
							<div className="col-2">
								<div className="form-check">
									<input className="form-check-input" type="checkbox" name="PointsDeductions" id="PointsDeductions" onChange={this.handleCheckbox} />
									<label className="form-check-label" htmlFor="PointsDeductions">Points deductions (e.g., for yellow/red cards)</label>
								</div>
							</div>
							<div className="col">
								<div className="form-check form-check-inline">
									<input className="form-check-input" type="radio" name="PointsDeductionsAreCumulative" id="PointsDeductionsAreCumulative1" value="false" onChange={this.handleChange} />
									<label className="form-check-label" htmlFor="PointsDeductionsAreCumulative1">Points deductions are entered as weekly totals</label>
								</div>
								<div className="form-check form-check-inline">
									<input className="form-check-input" type="radio" name="PointsDeductionsAreCumulative" id="PointsDeductionsAreCumulative2" value="true" onChange={this.handleChange} />
									<label className="form-check-label" htmlFor="PointsDeductionsAreCumulative2">Points deductions are entered cumulatively</label>
								</div>
							</div>
						</div>
					</div>
				</form>
				<p className="text-end">
					<button className="btn btn-link" onClick={this.props.prevStep}>Previous</button>
					<button className="btn btn-primary" onClick={this.handleSubmit}>Next</button>
				</p>
			</div>
		);
		return (
			<AccordionItem stepNum={this.props.stepNum} content={content} headerText="Configure spreadsheet" />
		);
	}
}

class DivisionReview extends React.Component {
	render() {
		const programTeams = {};
		const programNames = [];
		for (var i = 0; i < this.props.teams.length; i++) {
			const team = this.props.teams[i];
			if (typeof (programTeams[team.program]) === "undefined") {
				programTeams[team.program] = [];
				programNames.push(team.program);
			}

			programTeams[team.program].push(team.name);
		}

		let programBreakdown = ".";
		if (programNames.length > 1) {
			let first = true;
			for (var i = 0; i < programNames.length; i++) {
				if (first) {
					programBreakdown = ": ";
					first = false;
				} else {
					programBreakdown += ", ";
				}
				const programName = programNames[i];
				const teamsInProgram = programTeams[programName];
				programBreakdown += `${teamsInProgram.length} teams belonging to ${programName}`;
			}
		}

		const divisionConfigReview = (
			<DivisionConfigReview
				divisionName={this.props.divisionName}
				divisionConfig={this.props.divisionConfig}
				oddNumberOfTeams={(this.props.teams.length % 2) === 1}
				hasOtherRegions={programNames.length > 1}
			/>
		);
		return (
			<li>
				{this.props.divisionName} has {this.props.teams.length} teams{programBreakdown}
				{divisionConfigReview}
			</li>
		);
	}
}

class DivisionConfigReview extends React.Component {
	render() {
		let practiceRounds = "";
		if (this.props.divisionConfig.PracticeRounds)
			practiceRounds = (<li>The first {this.props.divisionConfig.PracticeRounds} rounds are practice games and will not count towards standings.</li>);

		const divisionFormName = this.props.divisionName.convertToFormFriendly();
		let oddNumberTeamHandling = null;
		if (this.props.oddNumberOfTeams) {
			let selectedValue = false;
			const selectionMade = typeof (this.props.divisionConfig[`HasFriendlyGamesEachRound-${divisionFormName}`]) !== "undefined";
			if (selectionMade)
				selectedValue = String(true) === this.props.divisionConfig[`HasFriendlyGamesEachRound-${divisionFormName}`];
			let oddNumberText = "As there is an odd number of teams in this division, ";
			oddNumberText += selectedValue ?
				"the team that would normally have a bye for the round will play a scrimmage against one of the other teams." :
				"one team will have a bye for the round and will not play.";
			oddNumberTeamHandling = (<li>{oddNumberText}</li>);
		}

		let otherRegionHandling = null;
		if (this.props.hasOtherRegions) {
			let selectedValue = false;
			const selectionMade = typeof (this.props.divisionConfig[`IncludeOtherRegionsInStandings-${divisionFormName}`]) !== "undefined";
			if (selectionMade)
				selectedValue = String(true) === this.props.divisionConfig[`IncludeOtherRegionsInStandings-${divisionFormName}`];
			const willOrWillNot = selectedValue ? "" : " not";
			const otherRegionText = `Teams from this region will be playing against opponents from other programs, and those other teams will${willOrWillNot} be included in the standings table.`;
			otherRegionHandling = (<li>{otherRegionText}</li>);
		}

		return (
			<ul>
				{practiceRounds}
				{oddNumberTeamHandling}
				{otherRegionHandling}
			</ul>
		);
	}
}

class AdditionalSheetReview extends React.Component {
	render() {
		const affectsStandings = this.props.affectsStandings ? "" : " not";
		const weeklyValues = this.props.weeklyValues ? "weekly values" : "cumulative totals";
		return (
			<li>{this.props.sheetName}: These will{affectsStandings} affect standings and the values are entered as {weeklyValues}.</li>
		);
	}
}

class ReviewAndSubmit extends React.Component {
	static contextType = AppContext;

	componentDidUpdate() {
		if (this.props.stepNum === this.context.currentStep)
			document.getElementById(`accordion${this.props.stepNum}`).scrollIntoView();
	}

	render() {
		const divisions = this.context.divisions;
		const divisionConfigurations = this.context.divisionConfigurations;
		const spreadsheetConfiguration = this.context.spreadsheetConfiguration;
		const divisionContent = [];
		let keys = [];
		if (divisions) {
			keys = Object.keys(divisions);
			keys.sort();
			for (var i = 0; i < keys.length; i++) {
				const divisionName = keys[i];
				const teams = divisions[divisionName].teams;
				let divisionConfig = divisionConfigurations[divisionName];
				if (!divisionConfig) {
					// default value in case user didn't input anything
					divisionConfig = {
						PracticeRounds: 0
					};
					divisionConfigurations[divisionName] = divisionConfig;
					// this.context.setValue({ divisionConfigurations: divisionConfigurations }); -- not allowed during render() but can't seem to find any better place to do it so we'll need to do it server-side
				}
				divisionContent.push(<DivisionReview key={divisionName} divisionName={divisionName} teams={teams} divisionConfig={divisionConfig} />);
			}
		}

		const spreadsheetInfo = typeof (spreadsheetConfiguration.SpreadsheetId) === "undefined" ?
			(<p>{`A new Google Sheets document will be created and it will be titled ${spreadsheetConfiguration.SpreadsheetTitle}.`}</p>) :
			(<p><a href={`https://docs.google.com/spreadsheets/d/${spreadsheetConfiguration.SpreadsheetId}/edit#gid=0`} target="_blank">This existing spreadsheet</a> will be used and it will be renamed {`${spreadsheetConfiguration.SpreadsheetTitle}`}.</p>);

		const gameDatesArr = typeof (spreadsheetConfiguration.GameDates) === "undefined" ? [] : spreadsheetConfiguration.GameDates.split(', ');

		const addlSheets = [];
		if (spreadsheetConfiguration.RefereePoints) {
			const affectsStandings = spreadsheetConfiguration.RefereePointsAffectStandings === true;
			const weeklyValues = String(true) !== spreadsheetConfiguration.RefereePointsAreCumulative;
			addlSheets.push(<AdditionalSheetReview sheetName="Referee points" key="Referee points" affectsStandings={affectsStandings} weeklyValues={weeklyValues} />);
		}
		if (spreadsheetConfiguration.VolunteerPoints) {
			const affectsStandings = spreadsheetConfiguration.VolunteerPointsAffectStandings === true;
			const weeklyValues = String(true) !== spreadsheetConfiguration.VolunteerPointsAreCumulative;
			addlSheets.push(<AdditionalSheetReview sheetName="Volunteer points" key="Volunteer points" affectsStandings={affectsStandings} weeklyValues={weeklyValues} />);
		}
		if (spreadsheetConfiguration.SportsmanshipPoints) {
			const affectsStandings = spreadsheetConfiguration.SportsmanshipPointsAffectStandings === true;
			const weeklyValues = String(true) !== spreadsheetConfiguration.SportsmanshipPointsAreCumulative;
			addlSheets.push(<AdditionalSheetReview sheetName="Sportsmanship points" key="Sportsmanship points" affectsStandings={affectsStandings} weeklyValues={weeklyValues} />);
		}
		if (spreadsheetConfiguration.PointsDeductions) {
			const weeklyValues = String(true) !== spreadsheetConfiguration.PointsDeductionsAreCumulative;
			addlSheets.push(<AdditionalSheetReview sheetName="Points deductions" key="Points deductions" affectsStandings={true} weeklyValues={weeklyValues} />);
		}
		let addlSheetInfo = null;
		if (addlSheets.length > 0) {
			addlSheetInfo = (
				<div>
					<p>The following additional sheets will be created:</p>
					<ul>
						{addlSheets}
					</ul>
				</div>
			);
		}

		const content = (
			<div className="accordion-body">
				<p>There are a total of {keys.length} divisions that we will create score entry sheets for:</p>
				<ul>{divisionContent}</ul>
				{spreadsheetInfo}
				<p>The program name in SportsConnect for this region is {spreadsheetConfiguration.ProgramName}.</p>
				<p>There are a total of {gameDatesArr.length} rounds of games, and the round dates are: {spreadsheetConfiguration.GameDates}.</p>
				{addlSheetInfo}
				<p className="text-end">
					<button className="btn btn-link" onClick={this.props.prevStep}>Previous</button>
					<button className="btn btn-primary" onClick={this.props.handleSubmit}>Finish</button>
				</p>
			</div>
		);
		return (
			<AccordionItem stepNum={this.props.stepNum} content={content} headerText="Review and submit" />
		);
	}
}

ReactDOM.render(
	<App />, document.getElementById('root')
);