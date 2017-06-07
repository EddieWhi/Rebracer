﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SLaks.Rebracer.Services {
	///<summary>Handles Visual Studio events to save and load settings files when appropriate.</summary>
	[Export(typeof(IAutoActivatingService))]
	class SolutionListener : IAutoActivatingService {
		private readonly DTE dte;
		private readonly SettingsLocator locator;
		private readonly ILogger logger;
		private readonly SettingsPersister persister;

		static readonly VSConstants.VSStd97CmdID[] optionsCommands =
		{
			VSConstants.VSStd97CmdID.ToolsOptions,
			VSConstants.VSStd97CmdID.DebugOptions,
			VSConstants.VSStd97CmdID.CustomizeKeyboard
		};

		// Necessary to prevent event objects from being GC'd.
		// See http://stackoverflow.com/a/13581371/34397
		private readonly SolutionEvents solutionEvents;
		private readonly DTEEvents dteEvents;
		private readonly ProjectItemsEvents projectEvents;
		private readonly List<CommandEvents> commandsEvents = new List<CommandEvents>();

		[ImportingConstructor]
		public SolutionListener(SVsServiceProvider sp, ILogger logger, SettingsPersister persister, SettingsLocator locator) {
			this.logger = logger;
			this.locator = locator;
			this.persister = persister;

			dte = (DTE)sp.GetService(typeof(DTE));

			dteEvents = dte.Events.DTEEvents;
			solutionEvents = dte.Events.SolutionEvents;
			projectEvents = ((Events2)dte.Events).ProjectItemsEvents;
		}
		public void Activate() {
			InitializeSettingsFile();

			solutionEvents.AfterClosing += SolutionEvents_AfterClosing;
			solutionEvents.Opened += SolutionEvents_Opened;

			projectEvents.ItemAdded += ProjectEvents_ItemAdded;
			projectEvents.ItemRemoved += ProjectEvents_ItemRemoved;

			foreach (var optionCmdId in optionsCommands) {
				AddCommandEventHandler(VSConstants.GUID_VSStandardCommandSet97, optionCmdId, ToolsOptionsCommand_AfterExecute);
			}
			AddCommandEventHandler(VSConstants.GUID_VSStandardCommandSet97, VSConstants.VSStd97CmdID.SaveSolution, SaveAllCommand_AfterExecute);

			dteEvents.OnBeginShutdown += DTEEvents_OnBeginShutdown;
			solutionEvents.BeforeClosing += SolutionEvents_BeforeClosing;
		}

		private void AddCommandEventHandler(Guid group, VSConstants.VSStd97CmdID cmdId, _dispCommandEvents_AfterExecuteEventHandler handler) {
			var h = dte.Events.CommandEvents[group.ToString("B"), (int)cmdId];
			h.AfterExecute += handler;
			commandsEvents.Add(h);
		}

		private void InitializeSettingsFile() {
			// On first launch, populate the global settings file
			// before loading any solution settings.
			if (!File.Exists(locator.UserSettingsFile)) {
				logger.Log("Creating user settings file to store current global settings");
				persister.CreateSettingsFile(locator.UserSettingsFile,
					"Rebracer User Settings File",
					"This file contains your global Visual Studio settings.",
					"Rebracer uses this file to restore your settings after",
					"closing a solution that specifies its own settings.",
					"This file will be automatically updated by Rebracer as",
					"you change settings in Visual Studio"
				);
			}

			// When VS is launched, wait until we know
			// whether the used opened a solution, and
			// activate the solution or global file.
			persister.ActivateSettingsFile(locator.GetActiveFile(dte.Solution));
		}

		#region Events to read settings
		private void SolutionEvents_Opened() {
			// When the user opens a solution, activate its
			// settings file, if any.
			persister.ActivateSettingsFile(locator.GetActiveFile(dte.Solution));
		}

		private async void SolutionEvents_AfterClosing() {
			// If the user closed a solution, switch back
			// to the global (or new solution's) settings
			// Wait a bit to avoid double-loading in case
			// the user opened a new solution, as opposed
			// to closing this one only.
			await Task.Delay(750);
			persister.ActivateSettingsFile(locator.GetActiveFile(dte.Solution));
		}


		private void ProjectEvents_ItemAdded(ProjectItem ProjectItem) {
			// If the user added a different file, or if there
			// is no real solution, do nothing.
			// Also do nothing when a solution that contains a
			// Solution Item that doesn't exist is opened.
			if (!dte.Solution.IsOpen || string.IsNullOrEmpty(dte.Solution.FullName))
				return;
			var expectedPath = locator.SolutionPath(dte.Solution);
			if (String.IsNullOrEmpty(expectedPath)
			 || ProjectItem.Name != Path.GetFileName(expectedPath)
			 || ProjectItem.FileCount != 1 || !File.Exists(ProjectItem.get_FileNames(1)))
				return;

			if (File.Exists(expectedPath))
				persister.ActivateSettingsFile(expectedPath);
			else
				MessageBox.Show("The Rebracer settings file that you added is not within the solution directory. Whilst this"
							  + "will work, you may get unexpected changes when sharing a single settings file between solutions.\n"
							  + "To add a Rebracer settings file to the solution, right-click the solution, click Add, New Rebracer Settings File\n"
							  + "Alternatively, copy an existing settings file to \n  " + expectedPath,
								"Rebracer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}

		private void ProjectEvents_ItemRemoved(ProjectItem ProjectItem) {
			var oldPath = locator.SolutionPath(dte.Solution);
			if (String.IsNullOrEmpty(oldPath)
			 || ProjectItem.FileCount != 1 || ProjectItem.get_FileNames(1) != oldPath)
				return;

			if (!File.Exists(oldPath))
				persister.ActivateSettingsFile(locator.UserSettingsFile);
			else if (DialogResult.Yes == MessageBox.Show("The Rebracer settings file still exists; you are still using solution-specific settings.\n"
													   + "Would you like to delete the Rebracer settings file from disk and revert to global settings?",
														 "Rebracer", MessageBoxButtons.YesNo, MessageBoxIcon.Question)) {
				File.Delete(oldPath);
				persister.ActivateSettingsFile(locator.UserSettingsFile);
			}
		}
		#endregion

		#region Events to save settings
		private void ToolsOptionsCommand_AfterExecute(string Guid, int ID, object CustomIn, object CustomOut) {
			// After the user changes any options, save them.
			persister.SaveSettings();
		}

		private void DTEEvents_OnBeginShutdown() {
			// In case settings changed without the options dialog, save on exit
			persister.SaveSettings();
		}

		private void SolutionEvents_BeforeClosing() {
			// In case settings changed without the options dialog, save solution settings before closing it
			if (persister.SettingsPath != locator.UserSettingsFile)
				persister.SaveSettings();
		}

		private void SaveAllCommand_AfterExecute(string Guid, int ID, object CustomIn, object CustomOut) {
			// In case settings changed without the options dialog, save solution settings on Save All
			if (persister.SettingsPath != locator.UserSettingsFile)
				persister.SaveSettings();
		}
		#endregion
	}
}
