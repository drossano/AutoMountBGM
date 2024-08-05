using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Config;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game.Character;

using Lumina.Excel.GeneratedSheets;

using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

using PlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace PrincessRTFM.AutoMountBGM;

public class Plugin: IDalamudPlugin {
	public const ushort BgmIdBorderless = 319;
	public const ushort BgmIdRoadsLessTraveled = 895;


	public const string Name = "AutoMountBGM";
	public static string Command => $"/{Name.ToLower()}";

	[PluginService] public static IDalamudPluginInterface Interface { get; private set; } = null!;
	[PluginService] public static IClientState ClientState { get; private set; } = null!;
	[PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] public static IChatGui ChatGui { get; private set; } = null!;
	[PluginService] public static ICondition Condition { get; private set; } = null!;
	[PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
	[PluginService] public static IDataManager GameData { get; private set; } = null!;
	[PluginService] public static IPluginLog Log { get; private set; } = null!;
	public static Configuration Config { get; private set; } = null!;

	public static WindowSystem Windows { get; private set; } = null!;
	internal static Window ConfigWindow { get; private set; } = null!;

	private static readonly Dictionary<ushort, string> bgmNames = [];
	private static readonly Dictionary<ushort, MountData> mountData = [];
	private static MountData[] alphabetisedMountData = [];

	internal static MountData[] AlphabetisedMounts => Initialised ? alphabetisedMountData : [];

	public static bool Initialised => initialiserThread?.IsCompletedSuccessfully ?? false;
	private static Task initialiserThread = null!;

	public Plugin() {
		Config = Interface.GetPluginConfig() as Configuration ?? new();
		initialiserThread ??= Task.Run(parallelInit);
		CommandManager.AddHandler(Command, new(this.onCommand) {
			HelpMessage = "[on|off] - check, enable, or disable automatic mount BGM activation for your current mount"
				+ $"\n{Command} open - open the mount BGM control window",
			ShowInHelp = true,
		});
		Condition.ConditionChange += this.onConditionChanged;
		Windows = new(Name);
		ConfigWindow = new ConfigWindow(Name);
		Windows.AddWindow(ConfigWindow);
		Interface.UiBuilder.OpenMainUi += ToggleUi;
		Interface.UiBuilder.OpenConfigUi += ToggleUi;
		Interface.UiBuilder.Draw += Windows.Draw;
	}

	private static void parallelInit() {
		Log.Info("Caching BGM track names...");
		foreach (BGM bgm in GameData.GetExcelSheet<BGM>()!) {
			bgmNames[(ushort)bgm.RowId] = bgm.File.RawString.Replace("music/", "").Replace("ffxiv/", "");
		}
		Log.Info("Caching mount data...");
		foreach (Mount mount in GameData.GetExcelSheet<Mount>()!) {
			string name = mount.Singular;
			ushort id = (ushort)mount.RowId;
			if (!string.IsNullOrWhiteSpace(name))
				mountData[id] = new MountData(id, name, (ushort)mount.RideBGM.Row);
		}
		Log.Info("Alphabetising mount list...");
		alphabetisedMountData = mountData
			.OrderBy(p => p.Value.Name, StringComparer.OrdinalIgnoreCase)
			.Select(p => p.Value)
			.ToArray();
		Log.Info("Parallel initialisation complete - plugin ready!");
	}

	public static void ToggleUi() {
		if (!Initialised) {
			Log.Warning("Parallel initialisation thread has not yet completed! Window will NOT draw!");
		}
		Log.Info($"{(ConfigWindow.IsOpen ? "Clos" : "Open")}ing mount BGM control window");
		ConfigWindow.Toggle();
	}

	public static string? GetMountName(ushort mountId) => mountData.TryGetValue(mountId, out MountData? mount) ? mount.Name : null;
	public static string GetBgmName(ushort trackId) => bgmNames.TryGetValue(trackId, out string? name)
		? name
		: "[N/A]";
	public static unsafe bool CheckMountUnlocked(ushort mountId) {
		if (!ClientState.IsLoggedIn)
			return true; // if you're not logged in, this condition/check is meaningless
		PlayerState* ps = PlayerState.Instance();
		if (ps is null)
			return true; // likewise, just in case
		return ps->IsMountUnlocked(mountId);
	}

	private unsafe ushort mountId {
		get {
			IGameObject? player = ClientState.LocalPlayer;
			if (player is null)
				return 0;
			Character* native = (Character*)player.Address;
			if (native is null)
				return 0;
			if (!native->IsMounted()) // just in case
				return 0;
			MountContainer? mount = native->Mount;
			return mount?.MountId ?? 0;
		}
	}

	private void onConditionChanged(ConditionFlag flag, bool value) {
		if (flag is not (ConditionFlag.Mounted or ConditionFlag.Mounted2))
			return;
		ushort mount = this.mountId;
		if (!value || mount is 0) { // disable mount bgm when unmounting to prevent volume stutters when using a disabled mount
			GameConfig.Set(SystemConfigOption.SoundChocobo, false);
			return;
		}

		if (Config.DisableBorderlessBgm) {
			if (mountData[mount].BgmId == BgmIdBorderless) {
				GameConfig.Set(SystemConfigOption.SoundChocobo, false);
				return;
			}
		}

		if ((Config.DisableBorderlessBgm && mountData[mount].BgmId == BgmIdBorderless) || Config.BgmDisabledMounts.Contains(mount))
			GameConfig.Set(SystemConfigOption.SoundChocobo, false);
		else
			GameConfig.Set(SystemConfigOption.SoundChocobo, true);

		if (Config.DisableRoadsLessTraveledBgm) {
			if (mountData[mount].BgmId == BgmIdRoadsLessTraveled) {
				GameConfig.Set(SystemConfigOption.SoundChocobo, false);
				return;
			}
		}

		if ((Config.DisableRoadsLessTraveledBgm && mountData[mount].BgmId == BgmIdRoadsLessTraveled) || Config.BgmDisabledMounts.Contains(mount))
			GameConfig.Set(SystemConfigOption.SoundChocobo, false);
		else
			GameConfig.Set(SystemConfigOption.SoundChocobo, true);

	}

	private void onCommand(string command, string args) {
		if (!Initialised) {
			Log.Warning("Parallel initialisation thread has not yet completed!");
			ChatGui.PrintError($"{Name} is still initialising, please wait.");
			return;
		}
		ushort mount = this.mountId;
		if (mount is 0) {
			ChatGui.PrintError("You are not currently mounted.");
			ChatGui.Print($"Use '{Command} open' to open the mount BGM configuration window.");
			return;
		}
		string[] argv = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		string subcmd = argv.Length >= 1 ? argv[0].ToLower() : string.Empty;
		string value = argv.Length >= 2 ? argv[1].ToLower() : string.Empty;
		switch (subcmd) {
			case "":
				if (Config.BgmDisabledMounts.Contains(mount))
					ChatGui.Print($"{GetMountName(mount)} auto-disables BGM when used.");
				else
					ChatGui.Print($"{GetMountName(mount)} auto-enables BGM when used.");
				break;
			case "on":
				Config.BgmDisabledMounts.Remove(mount);
				ChatGui.Print($"{GetMountName(mount)} will now enable mount BGM when used.");
				this.onConditionChanged(ConditionFlag.Mounted, true);
				break;
			case "off":
				Config.BgmDisabledMounts.Add(mount);
				ChatGui.Print($"{GetMountName(mount)} will now disable mount BGM when used.");
				this.onConditionChanged(ConditionFlag.Mounted, true);
				break;
			case "open":
				ToggleUi();
				break;
			default:
				ChatGui.PrintError("Unknown subcommand: " + subcmd);
				break;
		}
		Config.Save();
	}

	#region IDisposable
	private bool disposed;
	protected void Dispose(bool disposing) {
		if (this.disposed)
			return;
		this.disposed = true;

		if (disposing) {
			Condition.ConditionChange -= this.onConditionChanged;
			CommandManager.RemoveHandler(Command);
			Interface.UiBuilder.OpenMainUi -= ToggleUi;
			Interface.UiBuilder.OpenConfigUi -= ToggleUi;
			Interface.UiBuilder.Draw -= Windows.Draw;
		}

		alphabetisedMountData = [];
		mountData.Clear();
	}
	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
