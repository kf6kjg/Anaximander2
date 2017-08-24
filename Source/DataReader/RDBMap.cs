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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Nini.Config;

namespace DataReader {
	public class RDBMap {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly ConcurrentDictionary<Guid, Region> MAP = new ConcurrentDictionary<Guid, Region>();
		private readonly ConcurrentDictionary<long, Region> COORD_MAP = new ConcurrentDictionary<long, Region>();
		private readonly ConcurrentBag<Guid> DEAD_REGION_IDS = new ConcurrentBag<Guid>();

		private readonly string CONNECTION_STRING;
		private readonly string RDB_CONNECTION_STRING_PARTIAL;

		private readonly ParallelOptions PARALLELISM_OPTIONS;

		#region Constructors

		public RDBMap(IConfigSource config) {
			var data_config = config.Configs["Database"];

			CONNECTION_STRING = data_config.GetString("MasterDatabaseConnectionString", CONNECTION_STRING).Trim();

			if (string.IsNullOrWhiteSpace(CONNECTION_STRING)) {
#pragma warning disable RECS0143 // Cannot resolve symbol in text argument
				throw new ArgumentNullException("MasterDatabaseConnectionString", "Missing or empty key in section [Database] of the ini file.");
#pragma warning restore RECS0143 // Cannot resolve symbol in text argument
			}

			RDB_CONNECTION_STRING_PARTIAL = data_config.GetString("RDBConnectionStringPartial", RDB_CONNECTION_STRING_PARTIAL).Trim();

			if (string.IsNullOrWhiteSpace(RDB_CONNECTION_STRING_PARTIAL)) {
#pragma warning disable RECS0143 // Cannot resolve symbol in text argument
				throw new ArgumentNullException("RDBConnectionStringPartial", "Missing or empty key in section [Database] of the ini file.");
#pragma warning restore RECS0143 // Cannot resolve symbol in text argument
			}

			if (!RDB_CONNECTION_STRING_PARTIAL.Contains("Data Source")) {
				RDB_CONNECTION_STRING_PARTIAL = "Data Source={0};" + RDB_CONNECTION_STRING_PARTIAL;
			}

			if (MAP.Count > 0) { // No sense in trying to remove old entries when there are no entries!
				DeleteOldMapEntries();
			}

			PARALLELISM_OPTIONS = new ParallelOptions { MaxDegreeOfParallelism = config.Configs["Startup"].GetInt("MaxParallism", -1) }; // -1 means full parallel.  1 means non-parallel.

			UpdateMap();
		}

		#endregion

		#region Public Methods

		public void DeleteOldMapEntries() {
			var active_regions = new List<Guid>();

			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				if (conn == null) {
					return;
				}
				using (var cmd = conn.CreateCommand()) {
					/* Gets the full list of known regions.  Any region IDs that are not in this list are to be removed. */
					cmd.CommandText = @"SELECT
						regionID
					FROM
						estate_map
					ORDER BY
						region_id";
					var reader = DBHelpers.ExecuteReader(cmd);
					if (reader == null) {
						return;
					}

					try {
						while (reader.Read()) {
							active_regions.Add(Guid.Parse(Convert.ToString(reader["region_id"])));
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
				if (MAP.TryGetValue(id, out reg) && reg.HasKnownCoordinates()) {
					COORD_MAP.TryRemove(CoordToIndex((int)reg.locationX, (int)reg.locationY), out reg);
				}
				MAP.TryRemove(id, out reg);
			});
		}

		public void UpdateMap() {
			// Stores RDB connection strings as keys to dictionaries of region UUIDs mapped to the region data.
			var regions_by_rdb = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, RegionInfo>>();

			LOG.Debug("[RDB_MAP] Loading region to host map from DB.");
			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				if (conn == null) {
					return;
				}
				using (var cmd = conn.CreateCommand()) {
					RegionInfo new_entry;
					ConcurrentDictionary<Guid, RegionInfo> region_list;

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
					if (reader == null) {
						return;
					}

					try {
						while (reader.Read()) {
							var rdbHostName = Convert.ToString(reader["host_name"]);
							var rdbhost = GetRDBConnectionString(rdbHostName);

							if (!regions_by_rdb.TryGetValue(rdbhost, out region_list)) {
								region_list = new ConcurrentDictionary<Guid, RegionInfo>();
								regions_by_rdb.TryAdd(rdbhost, region_list);
							}

							var region_id = Guid.Parse(Convert.ToString(reader["regionID"]));

							// Check to see if the map already has this entry and if the new entry is shut down.
							if (region_list.TryGetValue(region_id, out new_entry) && Convert.IsDBNull(reader["regionName"])) {
								// Update the RDB connection
								new_entry.RDBConnectionString = rdbhost;
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

							if (!region_list.TryAdd(region_id, new_entry)) {
								LOG.Warn($"[RDB_MAP] Attempted to add a duplicate region entry for RDB {rdbHostName}: Region is '{new_entry.regionName}' with UUID '{region_id}'.  Check to see if you have duplicate entries in your 'regions' table, or if you have multiple entries in the 'RegionRdbMapping' for the same region UUID.");
							}
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			LOG.Debug("[RDB_MAP] Loading terrain data from DB.");
			Parallel.ForEach(regions_by_rdb.Keys.ToList(), PARALLELISM_OPTIONS, (rdb_connection_string) => {
				var oldPriority = Thread.CurrentThread.Priority;
				Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

				RegionTerrainData data;
				Guid region_id;

				using (var conn = DBHelpers.GetConnection(rdb_connection_string)) {
					if (conn == null) {
						return; // This causes everything on this RDB to be skipped.
					}
					using (var cmd = conn.CreateCommand()) {
						cmd.CommandText = @"SELECT RegionUUID, terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_sw, elevation_2_sw, elevation_1_se, elevation_2_se, water_height, Heightfield FROM regionsettings natural join terrain;";
						var reader = DBHelpers.ExecuteReader(cmd);
						if (reader == null) {
							return; // This causes everything on this RDB to be skipped.
						}

						try {
							while (reader.Read()) {
								region_id = Guid.Parse(GetDBValue(reader, "RegionUUID"));

								ConcurrentDictionary<Guid, RegionInfo> region_list;

								if (regions_by_rdb.TryGetValue(rdb_connection_string, out region_list) && !region_list.ContainsKey(region_id)) {
									// Either of the regionsettings and/or terrain tables on one of the rdb hosts has an entry for a region id that does not exist in the estates table.
									// Or the DB has entries for both the domain AND the IP that domain points to.
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
								for (int x = 0; x < data.heightmap.GetLength(0); x++) {
									for (int y = 0; y < data.heightmap.GetLength(1); y++) {
										data.heightmap[x, y] = br.ReadDouble();
									}
								}
								LOG.Info($"[RDB_MAP] Loaded terrain data for region {region_id}");

								var info = regions_by_rdb[rdb_connection_string][region_id];
								var region = new Region(info, data);

								if (!MAP.TryAdd(region_id, region)) {
									Region orig;
									MAP.TryGetValue(region_id, out orig);
									LOG.Warn($"[RDB_MAP] Region '{region_id}' is a duplicate: name is '{region.regionName}' and is duplicating a region with name '{orig?.regionName}'.");
								}

								// Not all regions returned have a position, after all some could be in an offline state and never been seen before.
								if (info.locationX != null) {
									var coord = CoordToIndex((int)info.locationX, (int)info.locationY);
									if (!COORD_MAP.TryAdd(coord, region)) {
										Region orig;
										COORD_MAP.TryGetValue(coord, out orig);
										LOG.Warn($"[RDB_MAP] Region {info.regionId} named '{info.regionName}' at <{info.locationX},{info.locationY}> at same location as {orig?.regionId} named '{orig?.regionName}' at <{orig?.locationX},{orig?.locationY}>. Both of these regions are listed as online in the 'regions' table.");
									}
								}
							}
						}
						finally {
							reader.Close();
						}
					}
				}

				Thread.CurrentThread.Priority = oldPriority;
			});

			LOG.Debug("[RDB_MAP] Loading prim data from DB.");
			Parallel.ForEach(regions_by_rdb.Keys.ToList(), PARALLELISM_OPTIONS, (rdb_connection_string) => {
				Guid region_id;
				Region region;

				using (var conn = DBHelpers.GetConnection(rdb_connection_string)) {
					if (conn == null) {
						return;
					}
					using (var cmd = conn.CreateCommand()) {
						cmd.CommandText = @"SELECT
	RegionUUID,
	UUID,
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
						if (reader == null) {
							return;
						}

						try {
							while (reader.Read()) {
								region_id = Guid.Parse(GetDBValue(reader, "RegionUUID"));

								if (!MAP.TryGetValue(region_id, out region)) {
									// The prims table on one of the rdb hosts has an entry for a region id that does not exist in the estates, regionsettings, and terrain tables.
									// Or the DB has entries for both the domain AND the IP that domain points to.
									continue;
								}

								if (!region.HasKnownCoordinates()) {
									// Hey, the region is KNOWN to not have any clue as to where it is located.  Skippage.

									// ISSUE: since the region location info is pulled from disk sometime much later this will result in offline regions that don't have their prim data.
									// Not an issue ATM, but at some point offline with prims will be wanted.

									continue;
								}

								var data = new RegionPrimData() {
									RegionId = region_id,
									PrimId = new Guid(GetDBValue(reader, "UUID")),
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

								// North-West
								AddPrimToRegionAtOffsetFrom(data, region, -1, -1);
								// North
								AddPrimToRegionAtOffsetFrom(data, region,  0, -1);
								// Nor-East
								AddPrimToRegionAtOffsetFrom(data, region,  1, -1);
								// East
								AddPrimToRegionAtOffsetFrom(data, region,  1,  0);
								// South-East
								AddPrimToRegionAtOffsetFrom(data, region,  1,  1);
								// South
								AddPrimToRegionAtOffsetFrom(data, region,  0,  1);
								// South-West
								AddPrimToRegionAtOffsetFrom(data, region, -1,  1);
								// West
								AddPrimToRegionAtOffsetFrom(data, region, -1,  0);
							}
						}
						finally {
							reader.Close();
						}
					}
				}
			});
		}

		public bool CreateRegion(Guid region_id) {
			var info = new RegionInfo();
			RegionTerrainData terrain_data;
			Region region;

			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				if (conn == null) {
					return false;
				}
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
					if (reader == null) {
						return false;
					}

					try {
						if (!reader.Read()) {
							// If there are no valid results, then the requested region does not exist and there's no point in continuing.
							LOG.Warn($"[RDB_MAP] Region '{region_id}' does not exist in the database.  Aborting creation.");
							return false;
						}

						var rdbHostName = Convert.ToString(reader["host_name"]);
						string rdbhost = GetRDBConnectionString(rdbHostName);

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
				if (conn == null) {
					return false;
				}
				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_sw, elevation_2_sw, elevation_1_se, elevation_2_se, water_height, Heightfield
						FROM regionsettings NATURAL JOIN terrain
						WHERE RegionUUID = @region_id
					";
					cmd.Parameters.AddWithValue("region_id", region_id);
					cmd.Prepare();
					var reader = DBHelpers.ExecuteReader(cmd);
					if (reader == null) {
						return false;
					}

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
						LOG.Info($"[RDB_MAP] Loaded terrain data for region {region_id}");
					}
					finally {
						reader.Close();
					}
				}

				region = new Region(info, terrain_data);

				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT
							UUID,
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
					if (reader == null) {
						return false;
					}

					try {
						while (reader.Read()) {
							var prim_data = new RegionPrimData() {
								RegionId = region_id,
								PrimId = new Guid(GetDBValue(reader, "UUID")),
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

			// Add or replace the region.
			Region temp;
			MAP.TryRemove(region_id, out temp); // Don't care if it fails.
			MAP.TryAdd(region_id, region); // Won't fail because of the remove just above.

			// Not all regions returned have a position, after all some could be in an offline state and not yet been seen.
			if (region.HasKnownCoordinates()) {
				var coord = CoordToIndex((int)region.locationX, (int)region.locationY);

				// Add or replace the region.
				COORD_MAP.TryRemove(coord, out temp); // Don't care if it fails.
				COORD_MAP.TryAdd(coord, region); // Won't fail because of the remove just above.
			}

			return true;
		}

		public void UpdateRegionInfo(Guid region_id) {
			var region = GetRegionByUUID(region_id);

			if (region == null) {
				// This region is missing...
				CreateRegion(region_id);
				return;
			}

			var info = new RegionInfo();

			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				if (conn == null) {
					return;
				}
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
					if (reader == null) {
						return;
					}

					try {
						reader.Read();
						var rdbHostName = Convert.ToString(reader["host_name"]);
						string rdbhost = GetRDBConnectionString(rdbHostName);

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

			// Add or replace the region.
			Region temp;
			MAP.TryRemove(region_id, out temp); // Don't care if it fails.
			MAP.TryAdd(region_id, region); // Won't fail because of the remove just above.

			// Not all regions returned have a position, after all some could be in an offline state and not yet been seen.
			if (region.HasKnownCoordinates()) {
				var coord = CoordToIndex((int)region.locationX, (int)region.locationY);

				// Add or replace the region.
				COORD_MAP.TryRemove(coord, out temp); // Don't care if it fails.
				COORD_MAP.TryAdd(coord, region); // Won't fail because of the remove just above.
			}
		}

		public void UpdateRegionTerrainData(Guid region_id) {
			var region = GetRegionByUUID(region_id);

			if (region == null) {
				// This region is missing...
				CreateRegion(region_id);
				return;
			}

			RegionTerrainData terrain_data;

			using (var conn = DBHelpers.GetConnection(region.rdbConnectionString)) {
				if (conn == null) {
					return;
				}
				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_sw, elevation_2_sw, elevation_1_se, elevation_2_se, water_height, Heightfield
						FROM regionsettings NATURAL JOIN terrain
						WHERE RegionUUID = @region_id
					";
					cmd.Parameters.AddWithValue("region_id", region_id);
					cmd.Prepare();
					var reader = DBHelpers.ExecuteReader(cmd);
					if (reader == null) {
						return;
					}

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
						LOG.Info($"[RDB_MAP] Loaded terrain data for region {region_id}");
					}
					finally {
						reader.Close();
					}
				}
			}

			region = new Region(region, terrain_data);

			// Add or replace the region.
			Region temp;
			MAP.TryRemove(region_id, out temp); // Don't care if it fails.
			MAP.TryAdd(region_id, region); // Won't fail because of the remove just above.

			// Not all regions returned have a position, after all some could be in an offline state and not yet been seen.
			if (region.HasKnownCoordinates()) {
				var coord = CoordToIndex((int)region.locationX, (int)region.locationY);

				// Add or replace the region.
				COORD_MAP.TryRemove(coord, out temp); // Don't care if it fails.
				COORD_MAP.TryAdd(coord, region); // Won't fail because of the remove just above.
			}
		}

		public void UpdateRegionPrimData(Guid region_id) {
			var region = new Region(GetRegionByUUID(region_id), wipe_prims: true);

			if (region == null) {
				// This region is missing...
				CreateRegion(region_id);
				return;
			}

			using (var conn = DBHelpers.GetConnection(region.rdbConnectionString)) {
				if (conn == null) {
					return;
				}
				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT
							UUID,
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
					if (reader == null) {
						return;
					}

					try {
						while (reader.Read()) {
							var prim_data = new RegionPrimData() {
								RegionId = region_id,
								PrimId = new Guid(GetDBValue(reader, "UUID")),
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

			// Add or replace the region.
			Region temp;
			MAP.TryRemove(region_id, out temp); // Don't care if it fails.
			MAP.TryAdd(region_id, region); // Won't fail because of the remove just above.

			// Not all regions returned have a position, after all some could be in an offline state and not yet been seen.
			if (region.HasKnownCoordinates()) {
				var coord = CoordToIndex((int)region.locationX, (int)region.locationY);

				// Add or replace the region.
				COORD_MAP.TryRemove(coord, out temp); // Don't care if it fails.
				COORD_MAP.TryAdd(coord, region); // Won't fail because of the remove just above.
			}
		}

		public int GetRegionCount() {
			return MAP.Count;
		}

		public IEnumerable<Guid> GetRegionUUIDs() {
			return MAP.Keys;
		}

		public Region GetRegionByUUID(Guid uuid) {
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

		public void UpdateRegionLocation(Guid region_id, int locationX, int locationY) {
			var region = GetRegionByUUID(region_id);
			var coord_index = CoordToIndex(locationX, locationY);

			// Clean up the reverse lookup.
			Region temp;
			COORD_MAP.TryRemove(coord_index, out temp);

			region.locationX = locationX;
			region.locationY = locationY;

			COORD_MAP.TryAdd(coord_index, region);
		}

		#endregion

		private string GetRDBConnectionString(string rdbHostName) {
			string rdbhost;

			// The RDB connection string could have different user, table, or password than the main.
			if (string.IsNullOrWhiteSpace(rdbHostName)) {
				// Not on an RDB, use the main as this implies that the region is either an invalid entry or is connected to the main DB.
				rdbhost = CONNECTION_STRING;
			}
			else {
				rdbhost = string.Format(RDB_CONNECTION_STRING_PARTIAL, rdbHostName.ToLowerInvariant());
			}

			return rdbhost;
		}

		private static long CoordToIndex(int x, int y) {
			return ((long)x << 32) + y;
		}

		private static T? GetDBValueOrNull<T>(IDataRecord reader, string name) where T : struct {
			Contract.Ensures(Contract.Result<T?>() != null);
			var result = new T?();
			try {
				var ordinal = reader.GetOrdinal(name);
				if (!reader.IsDBNull(ordinal)) {
					result = (T)Convert.ChangeType(reader.GetValue(ordinal), typeof(T));
				}
			}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			catch {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			return result;
		}

		private static T GetDBValue<T>(IDataRecord reader, string name) where T : struct {
			var result = new T();
			try {
				var ordinal = reader.GetOrdinal(name);
				if (!reader.IsDBNull(ordinal)) {
					result = (T)Convert.ChangeType(reader.GetValue(ordinal), typeof(T));
				}
			}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			catch {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			return result;
		}

		private static string GetDBValue(IDataRecord reader, string name) {
			var ordinal = reader.GetOrdinal(name);
			return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
		}

		private void AddPrimToRegionAtOffsetFrom(RegionPrimData primData, Region origin, int offsetX, int offsetY) {
			// Since RegionPrimData is a struct it's already been shallowly copied getting here.

			Region secondaryRegion;

			if (COORD_MAP.TryGetValue(CoordToIndex((int)origin.locationX + offsetX, (int)origin.locationY + offsetY), out secondaryRegion)) {
				// Ok, so there's a region there. Add the prim to it without too much thought about whether the prim'll actually overlap that space for now.

				// Offset the prim into the space of the other region.
				primData.GroupPositionX = -256.0 * offsetX + primData.GroupPositionX;
				primData.GroupPositionY = -256.0 * offsetY + primData.GroupPositionY;

				secondaryRegion.AddPrim(new Prim(ref primData));
			}
		}
	}
}

