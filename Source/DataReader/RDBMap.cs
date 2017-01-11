// RDBMap.cs
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
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using log4net;
using MySql.Data.MySqlClient;
using Nini.Config;
using System.Diagnostics.Contracts;

namespace DataReader {
	public class RDBMap {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly Dictionary<string, Region> MAP = new Dictionary<string, Region>();
		private readonly Dictionary<long, Region> COORD_MAP = new Dictionary<long, Region>();
		private readonly List<string> DEAD_REGION_IDS = new List<string>();

		private readonly string CONNECTION_STRING;
		private readonly string RDB_CONNECTION_STRING_PARTIAL;

		#region Constructors

		public RDBMap(IConfigSource config) {
			var data_config = config.Configs["Database"];

			CONNECTION_STRING = data_config.GetString("MasterDatabaseConnectionString", CONNECTION_STRING).Trim();

			if (string.IsNullOrWhiteSpace(CONNECTION_STRING)) {
				// Analysis disable once NotResolvedInText
				throw new ArgumentNullException("MasterDatabaseConnectionString", "Missing or empty key in section [Database] of the ini file.");
			}

			RDB_CONNECTION_STRING_PARTIAL = data_config.GetString("RDBConnectionStringPartial", RDB_CONNECTION_STRING_PARTIAL).Trim();

			if (string.IsNullOrWhiteSpace(RDB_CONNECTION_STRING_PARTIAL)) {
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

			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				using (var cmd = conn.CreateCommand()) {
					/* Gets the full list of known regions.  Any region IDs that are not in this list are to be removed. */
					cmd.CommandText = @"SELECT
						regionID
					FROM
						estate_map
					ORDER BY
						region_id";
					var reader = DBHelpers.ExecuteReader(cmd);

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
				Region reg;
				if (MAP.TryGetValue(id, out reg) && reg.locationX != null && reg.locationY != null) {
					COORD_MAP.Remove(CoordToIndex((int)reg.locationX, (int)reg.locationY));
				}
				MAP.Remove(id);
			});
		}

		public void UpdateMap() {
			var regions_by_rdb = new Dictionary<string, Dictionary<string, RegionInfo>>();
			RegionInfo new_entry;
			Dictionary<string, RegionInfo> region_list;

			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				using (var cmd = conn.CreateCommand()) {
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
					var reader = DBHelpers.ExecuteReader(cmd);

					try {
						while (reader.Read()) {
							var rdbhost = Convert.ToString(reader["host_name"]);

							if (string.IsNullOrWhiteSpace(rdbhost)) {
								// Not on an RDB, use the main.
								rdbhost = conn.DataSource;
							}
							else {
								// Got an RDB, normallize the case and format.
								rdbhost = string.Format(RDB_CONNECTION_STRING_PARTIAL, rdbhost.ToLowerInvariant());
							}

							if (regions_by_rdb.ContainsKey(rdbhost)) {
								region_list = regions_by_rdb[rdbhost];
							}
							else {
								region_list = new Dictionary<string, RegionInfo>();
								regions_by_rdb.Add(rdbhost, region_list);
							}

							var region_id = Convert.ToString(reader["regionID"]);

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

			#if DEBUG
			var options = new ParallelOptions { MaxDegreeOfParallelism = -1 }; // -1 means full parallel.  1 means non-parallel.

			Parallel.ForEach(regions_by_rdb.Keys.ToList(), options, (rdb_connection_string) => {
			#else
			Parallel.ForEach(regions_by_rdb.Keys.ToList(), (rdb_connection_string) => {
			#endif
				RegionTerrainData data;
				string region_id;

				using (var conn = DBHelpers.GetConnection(rdb_connection_string)) {
					using (var cmd = conn.CreateCommand()) {
						cmd.CommandText = @"SELECT RegionUUID, terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_sw, elevation_2_sw, elevation_1_se, elevation_2_se, water_height, Heightfield FROM regionsettings natural join terrain;";
						var reader = DBHelpers.ExecuteReader(cmd);

						try {
							while (reader.Read()) {
								region_id = GetDBValue(reader, "RegionUUID");

								if (!regions_by_rdb[rdb_connection_string].ContainsKey(region_id)) {
									LOG.Info($"Either of the regionsettings and/or terrain tables on one of the rdb hosts has an entry for region id '{region_id}' that does not exist in the estates table.");
									continue;
								}

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

								data.heightmap = new double[256, 256];
								data.heightmap.Initialize();

								var br = new BinaryReader(new MemoryStream((byte[])reader["Heightfield"]));
								for (int x = 0; x < data.heightmap.GetLength(0); x++)
								{
									for (int y = 0; y < data.heightmap.GetLength(1); y++)
									{
										data.heightmap[x, y] = br.ReadDouble();
									}
								}
								LOG.Info($"[REGION DB]: Loaded data for region {region_id}");

								var info = regions_by_rdb[rdb_connection_string][region_id];
								var region = new Region(info, data);

								MAP.Add(region_id, region);

								// Not all regions returned have a position, after all some could be in a crashed state.
								if (info.locationX != null) {
									COORD_MAP.Add(CoordToIndex((int)info.locationX, (int)info.locationY), region);
								}
							}
						}
						finally {
							reader.Close();
						}
					}
				}
			});

			#if DEBUG
			Parallel.ForEach(regions_by_rdb.Keys.ToList(), options, (rdb_connection_string) => {
			#else
			Parallel.ForEach(regions_by_rdb.Keys.ToList(), (rdb_connection_string) => {
			#endif
				string region_id;
				Region region;

				using (var conn = DBHelpers.GetConnection(rdb_connection_string)) {
					using (var cmd = conn.CreateCommand()) {
						cmd.CommandText = @"SELECT
	RegionUUID,
	ObjectFlags,
	State,
	PositionX, PositionY, PositionZ,
	GroupPositionX, GroupPositionY, GroupPositionZ,
	ScaleX, ScaleY, ScaleZ,
	RotationX, RotationY, RotationZ, RotationW,
	RootRotationX, RootRotationY, RootRotationZ, RootRotationW,
	Texture
FROM
	prims pr
	NATURAL JOIN primshapes
	LEFT JOIN (
		SELECT
			RotationX AS RootRotationX,
			RotationY AS RootRotationY,
			RotationZ AS RootRotationZ,
			RotationW AS RootRotationW,
			SceneGroupID
		FROM
			prims pr
		WHERE
			LinkNumber = 1
	) AS rootprim ON rootprim.SceneGroupID = pr.SceneGroupID
WHERE
	GroupPositionZ < 766 /* = max terrain height + render height */
	AND LENGTH(Texture) > 0
	AND ObjectFlags & (0 | 0x40000 | 0x20000) = 0
	AND ScaleX > 1.0
	AND ScaleY > 1.0
	AND ScaleZ > 1.0
	AND PCode NOT IN (255, 111, 95)
ORDER BY
	GroupPositionZ, PositionZ
;";
						var reader = DBHelpers.ExecuteReader(cmd);

						try {
							while (reader.Read()) {
								region_id = GetDBValue(reader, "RegionUUID");

								if (!MAP.ContainsKey(region_id)) {
									LOG.Info($"The prims table on one of the rdb hosts has an entry for region id '{region_id}' that does not exist in the estates, regionsettings, and terrain tables.");
									continue;
								}

								region = MAP[region_id];

								var data = new RegionPrimData() {
									ObjectFlags = GetDBValue<int>(reader, "ObjectFlags"),
									State = GetDBValue<int>(reader, "State"),
									PositionX = GetDBValue<double>(reader, "PositionX"),
									PositionY = GetDBValue<double>(reader, "PositionY"),
									PositionZ = GetDBValue<double>(reader, "PositionZ"),
									GroupPositionX = GetDBValue<double>(reader, "GroupPositionX"),
									GroupPositionY = GetDBValue<double>(reader, "GroupPositionY"),
									GroupPositionZ = GetDBValue<double>(reader, "GroupPositionZ"),
									ScaleX = GetDBValue<double>(reader, "ScaleX"),
									ScaleY = GetDBValue<double>(reader, "ScaleY"),
									ScaleZ = GetDBValue<double>(reader, "ScaleZ"),
									RotationX = GetDBValue<double>(reader, "RotationX"),
									RotationY = GetDBValue<double>(reader, "RotationY"),
									RotationZ = GetDBValue<double>(reader, "RotationZ"),
									RotationW = GetDBValue<double>(reader, "RotationW"),
									RootRotationX = GetDBValueOrNull<double>(reader, "RootRotationX"),
									RootRotationY = GetDBValueOrNull<double>(reader, "RootRotationY"),
									RootRotationZ = GetDBValueOrNull<double>(reader, "RootRotationZ"),
									RootRotationW = GetDBValueOrNull<double>(reader, "RootRotationW"),
									Texture = (byte[])reader["Texture"],
								};

								region.AddPrim(new Prim(ref data));
							}
						}
						finally {
							reader.Close();
						}
					}
				}
			});
		}

		public bool CreateRegion(string region_id) {
			var info = new RegionInfo();
			RegionTerrainData terrain_data;
			Region region;

			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				using (var cmd = conn.CreateCommand()) {
					/* Gets the full list of what regions are on what host.
					A null host_name indicates that that region's data is on this host, otherwise contains the host for the region's data.
					A null regionName indicates that the region is shut down, otherwise that the region is up or crashed.
					*/
					cmd.CommandText = @"SELECT
							(
								SELECT
									host_name
								FROM
									RdbHosts
									INNER JOIN RegionRdbMapping ON id = rdb_host_id
								WHERE
									region_id = @region_id
							) host_name, regionName, locX, locY, sizeX, sizeY, serverIP, serverPort
						FROM
							regions
						WHERE
							uuid = @region_id
					";
					cmd.Parameters.AddWithValue("region_id", region_id);
					cmd.Prepare();
					var reader = DBHelpers.ExecuteReader(cmd);

					try {
						if (!reader.Read()) {
							// If there are no valid results, then the requested region does not exist and there's no point in continuing.
							LOG.Info($"Region '{region_id}' does not exist in the database.  Aborting creation.");
							return false;
						}

						var rdbhost = Convert.ToString(reader["host_name"]);

						if (string.IsNullOrWhiteSpace(rdbhost)) {
							rdbhost = conn.DataSource;
						}

						rdbhost = string.Format(RDB_CONNECTION_STRING_PARTIAL, rdbhost);

						// Check to see if the map already has this entry and if the new entry is shut down.
						if (Convert.IsDBNull(reader["regionName"])) {
							info.RDBConnectionString = rdbhost; // Update the RDB connection
						}
						else { // The DB has the freshest information.  Does not imply the region is online - it could have crashed.
							info.regionId = region_id;
							info.RDBConnectionString = rdbhost;
							info.regionName = reader.IsDBNull(reader.GetOrdinal("regionName")) ? null : Convert.ToString(reader["regionName"]);
							info.locationX = GetDBValueOrNull<int>(reader, "locX");
							info.locationY = GetDBValueOrNull<int>(reader, "locY");
							info.sizeX = GetDBValueOrNull<int>(reader, "sizeX");
							info.sizeY = GetDBValueOrNull<int>(reader, "sizeY");
							info.serverIP = reader.IsDBNull(reader.GetOrdinal("serverIP")) ? null : Convert.ToString(reader["serverIP"]);
							info.serverPort = GetDBValueOrNull<int>(reader, "serverPort");
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			using (var conn = DBHelpers.GetConnection(info.RDBConnectionString)) {
				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_sw, elevation_2_sw, elevation_1_se, elevation_2_se, water_height, Heightfield
						FROM regionsettings NATURAL JOIN terrain
						WHERE RegionUUID = @region_id
					";
					cmd.Parameters.AddWithValue("region_id", region_id);
					cmd.Prepare();
					var reader = DBHelpers.ExecuteReader(cmd);

					try {
						reader.Read();

						terrain_data.terrainTexture1 = GetDBValue(reader, "terrain_texture_1");
						terrain_data.terrainTexture2 = GetDBValue(reader, "terrain_texture_2");
						terrain_data.terrainTexture3 = GetDBValue(reader, "terrain_texture_3");
						terrain_data.terrainTexture4 = GetDBValue(reader, "terrain_texture_4");

						terrain_data.elevation1NW = GetDBValue<double>(reader, "elevation_1_nw");
						terrain_data.elevation2NW = GetDBValue<double>(reader, "elevation_2_nw");
						terrain_data.elevation1NE = GetDBValue<double>(reader, "elevation_1_ne");
						terrain_data.elevation2NE = GetDBValue<double>(reader, "elevation_2_ne");
						terrain_data.elevation1SW = GetDBValue<double>(reader, "elevation_1_sw");
						terrain_data.elevation2SW = GetDBValue<double>(reader, "elevation_2_sw");
						terrain_data.elevation1SE = GetDBValue<double>(reader, "elevation_1_se");
						terrain_data.elevation2SE = GetDBValue<double>(reader, "elevation_2_se");

						terrain_data.waterHeight = GetDBValue<double>(reader, "water_height");

						terrain_data.heightmap = new double[256, 256];
						terrain_data.heightmap.Initialize();

						var br = new BinaryReader(new MemoryStream((byte[])reader["Heightfield"]));
						for (int x = 0; x < terrain_data.heightmap.GetLength(0); x++) {
							for (int y = 0; y < terrain_data.heightmap.GetLength(1); y++) {
								terrain_data.heightmap[x, y] = br.ReadDouble();
							}
						}
						LOG.Info($"[REGION DB]: Loaded data for region {region_id}");
					}
					finally {
						reader.Close();
					}
				}

				region = new Region(info, terrain_data);

				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT
							ObjectFlags,
							State,
							PositionX, PositionY, PositionZ,
							GroupPositionX, GroupPositionY, GroupPositionZ,
							ScaleX, ScaleY, ScaleZ,
							RotationX, RotationY, RotationZ, RotationW,
							RootRotationX, RootRotationY, RootRotationZ, RootRotationW,
							Texture
						FROM
							prims pr
							NATURAL JOIN primshapes
							LEFT JOIN (
								SELECT
									RotationX AS RootRotationX,
									RotationY AS RootRotationY,
									RotationZ AS RootRotationZ,
									RotationW AS RootRotationW,
									SceneGroupID
								FROM
									prims pr
								WHERE
									LinkNumber = 1
							) AS rootprim ON rootprim.SceneGroupID = pr.SceneGroupID
						WHERE
							GroupPositionZ < 766 /* = max terrain height + render height */
							AND LENGTH(Texture) > 0
							AND ObjectFlags & (0 | 0x40000 | 0x20000) = 0
							AND ScaleX > 1.0
							AND ScaleY > 1.0
							AND ScaleZ > 1.0
							AND PCode NOT IN (255, 111, 95)
							AND RegionUUID = @region_id
						ORDER BY
							GroupPositionZ, PositionZ
					";
					cmd.Parameters.AddWithValue("region_id", region_id);
					cmd.Prepare();
					var reader = DBHelpers.ExecuteReader(cmd);

					try {
						while (reader.Read()) {
							var prim_data = new RegionPrimData() {
								ObjectFlags = GetDBValue<int>(reader, "ObjectFlags"),
								State = GetDBValue<int>(reader, "State"),
								PositionX = GetDBValue<double>(reader, "PositionX"),
								PositionY = GetDBValue<double>(reader, "PositionY"),
								PositionZ = GetDBValue<double>(reader, "PositionZ"),
								GroupPositionX = GetDBValue<double>(reader, "GroupPositionX"),
								GroupPositionY = GetDBValue<double>(reader, "GroupPositionY"),
								GroupPositionZ = GetDBValue<double>(reader, "GroupPositionZ"),
								ScaleX = GetDBValue<double>(reader, "ScaleX"),
								ScaleY = GetDBValue<double>(reader, "ScaleY"),
								ScaleZ = GetDBValue<double>(reader, "ScaleZ"),
								RotationX = GetDBValue<double>(reader, "RotationX"),
								RotationY = GetDBValue<double>(reader, "RotationY"),
								RotationZ = GetDBValue<double>(reader, "RotationZ"),
								RotationW = GetDBValue<double>(reader, "RotationW"),
								RootRotationX = GetDBValueOrNull<double>(reader, "RootRotationX"),
								RootRotationY = GetDBValueOrNull<double>(reader, "RootRotationY"),
								RootRotationZ = GetDBValueOrNull<double>(reader, "RootRotationZ"),
								RootRotationW = GetDBValueOrNull<double>(reader, "RootRotationW"),
								Texture = (byte[])reader["Texture"],
							};

							region.AddPrim(new Prim(ref prim_data));
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			MAP[region_id] = region; // Add or update.

			// Not all regions returned have a position, after all some could be in a crashed state.
			if (region.locationX != null) {
				COORD_MAP[CoordToIndex((int)region.locationX, (int)region.locationY)] = region; // Add or update.
			}

			return true;
		}

		public void UpdateRegionInfo(string region_id) {
			var region = GetRegionByUUID(region_id);

			if (region == null) {
				// This region is missing...
				CreateRegion(region_id);
				return;
			}

			var info = new RegionInfo();

			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				using (var cmd = conn.CreateCommand()) {
					/* Gets the full list of what regions are on what host.
					A null host_name indicates that that region's data is on this host, otherwise contains the host for the region's data.
					A null regionName indicates that the region is shut down, otherwise that the region is up or crashed.
					*/
					cmd.CommandText = @"SELECT
							(
								SELECT
									host_name
								FROM
									RdbHosts
									INNER JOIN RegionRdbMapping ON id = rdb_host_id
								WHERE
									region_id = @region_id
							) host_name, regionName, locX, locY, sizeX, sizeY, serverIP, serverPort
						FROM
							regions
						WHERE
							uuid = @region_id
					";
					cmd.Parameters.AddWithValue("region_id", region_id);
					cmd.Prepare();
					var reader = DBHelpers.ExecuteReader(cmd);

					try {
						reader.Read();
						var rdbhost = Convert.ToString(reader["host_name"]);

						if (string.IsNullOrWhiteSpace(rdbhost)) {
							rdbhost = conn.DataSource;
						}

						rdbhost = string.Format(RDB_CONNECTION_STRING_PARTIAL, rdbhost);

						// Check to see if the map already has this entry and if the new entry is shut down.
						if (Convert.IsDBNull(reader["regionName"])) {
							info.RDBConnectionString = rdbhost; // Update the RDB connection
						}
						else { // The DB has the freshest information.  Does not imply the region is online - it could have crashed.
							info.regionId = region_id;
							info.RDBConnectionString = rdbhost;
							info.regionName = reader.IsDBNull(reader.GetOrdinal("regionName")) ? null : Convert.ToString(reader["regionName"]);
							info.locationX = GetDBValueOrNull<int>(reader, "locX");
							info.locationY = GetDBValueOrNull<int>(reader, "locY");
							info.sizeX = GetDBValueOrNull<int>(reader, "sizeX");
							info.sizeY = GetDBValueOrNull<int>(reader, "sizeY");
							info.serverIP = reader.IsDBNull(reader.GetOrdinal("serverIP")) ? null : Convert.ToString(reader["serverIP"]);
							info.serverPort = GetDBValueOrNull<int>(reader, "serverPort");
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			region = new Region(region, info);

			MAP[region_id] = region; // Add or update.

			// Not all regions returned have a position, after all some could be in a crashed state.
			if (region.locationX != null) {
				COORD_MAP[CoordToIndex((int)region.locationX, (int)region.locationY)] = region; // Add or update.
			}
		}

		public void UpdateRegionTerrainData(string region_id) {
			var region = GetRegionByUUID(region_id);

			if (region == null) {
				// This region is missing...
				CreateRegion(region_id);
				return;
			}

			RegionTerrainData terrain_data;

			using (var conn = DBHelpers.GetConnection(region.rdbConnectionString)) {
				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_sw, elevation_2_sw, elevation_1_se, elevation_2_se, water_height, Heightfield
						FROM regionsettings NATURAL JOIN terrain
						WHERE RegionUUID = @region_id
					";
					cmd.Parameters.AddWithValue("region_id", region_id);
					cmd.Prepare();
					var reader = DBHelpers.ExecuteReader(cmd);

					try {
						reader.Read();

						terrain_data.terrainTexture1 = GetDBValue(reader, "terrain_texture_1");
						terrain_data.terrainTexture2 = GetDBValue(reader, "terrain_texture_2");
						terrain_data.terrainTexture3 = GetDBValue(reader, "terrain_texture_3");
						terrain_data.terrainTexture4 = GetDBValue(reader, "terrain_texture_4");

						terrain_data.elevation1NW = GetDBValue<double>(reader, "elevation_1_nw");
						terrain_data.elevation2NW = GetDBValue<double>(reader, "elevation_2_nw");
						terrain_data.elevation1NE = GetDBValue<double>(reader, "elevation_1_ne");
						terrain_data.elevation2NE = GetDBValue<double>(reader, "elevation_2_ne");
						terrain_data.elevation1SW = GetDBValue<double>(reader, "elevation_1_sw");
						terrain_data.elevation2SW = GetDBValue<double>(reader, "elevation_2_sw");
						terrain_data.elevation1SE = GetDBValue<double>(reader, "elevation_1_se");
						terrain_data.elevation2SE = GetDBValue<double>(reader, "elevation_2_se");

						terrain_data.waterHeight = GetDBValue<double>(reader, "water_height");

						terrain_data.heightmap = new double[256, 256];
						terrain_data.heightmap.Initialize();

						var br = new BinaryReader(new MemoryStream((byte[])reader["Heightfield"]));
						for (int x = 0; x < terrain_data.heightmap.GetLength(0); x++) {
							for (int y = 0; y < terrain_data.heightmap.GetLength(1); y++) {
								terrain_data.heightmap[x, y] = br.ReadDouble();
							}
						}
						LOG.Info($"[REGION DB]: Loaded data for region {region_id}");
					}
					finally {
						reader.Close();
					}
				}
			}

			region = new Region(region, terrain_data);

			MAP[region_id] = region; // Add or update.

			// Not all regions returned have a position, after all some could be in a crashed state.
			if (region.locationX != null) {
				COORD_MAP[CoordToIndex((int)region.locationX, (int)region.locationY)] = region; // Add or update.
			}
		}

		public void UpdateRegionPrimData(string region_id) {
			var region = new Region(GetRegionByUUID(region_id), wipe_prims:true);

			if (region == null) {
				// This region is missing...
				CreateRegion(region_id);
				return;
			}

			using (var conn = DBHelpers.GetConnection(region.rdbConnectionString)) {
				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT
							ObjectFlags,
							State,
							PositionX, PositionY, PositionZ,
							GroupPositionX, GroupPositionY, GroupPositionZ,
							ScaleX, ScaleY, ScaleZ,
							RotationX, RotationY, RotationZ, RotationW,
							RootRotationX, RootRotationY, RootRotationZ, RootRotationW,
							Texture
						FROM
							prims pr
							NATURAL JOIN primshapes
							LEFT JOIN (
								SELECT
									RotationX AS RootRotationX,
									RotationY AS RootRotationY,
									RotationZ AS RootRotationZ,
									RotationW AS RootRotationW,
									SceneGroupID
								FROM
									prims pr
								WHERE
									LinkNumber = 1
							) AS rootprim ON rootprim.SceneGroupID = pr.SceneGroupID
						WHERE
							GroupPositionZ < 766 /* = max terrain height + render height */
							AND LENGTH(Texture) > 0
							AND ObjectFlags & (0 | 0x40000 | 0x20000) = 0
							AND ScaleX > 1.0
							AND ScaleY > 1.0
							AND ScaleZ > 1.0
							AND PCode NOT IN (255, 111, 95)
							AND RegionUUID = @region_id
						ORDER BY
							GroupPositionZ, PositionZ
					";
					cmd.Parameters.AddWithValue("region_id", region_id);
					cmd.Prepare();
					var reader = DBHelpers.ExecuteReader(cmd);

					try {
						while (reader.Read()) {
							var prim_data = new RegionPrimData() {
								ObjectFlags = GetDBValue<int>(reader, "ObjectFlags"),
								State = GetDBValue<int>(reader, "State"),
								PositionX = GetDBValue<double>(reader, "PositionX"),
								PositionY = GetDBValue<double>(reader, "PositionY"),
								PositionZ = GetDBValue<double>(reader, "PositionZ"),
								GroupPositionX = GetDBValue<double>(reader, "GroupPositionX"),
								GroupPositionY = GetDBValue<double>(reader, "GroupPositionY"),
								GroupPositionZ = GetDBValue<double>(reader, "GroupPositionZ"),
								ScaleX = GetDBValue<double>(reader, "ScaleX"),
								ScaleY = GetDBValue<double>(reader, "ScaleY"),
								ScaleZ = GetDBValue<double>(reader, "ScaleZ"),
								RotationX = GetDBValue<double>(reader, "RotationX"),
								RotationY = GetDBValue<double>(reader, "RotationY"),
								RotationZ = GetDBValue<double>(reader, "RotationZ"),
								RotationW = GetDBValue<double>(reader, "RotationW"),
								RootRotationX = GetDBValueOrNull<double>(reader, "RootRotationX"),
								RootRotationY = GetDBValueOrNull<double>(reader, "RootRotationY"),
								RootRotationZ = GetDBValueOrNull<double>(reader, "RootRotationZ"),
								RootRotationW = GetDBValueOrNull<double>(reader, "RootRotationW"),
								Texture = (byte[])reader["Texture"],
							};

							region.AddPrim(new Prim(ref prim_data));
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			MAP[region_id] = region; // Add or update.

			// Not all regions returned have a position, after all some could be in a crashed state.
			if (region.locationX != null) {
				COORD_MAP[CoordToIndex((int)region.locationX, (int)region.locationY)] = region; // Add or update.
			}
		}

		public int GetRegionCount() {
			return MAP.Count;
		}

		public IEnumerable<string> GetRegionUUIDsAsStrings() {
			return MAP.Keys;
		}

		public Region GetRegionByUUID(string uuid) {
			Region region;
			if (MAP.TryGetValue(uuid, out region)) {
				return region;
			}
			return null;
		}

		public Region GetRegionByLocation(int locationX, int locationY) {
			var index = CoordToIndex(locationX, locationY);
			Region region;
			if (COORD_MAP.TryGetValue(index, out region)) {
				return region;
			}
			return null;
		}

		public void UpdateRegionLocation(string region_id, int locationX, int locationY) {
			var region = GetRegionByUUID(region_id);
			var coord_index = CoordToIndex(locationX, locationY);

			if (region.locationX != null) {
				// Clean up the reverse lookup.
				COORD_MAP.Remove(coord_index);
			}

			region.locationX = locationX;
			region.locationY = locationY;

			COORD_MAP.Add(coord_index, region);
		}

		#endregion

		private static long CoordToIndex(int x, int y) {
			return (long)x << 32 + y;
		}

		private static T? GetDBValueOrNull<T>(IDataRecord reader, string name) where T: struct {
			Contract.Ensures(Contract.Result<T?>() != null);
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

