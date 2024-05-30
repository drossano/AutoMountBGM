using System;
using System.Numerics;

using Dalamud.Interface.Windowing;

using ImGuiNET;

namespace PrincessRTFM.AutoMountBGM;

public class ConfigWindow: Window {
	public const ImGuiWindowFlags FLAGS = ImGuiWindowFlags.None;
	private const string
		LblHeader = "Enable mount-specific BGM for...",
		LblOn = "Enabled",
		LblOff = "Disabled",
		LblSet = "Set";

	private static string bgmFilenameBorderless = null!;
	public static string BorderlessBgmFile {
		get {
			bgmFilenameBorderless ??= Plugin.GetBgmName(Plugin.BgmIdBorderless);
			return bgmFilenameBorderless;
		}
	}

	private bool playerLoggedIn = false;
	private bool filterOnlyUnlocked = false;
	private string searchText = string.Empty;

	public ConfigWindow(string name) : base(name, FLAGS, false) {
		this.AllowClickthrough = false;
		this.AllowPinning = true;
		this.SizeConstraints = new WindowSizeConstraints() {
			MinimumSize = new Vector2(500, 400),
			MaximumSize = new Vector2(600, 800),
		};
		this.Size = new(
			this.SizeConstraints.Value.MinimumSize.X,
			(this.SizeConstraints.Value.MinimumSize.X + this.SizeConstraints.Value.MaximumSize.X) / 2f
		);
#if DEBUG
		this.SizeCondition = ImGuiCond.Appearing;
#else
		this.SizeCondition = ImGuiCond.FirstUseEver;
#endif
	}
	public override void OnOpen() {
		base.OnOpen();
		this.Collapsed = false;
		this.searchText = string.Empty;
	}

	public override bool DrawConditions() => Plugin.Initialised;

	public override void PreDraw() {
		this.playerLoggedIn = Plugin.ClientState.IsLoggedIn;
		if (!this.playerLoggedIn)
			this.filterOnlyUnlocked = false;
	}

	public override void Draw() {
		drawMassControlButtons();

		Vector2 room = ImGui.GetContentRegionAvail();
		ImGuiStylePtr style = ImGui.GetStyle();
		Vector2 size = new(room.X, room.Y - (ImGui.GetTextLineHeightWithSpacing() * 2) - ImGui.GetFrameHeightWithSpacing());
		if (ImGui.BeginChild("mountcontrols", size, true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
			this.drawMountList();
			ImGui.Separator();
			this.drawFilters();
		}
		ImGui.EndChild();

		if (ImGui.Checkbox("Always disable mount BGM for mounts playing \"Borderless\"", ref Plugin.Config.DisableBorderlessBgm))
			Plugin.Config.Save();
		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(550);
			ImGui.TextUnformatted("This is the really mind-numbing default song for mounts that don't have their own custom track, such as the chocobo."
				+ $"The track name is {BorderlessBgmFile}, in case you want to look through the list.");
			ImGui.TextUnformatted("");
			ImGui.TextUnformatted("If this is enabled, it functions as an override - any time you get on a mount that plays Borderless, mount BGM will be disabled, regardless of that specific mount's setting.");
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}

		ImGui.TextUnformatted("The game doesn't expose song names for BGM tracks, sorry.");
		ImGui.TextUnformatted("The best I can do is the internal BGM track filename.");
	}

	private static void drawMassControlButtons() {
		ImGuiStylePtr style = ImGui.GetStyle();
		bool modeEnable = ImGui.IsKeyDown(ImGuiKey.ModCtrl); // control -> turn ON, else -> turn OFF
		string btnAll = $"{LblSet} All {(modeEnable ? LblOn : LblOff)}";
		string btnBad = $"{LblSet} \"Borderless\" {(modeEnable ? LblOn : LblOff)}";
		string tip = modeEnable ? $"Release CONTROL to {LblSet.ToLower()} {LblOff.ToUpper()}." : $"Hold CONTROL to {LblSet.ToLower()} {LblOn.ToUpper()}.";
		float wAll = ImGui.CalcTextSize(btnAll).X;
		float wBorderless = ImGui.CalcTextSize(btnBad).X;
		float wSpace = ImGui.CalcTextSize(" ").X;
		float edgeRight = ImGui.GetContentRegionAvail().X - style.WindowPadding.X - style.ItemSpacing.X;

		ImGui.TextUnformatted(LblHeader);

		ImGui.SameLine(edgeRight - (wAll + wSpace + wBorderless));
		if (shiftOnlySmallButton(btnAll, tip)) {
			if (modeEnable) {
				Plugin.Config.BgmDisabledMounts.Clear();
			}
			else {
				foreach (MountData mount in Plugin.AlphabetisedMounts)
					Plugin.Config.BgmDisabledMounts.Add(mount.Id);
			}
			Plugin.Config.Save();
		}

		ImGui.SameLine();
		if (shiftOnlySmallButton(btnBad, tip)) {
			foreach (MountData mount in Plugin.AlphabetisedMounts) {
				if (mount.BgmId == Plugin.BgmIdBorderless) {
					if (modeEnable)
						Plugin.Config.BgmDisabledMounts.Remove(mount.Id);
					else
						Plugin.Config.BgmDisabledMounts.Add(mount.Id);
				}
			}
			Plugin.Config.Save();
		}
	}

	private static bool shiftOnlySmallButton(string label, string extraTooltip = "") {
		bool shift = ImGui.IsKeyDown(ImGuiKey.ModShift);
		bool hasExtraText = !string.IsNullOrWhiteSpace(extraTooltip);
		if (!shift)
			ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().DisabledAlpha);
		bool retVal = ImGui.SmallButton(label) && shift;
		if (!shift)
			ImGui.PopStyleVar();
		if (ImGui.IsItemHovered() && (!shift || hasExtraText)) {
			ImGui.BeginTooltip();
			if (!shift)
				ImGui.TextUnformatted("Hold SHIFT to use this button.");
			if (hasExtraText)
				ImGui.TextUnformatted(extraTooltip);
			ImGui.EndTooltip();
		}
		return retVal;
	}

	private void drawFilters() {
		ImGui.BeginGroup();
		ImGui.BeginDisabled(!this.playerLoggedIn);
		ImGui.Checkbox("Show only unlocked mounts?", ref this.filterOnlyUnlocked);
		ImGui.EndDisabled();
		ImGui.EndGroup();
		if (!this.playerLoggedIn && ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.TextUnformatted("You must be logged in on a character to filter to unlocked mounts.");
			ImGui.EndTooltip();
		}

		ImGui.SameLine();
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		ImGui.InputTextWithHint("###searchByName", "Filter by name...", ref this.searchText, 20);
	}

	private void drawMountList() {
		ImGuiStylePtr style = ImGui.GetStyle();
		Vector2 size = new(0);
		//size.Y -= style.FramePadding.Y;
		size.Y -= ImGui.GetFrameHeightWithSpacing();
		bool filterByName = !string.IsNullOrEmpty(this.searchText);

		if (ImGui.BeginChild("mountlist", size, false, ImGuiWindowFlags.AlwaysVerticalScrollbar)) {
			foreach (MountData mount in Plugin.AlphabetisedMounts) {
				if (filterByName && !mount.Name.Contains(this.searchText, StringComparison.OrdinalIgnoreCase))
					continue;
				if (this.filterOnlyUnlocked && !mount.Unlocked)
					continue;

				bool enableBgm = !Plugin.Config.BgmDisabledMounts.Contains(mount.Id);
				if (ImGui.Checkbox($"{mount.Name}###mount{mount.Id}", ref enableBgm)) {
					if (enableBgm)
						Plugin.Config.BgmDisabledMounts.Add(mount.Id);
					else
						Plugin.Config.BgmDisabledMounts.Remove(mount.Id);
					Plugin.Config.Save();
				}

				if (ImGui.IsItemHovered()) {
					ImGui.BeginTooltip();
					ImGui.TextUnformatted($"BGM file: {mount.Bgm}");
					ImGui.EndTooltip();
				}
			}
		}
		ImGui.EndChild();
	}
}
