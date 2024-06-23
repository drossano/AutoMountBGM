using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Dalamud.Configuration;

namespace PrincessRTFM.AutoMountBGM;

[SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "directly passed to Imgui methods as ref params")]
public class Configuration: IPluginConfiguration {
	public int Version { get; set; } = 1;

	public HashSet<ushort> BgmDisabledMounts { get; set; } = [];
	public bool DisableBorderlessBgm = true;

	public void Save() => Plugin.Interface.SavePluginConfig(this);

	public void EnableBgm(ushort mountId) => this.BgmDisabledMounts.Remove(mountId);
	public void EnableBgm(MountData mount) => this.EnableBgm(mount.Id);
	public void EnableAll() => this.BgmDisabledMounts.Clear();

	public void DisableBgm(ushort mountId) => this.BgmDisabledMounts.Add(mountId);
	public void DisableBgm(MountData mount) => this.DisableBgm(mount.Id);
	public void DisableAll() {
		foreach (MountData mount in Plugin.AlphabetisedMounts)
			this.DisableBgm(mount);
	}

	public bool IsBgmEnabled(ushort mountId) => this.BgmDisabledMounts.Contains(mountId);
	public bool IsBgmEnabled(MountData mount) => this.IsBgmEnabled(mount.Id);
}
