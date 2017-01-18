using System;
using System.Diagnostics.CodeAnalysis;

namespace SLaks.Rebracer {
	static class GuidList {
		public const string guidRebracerPkgString = "57dbed6a-3ad0-4d95-a41c-796f55ee1333";
		public const string guidRebracerCmdSetString = "fdc314b9-606b-438e-afd9-1f19b7318529";

		public static readonly Guid guidRebracerCmdSet = new Guid(guidRebracerCmdSetString);
	}

	[SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification = "This enum is used solely as a container for constants")]

	///<summary>Contains command IDs for commands defined by this package.  These values are defined in the vsct file.</summary>
	public enum PackageCommand {
		CreateSolutionSettingsFile = 0x100
	}
}