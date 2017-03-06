using System;

namespace AssetReader {
	public struct AssetServerCFConfig : IAssetServerConfig {
		public string Name { get; set; }

		public AssetServerType Type {
			get {
				return AssetServerType.CF;
			}
		}

		public string Username { get; set; }

		public string APIKey { get; set; }

		public string DefaultRegion { get; set; }

		public bool UseInternalURL { get; set; }

		public string ContainerPrefix { get; set; }
	}
}