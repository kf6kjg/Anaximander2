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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Nini.Config;
using OpenMetaverse;

namespace DataReader {
	public class RDBMap {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly ConcurrentDictionary<Guid, Region> MAP = new ConcurrentDictionary<Guid, Region>();
		private readonly ConcurrentDictionary<Vector2, Region> COORD_MAP = new ConcurrentDictionary<Vector2, Region>();
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

			PARALLELISM_OPTIONS = new ParallelOptions { MaxDegreeOfParallelism = config.Configs["Startup"].GetInt("MaxParallelism", -1) }; // -1 means full parallel.  1 means non-parallel.

			UpdateMap();
		}

		#endregion

		#region Public Methods

		public void DeleteOldMapEntries() {
			var active_regions = new List<Guid>();

			LOG.Debug("Removing explicity removed regions, if any.");
			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				if (conn == null) {
					LOG.Warn($"Could not get connection to main DB, cannot remove old regions from map.");
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
					IDataReader reader = null;
					try {
						reader = DBHelpers.ExecuteReader(cmd);
					}
					catch (Exception e) {
						LOG.Warn($"Region list query DB reader threw an error when attempting to get regions list.", e);
					}

					if (reader == null) {
						LOG.Warn($"Region list query DB reader returned nothing from main DB, cannot remove old regions from map.");
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
				if (MAP.TryGetValue(id, out var reg) && reg.HasKnownCoordinates()) {
					COORD_MAP.TryRemove((Vector2)reg.Location, out reg);
				}
				MAP.TryRemove(id, out reg);
			});
		}

		public void UpdateMap() {
			// Stores RDB connection strings as keys to dictionaries of region UUIDs mapped to the region data.
			var region_list = new ConcurrentDictionary<Guid, Region>();

			LOG.Debug("Loading region-to-host map from DB.");
			using (var conn = DBHelpers.GetConnection(CONNECTION_STRING)) {
				if (conn == null) {
					LOG.Warn($"Could not get connection to main DB, cannot update the map.");
					return;
				}
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
					cmd.CommandTimeout = 600;
					IDataReader reader = null;
					try {
						reader = DBHelpers.ExecuteReader(cmd);
					}
					catch (Exception e) {
						LOG.Warn($"Region list query DB reader threw an error when attempting to update the map.", e);
					}

					if (reader == null) {
						LOG.Warn($"Region list query DB reader returned nothing from main DB, cannot update the map.");
						return;
					}

					try {
						while (reader.Read()) {
							var rdbHostName = Convert.ToString(reader["host_name"]);
							var connString = GetRDBConnectionString(rdbHostName);
							var region_id = Guid.Parse(Convert.ToString(reader["regionID"]));

							// Check to see if the map already has this entry and if the new entry is shut down.
							if (region_list.TryGetValue(region_id, out var region) && Convert.IsDBNull(reader["regionName"])) {
								LOG.Debug($"Found offline region {region_id}.");

								// Region is offline, update the RDB connection in case that's changed.
								region._rdbConnectionString = connString;
							}
							else { // The DB has the freshest information.  Does not imply the region is online - it could have crashed.
								var locationX = GetDBValueOrNull<int>(reader, "locX");
								var locationY = GetDBValueOrNull<int>(reader, "locY");

								region = new Region(connString) {
									Id = region_id,
									Location = locationX == null || locationY == null ? (Vector2?)null : new Vector2((float)locationX, (float)locationY),
									Name = reader.IsDBNull(reader.GetOrdinal("regionName")) ? null : Convert.ToString(reader["regionName"]),
									ServerIP = reader.IsDBNull(reader.GetOrdinal("serverIP")) ? null : Convert.ToString(reader["serverIP"]),
									ServerPort = GetDBValueOrNull<int>(reader, "serverPort"),
									Size = new Vector2(256f, 256f), // DB always has 0 as far as I'm aware. Looks like regions.sizeX and .sizeY are never even read by Halcyon code as of 9/3/2017.
								};

								LOG.Debug($"Found online region {region_id} named '{region.Name}' at {locationX}, {locationY}.");
							}

							if (!region_list.TryAdd(region_id, region)) {
								LOG.Warn($"Attempted to add a duplicate region entry for RDB {rdbHostName}: Region is '{region.Name}' with UUID '{region_id}'.  Check to see if you have duplicate entries in your 'regions' table, or if you have multiple entries in the 'RegionRdbMapping' for the same region UUID.");
							}
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			LOG.Debug("Preparing updated region map.");
			Parallel.ForEach(region_list.Values.ToList(), PARALLELISM_OPTIONS, region => {
				var oldPriority = Thread.CurrentThread.Priority;
				Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

				if (!MAP.TryAdd(region.Id, region)) {
					MAP.TryGetValue(region.Id, out var orig);
					LOG.Warn($"Region '{region.Id}' is a duplicate: name is '{region.Name}' and is duplicating a region with name '{orig?.Name}'.");
				}

				// Not all regions returned have a position, after all some could be in an offline state and never been seen before.
				if (region.HasKnownCoordinates()) {
					var coord = (Vector2)region.Location;
					if (!COORD_MAP.TryAdd(coord, region)) {
						COORD_MAP.TryGetValue(coord, out var orig);
						// Might be a bug here when this method is executed multiple times in a single execution of Anax, and a region was removed and another region was MOVED into the old region's location.
						// That would be resolved I think by executing DeleteOldMapEntries() before this method.
						LOG.Warn($"Region {region.Id} named '{region.Name}' at <{region.Location?.X},{region.Location?.Y}> at same location as {orig?.Id} named '{orig?.Name}' at <{orig?.Location?.X},{orig?.Location?.Y}>. Both of these regions are listed as online in the 'regions' table.");
					}
				}

				Thread.CurrentThread.Priority = oldPriority;
			});

			LOG.Debug("Connecting adjacent regions.");
			foreach (var region in region_list.Values) {
				if (region.HasKnownCoordinates()) {
					var adjacentRegions = new List<Region>();

					if (COORD_MAP.TryGetValue((Vector2)region.Location + new Vector2( 0,  1), out var north)) {
						adjacentRegions.Add(north);
					}

					if (COORD_MAP.TryGetValue((Vector2)region.Location + new Vector2(-1,  1), out var northeast)) {
						adjacentRegions.Add(northeast);
					}

					if (COORD_MAP.TryGetValue((Vector2)region.Location + new Vector2(-1,  0), out var east)) {
						adjacentRegions.Add(east);
					}

					if (COORD_MAP.TryGetValue((Vector2)region.Location + new Vector2(-1, -1), out var southeast)) {
						adjacentRegions.Add(southeast);
					}

					if (COORD_MAP.TryGetValue((Vector2)region.Location + new Vector2( 0, -1), out var south)) {
						adjacentRegions.Add(south);
					}

					if (COORD_MAP.TryGetValue((Vector2)region.Location + new Vector2( 1, -1), out var southwest)) {
						adjacentRegions.Add(southwest);
					}

					if (COORD_MAP.TryGetValue((Vector2)region.Location + new Vector2( 1,  0), out var west)) {
						adjacentRegions.Add(west);
					}

					if (COORD_MAP.TryGetValue((Vector2)region.Location + new Vector2( 1,  1), out var northwest)) {
						adjacentRegions.Add(northwest);
					}

					region._adjacentRegions = adjacentRegions;
				}
			}
		}

		public int GetRegionCount() {
			return MAP.Count;
		}

		public IEnumerable<Guid> GetRegionUUIDs() {
			return MAP.Keys;
		}

		public Region GetRegionByUUID(Guid uuid) {
			if (MAP.TryGetValue(uuid, out var region)) {
				return region;
			}
			return null;
		}

		public Region GetRegionByLocation(int locationX, int locationY) {
			var index = new Vector2(locationX, locationY);
			if (COORD_MAP.TryGetValue(index, out var region)) {
				return region;
			}
			return null;
		}

		public void UpdateRegionLocation(Guid region_id, int locationX, int locationY) {
			var region = GetRegionByUUID(region_id);
			var coord = new Vector2(locationX, locationY);

			// Clean up the reverse lookup.
			COORD_MAP.TryRemove(coord, out var temp);

			region.Location = coord;

			COORD_MAP.TryAdd(coord, region);
		}

		#endregion

		private string GetRDBConnectionString(string rdbHostName) {
			// The RDB connection string could have different user, table, or password than the main.
			if (string.IsNullOrWhiteSpace(RDB_CONNECTION_STRING_PARTIAL)) {
				// Not on an RDB, use the main as this implies that the region is either an invalid entry or is connected to the main DB.
				return CONNECTION_STRING;
			}

			if (string.IsNullOrWhiteSpace(rdbHostName)) { // No host name found in the DB, so get the host name from the main connection string.
				char[] delimiters = {'=', ';'};
				var connParts = CONNECTION_STRING.Split(delimiters);
				var dataSourceIndex = Array.FindIndex(connParts, item => item.Equals("Data Source", StringComparison.InvariantCultureIgnoreCase));
				if (dataSourceIndex < 0 || dataSourceIndex + 1 >= connParts.Length) {
					return CONNECTION_STRING; // Can't find the host name in the conenction string!
				}

				rdbHostName = connParts[dataSourceIndex + 1];
			}

			return string.Format(RDB_CONNECTION_STRING_PARTIAL, rdbHostName.ToLowerInvariant());
		}

		internal static T? GetDBValueOrNull<T>(IDataRecord reader, string name) where T : struct {
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

		internal static T GetDBValue<T>(IDataRecord reader, string name) where T : struct {
			var result = new T();
			try {
				var ordinal = reader.GetOrdinal(name);
				if (!reader.IsDBNull(ordinal)) {
					result = (T)Convert.ChangeType(reader.GetValue(ordinal), typeof(T));
				}
			}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			catch {
				// I reallly don't care.
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			return result;
		}

		internal static string GetDBValue(IDataRecord reader, string name) {
			var ordinal = reader.GetOrdinal(name);
			return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
		}
	}
}

