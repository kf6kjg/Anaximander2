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
using System.Data;
using System.IO;
using System.Net;
using log4net;
using MySql.Data.MySqlClient;
using Nini.Config;

namespace DataReader {
	public class Region {
		//private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string SQLReadAllRegions = @"SELECT
				terrain_texture_1,/*0*/
				terrain_texture_2,/*1*/
				terrain_texture_3,/*2*/
				terrain_texture_4,/*3*/
				elevation_1_nw,/*4*/
				elevation_2_nw,/*5*/
				elevation_1_ne,/*6*/
				elevation_2_ne,/*7*/
				elevation_1_sw,/*8*/
				elevation_2_sw,/*9*/
				elevation_1_se,/*10*/
				elevation_2_se,/*11*/
				water_height/*12*/
			FROM
				regionsettings
			WHERE
				RegionUUID = ?UUID";

		#region Public Properties and Accessors

		public string RegionID { get; private set; }

		public string RDBConnectionString { get; private set; }

		public string RegionName { get; private set; } = null;

		/// <summary>
		/// Queries the region server simstatus to detect if the region is online and accessable at this moment.
		/// </summary>
		/// <value><c>true</c> if this instance is region currently up; otherwise, <c>false</c>.</value>
		public bool IsRegionCurrentlyUp {
			get {
				if (ServerIP != null) {
					var wrGETURL = WebRequest.Create($"http://{ServerIP}:{ServerPort}/simstatus/");
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

		public int? LocationX { get; private set; } = null;

		public int? LocationY { get; private set; } = null;

		public int? SizeX { get; private set; } = null;

		public int? SizeY { get; private set; } = null;

		public string ServerIP { get; private set; } = null;

		public int? ServerPort { get; private set; } = null;


		public string TerrainTexture1 { get; private set; } = null;

		public string TerrainTexture2 { get; private set; } = null;

		public string TerrainTexture3 { get; private set; } = null;

		public string TerrainTexture4 { get; private set; } = null;

		public double Elevation1NW { get; private set; } = 0d;
		public double Elevation2NW { get; private set; } = 0d;
		public double Elevation1NE { get; private set; } = 0d;
		public double Elevation2NE { get; private set; } = 0d;
		public double Elevation1SW { get; private set; } = 0d;
		public double Elevation2SW { get; private set; } = 0d;
		public double Elevation1SE { get; private set; } = 0d;
		public double Elevation2SE { get; private set; } = 0d;
		public double WaterHeight { get; private set; } = 0d;

		#endregion

		#region Private Properties

		#endregion

		#region Constructors
		public Region(
			string regionID, string rdbConnectionString, string regionName,
			int? locX, int? locY,
			int? sizeX, int? sizeY,
			string serverIP, int? serverPort
		) {
			RegionID = regionID;
			RDBConnectionString = rdbConnectionString;
			RegionName = regionName;

			LocationX = locX;
			LocationY = locY;

			SizeX = sizeX;
			SizeY = sizeY;

			ServerIP = serverIP;
			ServerPort = serverPort;

			if (regionName != null) { // If the region name is null, then so is a lot of other stuff.
				using (MySqlConnection conn = DBHelpers.GetConnection(rdbConnectionString)) {
					using (MySqlCommand cmd = conn.CreateCommand()) {
						cmd.CommandText = SQLReadAllRegions;
						cmd.Parameters.AddWithValue("UUID", RegionID);
						IDataReader reader = DBHelpers.ExecuteReader(cmd);

						try {
							if (reader.Read()) {
								TerrainTexture1 = reader.GetString(0);
								TerrainTexture2 = reader.GetString(1);
								TerrainTexture3 = reader.GetString(2);
								TerrainTexture4 = reader.GetString(3);

								Elevation1NW = reader.GetDouble(4);
								Elevation2NW = reader.GetDouble(5);
								Elevation1NE = reader.GetDouble(6);
								Elevation2NE = reader.GetDouble(7);
								Elevation1SW = reader.GetDouble(8);
								Elevation2SW = reader.GetDouble(9);
								Elevation1SE = reader.GetDouble(10);
								Elevation2SE = reader.GetDouble(11);

								WaterHeight = reader.GetDouble(12);
							}
						}
						finally {
							reader.Close();
						}
					}
				}
			}
		}

		#endregion
	}
}

