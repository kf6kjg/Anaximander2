using System;

namespace AssetReader {
	public struct AssetServerWHIPConfig : IAssetServerConfig {
		public string Name { get; set; }

		public AssetServerType Type {
			get {
				return AssetServerType.WHIP;
			}
		}

		public string Host { get; set; }

		public int Port { get; set; }

		public string Password { get; set; }
	}
}