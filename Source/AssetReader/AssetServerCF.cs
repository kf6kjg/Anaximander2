// IAssetServer.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2017 Richard Curtice
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using InWorldz.Whip.Client;
using log4net;
using net.openstack.Core.Domain;
using Nini.Config;
using OpenMetaverse;

namespace AssetReader {
	public class AssetServerCF : IAssetServer {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private const int DEFAULT_READ_TIMEOUT = 45 * 1000;
		private const int DEFAULT_WRITE_TIMEOUT = 10 * 1000;
		/// <summary>
		/// How many hex characters to use for the CF container prefix
		/// </summary>
		private const int CONTAINER_UUID_PREFIX_LEN = 4;


		public string Username { get; private set; }
		public string APIKey { get; private set; }
		public string DefaultRegion { get; private set; }
		public bool UseInternalURL { get; private set; }
		public string ContainerPrefix { get; private set; }

		private string _configSectionName { get; set;}

		private InWorldz.Data.Assets.Stratus.CoreExt.ExtendedCloudFilesProvider _provider = null;

		public void Dispose() {
			_provider = null;
		}

		public void Initialize(IConfig settings) {
			_configSectionName = settings.Name;

			Username = settings.GetString("Username", string.Empty);
			APIKey = settings.GetString("APIKey", string.Empty);
			DefaultRegion = settings.GetString("DefaultRegion", string.Empty);
			UseInternalURL = settings.GetBoolean("UseInternalURL", true);
			ContainerPrefix = settings.GetString("ContainerPrefix", string.Empty);

			var identity = new CloudIdentity { Username = Username, APIKey = APIKey };
			var restService = new InWorldz.Data.Assets.Stratus.CoreExt.ExtendedJsonRestServices(DEFAULT_READ_TIMEOUT, DEFAULT_WRITE_TIMEOUT);
			_provider = new InWorldz.Data.Assets.Stratus.CoreExt.ExtendedCloudFilesProvider(identity, DefaultRegion, null, restService);

			//warm up
			_provider.GetAccountHeaders(useInternalUrl: UseInternalURL, region: DefaultRegion);

			LOG.Info($"[CF_SERVER] [{_configSectionName}] CF connection prepared for region {DefaultRegion} and prefix {ContainerPrefix} under user {Username}'.");
		}

		public async Task<AssetBase> RequestAssetAsync(UUID assetID) {
			return null;
		}

		public AssetBase RequestAssetSync(UUID assetID) {
			string assetIdStr = assetID.ToString();

			using (var memStream = new MemoryStream()) {
				try {
					WarnIfLongOperation($"GetObject for {assetID}", () => _provider.GetObject(GenerateContainerName(assetIdStr), GenerateAssetObjectName(assetIdStr), memStream, useInternalUrl: UseInternalURL, region: DefaultRegion));
				}
				catch {
					return null; // Just skip out for this round if there was an error.
				}

				memStream.Position = 0;

				var rawAsset = ProtoBuf.Serializer.Deserialize<InWorldz.Data.Assets.Stratus.StratusAsset>(memStream);

				if (rawAsset?.Data == null) {
					throw new InvalidOperationException($"[CF_SERVER] [{_configSectionName}] Asset deserialization failed. Asset ID: {assetID}, Stream Len: {memStream.Length}");
				}

				return rawAsset.ToAssetBase();
			}
		}

		/// <summary>
		/// CF containers are PREFIX_#### where we use the first N chars of the hex representation
		/// of the asset ID to partition the space. The hex alpha chars in the container name are uppercase
		/// </summary>
		/// <param name="assetId"></param>
		/// <returns></returns>
		private string GenerateContainerName(string assetId) {
			return ContainerPrefix + assetId.Substring(0, CONTAINER_UUID_PREFIX_LEN).ToUpper();
		}

		/// <summary>
		/// The object name is defined by the assetId, dashes stripped, with the .asset prefix
		/// </summary>
		/// <param name="assetId"></param>
		/// <returns></returns>
		private static string GenerateAssetObjectName(string assetId) {
			return assetId.Replace("-", String.Empty).ToLower() + ".asset";
		}


		private void WarnIfLongOperation(string opName, Action operation) {
			const ulong WARNING_TIME = 5000;

			ulong startTime = Util.GetLongTickCount();
			operation();
			ulong time = Util.GetLongTickCount() - startTime;

			if (time >= WARNING_TIME) {
				LOG.Warn($"[CF_SERVER] [{_configSectionName}] Slow CF operation {opName} took {time} ms.");
			}
		}
	}
}