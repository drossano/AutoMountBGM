using System;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Config;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace PrincessRTFM.AutoMountBGM;

public class Plugin: IDalamudPlugin {
	public string Name { get; } = "AutoMountBGM";
	public string Command => $"/{this.Name.ToLower()}";

	[PluginService] public static DalamudPluginInterface Interface { get; private set; } = null!;
	[PluginService] public static IClientState ClientState { get; private set; } = null!;
	[PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] public static IChatGui ChatGui { get; private set; } = null!;
	[PluginService] public static ICondition Condition { get; private set; } = null!;
	[PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
	public static Configuration Config { get; private set; } = null!;

	public Plugin() {
		Config = Interface.GetPluginConfig() as Configuration ?? new();
		CommandManager.AddHandler(this.Command, new(this.onCommand) {
			HelpMessage = "[on|off] - check, enable, or disable automatic mount BGM activation for your current mount",
			ShowInHelp = true,
		});
		Condition.ConditionChange += this.onConditionChanged;
		//GameConfig.Changed += this.watchGameConfig;
	}

	private void watchGameConfig(object? sender, ConfigChangeEvent evt) {
		string group = evt.Option.GetType().Name;
		string option = evt.Option.ToString();
		ChatGui.Print($"Option changed: {group}.{option}");
	}

	private unsafe ushort mountId {
		get {
			GameObject? player = ClientState.LocalPlayer;
			if (player is null)
				return 0;
			Character* native = (Character*)player.Address;
			if (native is null)
				return 0;
			if (!native->IsMounted()) // just in case
				return 0;
			Character.MountContainer? mount = native->Mount;
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

		if (Config.BgmDisabledMounts.Contains(mount))
			GameConfig.Set(SystemConfigOption.SoundChocobo, false);
		else
			GameConfig.Set(SystemConfigOption.SoundChocobo, true);
	}

	private void onCommand(string command, string args) {
		ushort mount = this.mountId;
		if (mount is 0) {
			ChatGui.PrintError("You are not currently mounted.");
			return;
		}
		string[] argv = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		string subcmd = argv.Length >= 1 ? argv[0].ToLower() : string.Empty;
		string value = argv.Length >= 2 ? argv[1].ToLower() : string.Empty;
		switch (subcmd) {
			case "":
				if (Config.BgmDisabledMounts.Contains(mount))
					ChatGui.Print("This mount auto-disables BGM when used.");
				else
					ChatGui.Print("This mount auto-enables BGM when used.");
				break;
			case "on":
				Config.BgmDisabledMounts.Remove(mount);
				ChatGui.Print("This mount will now enable mount BGM when used.");
				this.onConditionChanged(ConditionFlag.Mounted, true);
				break;
			case "off":
				Config.BgmDisabledMounts.Add(mount);
				ChatGui.Print("This mount will now disable mount BGM when used.");
				this.onConditionChanged(ConditionFlag.Mounted, true);
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
			CommandManager.RemoveHandler(this.Command);
			GameConfig.Changed -= this.watchGameConfig;
		}
	}
	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
