// Region.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2016 Richard Curtice
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
using System.Collections;
using System.Data;
using System.IO;
using System.Net;
using log4net;
using MySql.Data.MySqlClient;
using Nini.Config;

namespace DataReader {
	public class Region {
		//private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		#region Public Properties and Accessors

		public string regionId { get { return _info.regionId; } private set { _info.regionId = value; } }

		public string regionName { get { return _info.regionName; } private set { _info.regionName = value; } }

		/// <summary>
		/// Queries the region server simstatus to detect if the region is online and accessable at this moment.
		/// </summary>
		/// <value><c>true</c> if this instance is region currently up; otherwise, <c>false</c>.</value>
		public bool isRegionCurrentlyUp {
			get {
				if (serverIP != null) {
					var wrGETURL = WebRequest.Create($"http://{serverIP}:{serverPort}/simstatus/");
					try {
						// Total time for 1000 passing tests against a server on the Internet: 42352ms, for an avg of 42.352 ms per check.
						var objStream = wrGETURL.GetResponse().GetResponseStream();
						var objReader = new StreamReader(objStream);
						return objReader.ReadLine() == "OK";
					}
					catch (WebException) {
						// Total time for 1000 fail tests against localhost: 848ms, for an avg of 0.848 ms per check.
						return false;
					}
				}

				// Total time for 1000 False tests against nothing: 2ms, for an avg of 0.002 ms per check.
				return false;
			}
		}

		public int? locationX { get { return _info.locationX; } private set { _info.locationX = value; } }

		public int? locationY { get { return _info.locationY; } private set { _info.locationY = value; } }

		public int? sizeX { get { return _info.sizeX; } private set { _info.sizeX = value; } }

		public int? sizeY { get { return _info.sizeY; } private set { _info.sizeY = value; } }

		public string serverIP { get { return _info.serverIP; } private set { _info.serverIP = value; } }

		public int? serverPort { get { return _info.serverPort; } private set { _info.serverPort = value; } }


		public string terrainTexture1 { get { return _terrainData.terrainTexture1; } private set { _terrainData.terrainTexture1 = value; } }

		public string terrainTexture2 { get { return _terrainData.terrainTexture2; } private set { _terrainData.terrainTexture2 = value; } }

		public string terrainTexture3 { get { return _terrainData.terrainTexture3; } private set { _terrainData.terrainTexture3 = value; } }

		public string terrainTexture4 { get { return _terrainData.terrainTexture4; } private set { _terrainData.terrainTexture4 = value; } }

		public double elevation1NW { get { return _terrainData.elevation1NW; } private set { _terrainData.elevation1NW = value; } }

		public double elevation2NW { get { return _terrainData.elevation2NW; } private set { _terrainData.elevation2NW = value; } }

		public double elevation1NE { get { return _terrainData.elevation1NE; } private set { _terrainData.elevation1NE = value; } }

		public double elevation2NE { get { return _terrainData.elevation2NE; } private set { _terrainData.elevation2NE = value; } }

		public double elevation1SW { get { return _terrainData.elevation1SW; } private set { _terrainData.elevation1SW = value; } }

		public double elevation2SW { get { return _terrainData.elevation2SW; } private set { _terrainData.elevation2SW = value; } }

		public double elevation1SE { get { return _terrainData.elevation1SE; } private set { _terrainData.elevation1SE = value; } }

		public double elevation2SE { get { return _terrainData.elevation2SE; } private set { _terrainData.elevation2SE = value; } }

		public double waterHeight { get { return _terrainData.waterHeight; } private set { _terrainData.waterHeight = value; } }

		public double[,] heightmapData { get { return _terrainData.heightmap; } private set { _terrainData.heightmap = value; }} // Yes, the elements are stull mutable, but I don't feel like making my life that difficult ATM.

		#endregion

		#region Private Properties

		private RegionInfo _info;
		private RegionTerrainData _terrainData;

		#endregion

		#region Constructors

		public Region(RegionInfo info, RegionTerrainData terrain_data) {
			_info = info;
			_terrainData = terrain_data;
		}

		public Region(
			string region_id, string rdb_connection_string, string region_name,
			int? loc_x, int? loc_y,
			int? size_x, int? size_y,
			string server_ip, int? server_port
		) {
			regionId = region_id;
			_info.RDBConnectionString = rdb_connection_string;
			regionName = region_name;

			locationX = loc_x;
			locationY = loc_y;

			sizeX = size_x;
			sizeX = size_y;

			serverIP = server_ip;
			serverPort = server_port;
		}

		#endregion


	}
}

