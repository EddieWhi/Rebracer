using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace SLaks.Rebracer.Services {

	///<summary>Finds the correct location for solution-specific and user-global Rebracer settings files.</summary>
	[Export]
	public class SettingsLocator {
		readonly string userFolder;
		readonly DTE dte;

		string FileName
		{
			get
			{
				try {
					if (dte?.Version == "15.0")
						return "RebracerV15.0.xml";
					else
						return "Rebracer.xml";
				} catch (Exception) {
					return "Rebracer.xml";
				}
			}
		}

		[ImportingConstructor]
		public SettingsLocator(SVsServiceProvider sp) {
			userFolder = new ShellSettingsManager(sp).GetApplicationDataFolder(ApplicationDataFolder.RoamingSettings);
			dte = (DTE)sp.GetService(typeof(DTE));
		}

		///<summary>Gets the path to the user global settings file, to be used in the absence of a solution settings file.</summary>
		public string UserSettingsFile { get { return Path.Combine(userFolder, FileName); } }

		///<summary>Gets the path to a solution-specific settings file.</summary>
		public string SolutionPath(Solution solution) {
			return Path.Combine(Path.GetDirectoryName(solution.FileName), FileName);
		}

		///<summary>Gets the path to a solution-specific settings file, based on the solution items, not just the path.</summary>
		public string SolutionItemsPath(Solution solution) {
			try
			{
				var solItems = solution.GetSolutionItems();
				var bracerXml = solItems?.ProjectItems.OfType<ProjectItem>().FirstOrDefault(pi => pi.Name.Equals(FileName, StringComparison.OrdinalIgnoreCase));

				if (bracerXml?.FileCount == 1)
					return Path.Combine(Path.GetDirectoryName(solution.FileName), bracerXml.FileNames[1]);
			}
			catch (Exception)
			{
				//Ignore any errors
				return SolutionPath(solution);
			}

			return SolutionPath(solution);
		}

		///<summary>Gets the path to the settings file to use for a specific solution, if any.</summary>
		public string GetActiveFile(Solution solution) {
			if (!solution.IsOpen || String.IsNullOrEmpty(solution.FileName))
				return UserSettingsFile;
			return new[] { SolutionPath(solution), SolutionItemsPath(solution), UserSettingsFile }.FirstOrDefault(File.Exists) ?? UserSettingsFile;
		}
	}
}
