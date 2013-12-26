﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace SLaks.Rebracer.Notifications {
	public class DesignerThemeDictionary : ResourceDictionary {

		// We must access everything from these classes using dynamic due to NoPIA conflicts.
		// The compiler gives some errors since we do not have the right PIA, and the runtime
		// gives more errors because NoPIA doesn't unify for managed implementations.
		dynamic currentTheme;
		readonly dynamic service;
		public DesignerThemeDictionary() {
			if (ServiceProvider.GlobalProvider.GetService(new Guid("FD57C398-FDE3-42c2-A358-660F269CBE43")) != null)
				return; // Do nothing when hosted in VS
			//AssemblyResolverHack.AddHandler();
			ServiceProviderMock.Initialize();
			service = Activator.CreateInstance(Type.GetType("Microsoft.VisualStudio.Platform.WindowManagement.ColorThemeService, Microsoft.VisualStudio.Platform.WindowManagement"));
			ThemeIndex = 0;
		}
		int themeIndex;
		public int ThemeIndex {
			get { return themeIndex; }
			set { themeIndex = value; LoadTheme(value); }
		}

		static Color ToColorFromRgba(uint colorValue) {
			return Color.FromArgb((byte)(colorValue >> 24), (byte)colorValue, (byte)(colorValue >> 8), (byte)(colorValue >> 16));
		}
		static SolidColorBrush GetBrush(Color color) {
			var brush = new SolidColorBrush(color);
			brush.Freeze();
			return brush;
		}
		// Loosely based on Microsoft.VisualStudio.Platform.WindowManagement.ResourceSynchronizer.AddSolidColorKeys()
		public void LoadTheme(int index) {
			if (service == null)
				return;
			Clear();

			currentTheme = service.Themes[index % service.Themes.Count];
			foreach (ColorName colorName in service.ColorNames) {
				IVsColorEntry entry = currentTheme[colorName];
				if (entry == null || entry.BackgroundType == 0)
					continue;

				int colorId = VsColorFromName(colorName);
				if (colorId == 0)
					continue;

				var color = ToColorFromRgba(entry.Background);

				Add(VsColors.GetColorKey(colorId), color);
				Add(VsBrushes.GetBrushKey(colorId), GetBrush(color));
			}
		}

		// Microsoft.VisualStudio.Platform.WindowManagement.ColorNameTranslator
		static readonly Guid environmentColors = new Guid("{624ed9c3-bdfd-41fa-96c3-7c824ea32e3d}");
		static int VsColorFromName(ColorName colorName) {
			// Stolen from VsColors.TryGetColorIDFromBaseKey, which is new to 2012
			try {
				if (colorName.Category == environmentColors)
					return VsColors.GetColorID("VsColor." + colorName.Name);
			} catch (ArgumentOutOfRangeException) { }
			return 0;
		}
	}
}
namespace Microsoft.Internal.VisualStudio.Shell.Interop {
	[CompilerGenerated, Guid("413D8344-C0DB-4949-9DBC-69C12BADB6AC"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), TypeIdentifier]
	[ComImport]
	public interface IVsColorTheme {
		IVsColorEntry this[[In] ColorName Name] {
			[return: MarshalAs(UnmanagedType.Interface)]
			get;
		}
		Guid ThemeId { get; }
		string Name {
			[return: MarshalAs(UnmanagedType.BStr)]
			get;
		}
		bool IsUserVisible { get; }
		void Apply();
	}
	[CompilerGenerated, Guid("BBE70639-7AD9-4365-AE36-9877AF2F973B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), TypeIdentifier]
	[ComImport]
	public interface IVsColorEntry {
		ColorName ColorName { get; }
		byte BackgroundType { get; }
		byte ForegroundType { get; }
		uint Background { get; }
		uint Foreground { get; }
		uint BackgroundSource { get; }
		uint ForegroundSource { get; }
	}

	[CompilerGenerated, TypeIdentifier("EF2A7BE1-84AF-4E47-A2CF-056DF55F3B7A", "Microsoft.Internal.VisualStudio.Shell.Interop.ColorName")]
	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public struct ColorName {
		public Guid Category;
		[MarshalAs(UnmanagedType.BStr)]
		public string Name;
	}
}