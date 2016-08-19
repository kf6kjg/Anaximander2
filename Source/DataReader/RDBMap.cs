﻿// MyClass.cs
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
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using log4net;
using MySql.Data.MySqlClient;
using Nini.Config;

namespace DataReader {
	public class RDBMap {
		//private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly Dictionary<string, Region> MAP = new Dictionary<string, Region>();
		private readonly List<string> DEAD_REGION_IDS = new List<string>();

		private readonly string CONNECTION_STRING;
		private readonly string RDB_CONNECTION_STRING_PARTIAL;

		#region Constructors

		public RDBMap(IConfigSource config) {
			var data_config = config.Configs["Database"];

			CONNECTION_STRING = data_config.GetString("MasterDatabaseConnectionString", CONNECTION_STRING).Trim();

			if (String.IsNullOrWhiteSpace(CONNECTION_STRING)) {
				// Analysis disable once NotResolvedInText
				throw new ArgumentNullException("MasterDatabaseConnectionString", "Missing or empty key in section [Database] of the ini file.");
			}

			RDB_CONNECTION_STRING_PARTIAL = data_config.GetString("RDBConnectionStringPartial", RDB_CONNECTION_STRING_PARTIAL).Trim();

			if (String.IsNullOrWhiteSpace(RDB_CONNECTION_STRING_PARTIAL)) {
				// Analysis disable once NotResolvedInText
				throw new ArgumentNullException("RDBConnectionStringPartial", "Missing or empty key in section [Database] of the ini file.");
			}

			if (!RDB_CONNECTION_STRING_PARTIAL.Contains("Data Source")) {
				RDB_CONNECTION_STRING_PARTIAL = "Data Source={0};" + RDB_CONNECTION_STRING_PARTIAL;
			}

			if (MAP.Count > 0) { // No sense in trying to remove old entries when there are no entries!
				DeleteOldMapEntries();
			}

			UpdateMap();
		}

		#endregion

		#region Public Methods

		public void DeleteOldMapEntries() {
			var active_regions = new List<string>();

			using (MySqlConnection conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				using (MySqlCommand cmd = conn.CreateCommand()) {
					/* Gets the full list of known regions.  Any region IDs that are not in this list are to be removed. */
					cmd.CommandText = @"SELECT
						regionID
					FROM
						estate_map
					ORDER BY
						region_id";
					IDataReader reader = DBHelpers.ExecuteReader(cmd);

					try {
						while (reader.Read()) {
							active_regions.Add(Convert.ToString(reader["region_id"]));
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			Parallel.ForEach(MAP.Keys.Except(active_regions).ToList(), (id) => {
				DEAD_REGION_IDS.Add(id);
				MAP.Remove(id);
			});
		}

		public void UpdateMap() {
			var regions_by_rdb = new Dictionary<string, Dictionary<string, RegionInfo>>();
			RegionInfo new_entry;
			Dictionary<string, RegionInfo> region_list;

			using (MySqlConnection conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				using (MySqlCommand cmd = conn.CreateCommand()) {
					/* Gets the full list of what regions are on what host.
					A null host_name indicates that that region's data is on this host, otherwise contains the host for the region's data.
					A null regionName indicates that the region is shut down, otherwise that the region is up or crashed.
					*/
					cmd.CommandText = @"SELECT
						regionID, host_name, regionName, locX, locY, sizeX, sizeY, serverIP, serverPort
					FROM
						estate_map
						LEFT OUTER JOIN (
							SELECT
								host_name, region_id
							FROM
								RdbHosts
								INNER JOIN RegionRdbMapping ON id = rdb_host_id
						) AS rdbs ON regionID = region_id
						LEFT OUTER JOIN regions ON regionID = uuid
					ORDER BY
						host_name, region_id";
					IDataReader reader = DBHelpers.ExecuteReader(cmd);

					try {
						while (reader.Read()) {
							var rdbhost = Convert.ToString(reader["host_name"]);

							if (String.IsNullOrWhiteSpace(rdbhost)) {
								rdbhost = conn.DataSource;
							}

							rdbhost = String.Format(RDB_CONNECTION_STRING_PARTIAL, rdbhost);

							if (regions_by_rdb.ContainsKey(rdbhost)) {
								region_list = regions_by_rdb[rdbhost];
							}
							else {
								region_list = new Dictionary<string, RegionInfo>();
								regions_by_rdb.Add(rdbhost, region_list);
							}

							string region_id = Convert.ToString(reader["regionID"]);

							// Check to see if the map already has this entry and if the new entry is shut down.
							if (region_list.ContainsKey(region_id) && Convert.IsDBNull(reader["regionName"])) {
								new_entry = region_list[region_id];

								new_entry.RDBConnectionString = rdbhost; // Update the RDB connection
							}
							else { // The DB has the freshest information.  Does not imply the region is online - it could have crashed.
								new_entry.regionId = region_id;
								new_entry.RDBConnectionString = rdbhost;
								new_entry.regionName = reader.IsDBNull(reader.GetOrdinal("regionName")) ? null : Convert.ToString(reader["regionName"]);
								new_entry.locationX = GetDBValueOrNull<int>(reader, "locX");
								new_entry.locationY = GetDBValueOrNull<int>(reader, "locY");
								new_entry.sizeX = GetDBValueOrNull<int>(reader, "sizeX");
								new_entry.sizeY = GetDBValueOrNull<int>(reader, "sizeY");
								new_entry.serverIP = reader.IsDBNull(reader.GetOrdinal("serverIP")) ? null : Convert.ToString(reader["serverIP"]);
								new_entry.serverPort = GetDBValueOrNull<int>(reader, "serverPort");
							}

							region_list.Add(region_id, new_entry);
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			Parallel.ForEach(regions_by_rdb.Keys.ToList(), (rdb_connection_string) => {
				RegionTerrainData data;
				string region_id;

				using (MySqlConnection conn = DBHelpers.GetConnection(rdb_connection_string)) {
					using (MySqlCommand cmd = conn.CreateCommand()) {
						cmd.CommandText = @"SELECT RegionUUID, terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_sw, elevation_2_sw, elevation_1_se, elevation_2_se, water_height FROM regionsettings;";
						IDataReader reader = DBHelpers.ExecuteReader(cmd);

						try {
							while (reader.Read()) {
								region_id = GetDBValue(reader, "RegionUUID");

								data.terrainTexture1 = GetDBValue(reader, "terrain_texture_1");
								data.terrainTexture2 = GetDBValue(reader, "terrain_texture_2");
								data.terrainTexture3 = GetDBValue(reader, "terrain_texture_3");
								data.terrainTexture4 = GetDBValue(reader, "terrain_texture_4");

								data.elevation1NW = GetDBValue<double>(reader, "elevation_1_nw");
								data.elevation2NW = GetDBValue<double>(reader, "elevation_2_nw");
								data.elevation1NE = GetDBValue<double>(reader, "elevation_1_ne");
								data.elevation2NE = GetDBValue<double>(reader, "elevation_2_ne");
								data.elevation1SW = GetDBValue<double>(reader, "elevation_1_sw");
								data.elevation2SW = GetDBValue<double>(reader, "elevation_2_sw");
								data.elevation1SE = GetDBValue<double>(reader, "elevation_1_se");
								data.elevation2SE = GetDBValue<double>(reader, "elevation_2_se");

								data.waterHeight = GetDBValue<double>(reader, "water_height");

								MAP.Add(region_id, new Region(regions_by_rdb[rdb_connection_string][region_id], data));
							}
						}
						finally {
							reader.Close();
						}
					}
				}
			});
		}

		public int GetRegionCount() {
			return MAP.Count;
		}

		public IEnumerable<string> GetRegionUUIDsAsStrings() {
			return MAP.Keys;
		}

		public Region GetRegionByUUID(string uuid) {
			return MAP[uuid];
		}

		#endregion

		private static T? GetDBValueOrNull<T>(IDataRecord reader, string name) where T: struct {
			var result = new T?();
			try {
				var ordinal = reader.GetOrdinal(name);
				if (!reader.IsDBNull(ordinal)) {
					result = (T) Convert.ChangeType(reader.GetValue(ordinal), typeof(T));
				}
			}
			// Analysis disable once EmptyGeneralCatchClause
			catch {
			}

			return result;
		}

		private static T GetDBValue<T>(IDataRecord reader, string name) where T: struct {
			var result = new T();
			try {
				var ordinal = reader.GetOrdinal(name);
				if (!reader.IsDBNull(ordinal)) {
					result = (T) Convert.ChangeType(reader.GetValue(ordinal), typeof(T));
				}
			}
			// Analysis disable once EmptyGeneralCatchClause
			catch {
			}

			return result;
		}

		private static string GetDBValue(IDataRecord reader, string name) {
			var ordinal = reader.GetOrdinal(name);
			return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
		}
	}
}

