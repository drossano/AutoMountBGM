namespace PrincessRTFM.AutoMountBGM;

using System.Collections.Generic;
using System.Linq;

using Dalamud.Configuration;

public class Configuration: IPluginConfiguration {
	public int Version { get; set; } = 1;

	public bool NagOnUnsetMount { get; set; } = true;
	public HashSet<ushort> BgmEnabledMounts { get; set; } = new();
	public HashSet<ushort> BgmDisabledMounts { get; set; } = new();

	public void Save() => Plugin.Interface.SavePluginConfig(this);
	public int Clean() {
		ushort[] invalid = this.BgmEnabledMounts.Intersect(this.BgmDisabledMounts).ToArray();
		foreach (ushort mount in invalid) {
			this.BgmEnabledMounts.Remove(mount);
			this.BgmDisabledMounts.Remove(mount);
		}
		return invalid.Length;
	}
}
