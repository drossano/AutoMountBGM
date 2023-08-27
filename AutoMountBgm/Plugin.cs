namespace PrincessRTFM.AutoMountBGM;

using System;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Config;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

public class Plugin: IDalamudPlugin {
	public string Name { get; } = "AutoMountBGM";
	public string Command => $"/{this.Name.ToLower()}";

	[PluginService] public static DalamudPluginInterface Interface { get; private set; } = null!;
	[PluginService] public static IClientState ClientState { get; private set; } = null!;
	[PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] public static ChatGui ChatGui { get; private set; } = null!;
	[PluginService] public static Condition Condition { get; private set; } = null!;
	[PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
	public static Configuration Config { get; private set; } = null!;

	public Plugin() {
		Config = Interface.GetPluginConfig() as Configuration ?? new();
		int cleaned = Config.Clean();
		PluginLog.Information($"Removed {cleaned} mount{(cleaned == 0 ? "" : "s")} duplicated across both sets");
#if !DEBUG
		if (cleaned > 0)
#endif
		Config.Save();
		CommandManager.AddHandler(this.Command, new(this.onCommand) {
			HelpMessage = "[on|off|unset] - check, enable, disable, or unset automatic mount BGM activation for your current mount"
				+ $"\n{this.Command} nag [on|off] - enable, disable, or toggle nagging you when you use a mount that isn't configured in the plugin",
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
		if (!value)
			return;
		ushort mount = this.mountId;
		if (mount is 0)
			return;

		if (Config.BgmEnabledMounts.Contains(mount))
			GameConfig.Set(SystemConfigOption.SoundChocobo, true);
		else if (Config.BgmDisabledMounts.Contains(mount))
			GameConfig.Set(SystemConfigOption.SoundChocobo, false);
		else if (Config.NagOnUnsetMount)
			ChatGui.Print($"This mount neither enables nor disabled mount BGM on use. Use \"{this.Command} <on|off>\" to assign it a mode.");
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
				if (Config.BgmEnabledMounts.Contains(mount))
					ChatGui.Print("This mount auto-enables BGM when used.");
				else if (Config.BgmDisabledMounts.Contains(mount))
					ChatGui.Print("This mount auto-disabled BGM when used.");
				else
					ChatGui.Print("This mount does not auto-configure BGM when used.");
				break;
			case "on":
				Config.BgmEnabledMounts.Add(mount);
				Config.BgmDisabledMounts.Remove(mount);
				ChatGui.Print("This mount will now enable mount BGM when used.");
				this.onConditionChanged(ConditionFlag.Mounted, true);
				break;
			case "off":
				Config.BgmEnabledMounts.Remove(mount);
				Config.BgmDisabledMounts.Add(mount);
				ChatGui.Print("This mount will now disable mount BGM when used.");
				this.onConditionChanged(ConditionFlag.Mounted, true);
				break;
			case "unset":
				Config.BgmEnabledMounts.Remove(mount);
				Config.BgmDisabledMounts.Remove(mount);
				ChatGui.Print("This mount will neither enable nor disable mount BGM when used.");
				break;
			case "nag":
				switch (value) {
					case "":
						Config.NagOnUnsetMount = !Config.NagOnUnsetMount;
						ChatGui.Print($"You will no{(Config.NagOnUnsetMount ? "w" : " longer")} be nagged about assigning a mode to mounts that don't have one.");
						break;
					case "on":
						Config.NagOnUnsetMount = true;
						ChatGui.Print("You will now be nagged about assigning a mode to mounts that don't have one.");
						break;
					case "off":
						Config.NagOnUnsetMount = false;
						ChatGui.Print("You will no longer be nagged about assigning a mode to mounts that don't have one.");
						break;
					default:
						ChatGui.PrintError("Unknown subcommand: " + value);
						break;
				}
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
