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
					COORD_MAP.TryRemove((Vector2)reg.Location, out reg);
				}
				MAP.TryRemove(id, out reg);
			});
		}

		public void UpdateMap() {
			// Stores RDB connection strings as keys to dictionaries of region UUIDs mapped to the region data.
			var region_list = new ConcurrentDictionary<Guid, Region>();

			LOG.Debug("[RDB_MAP] Loading region-to-host map from DB.");
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
							var region_id = Guid.Parse(Convert.ToString(reader["regionID"]));

							// Check to see if the map already has this entry and if the new entry is shut down.
							Region region;
							if (region_list.TryGetValue(region_id, out region) && Convert.IsDBNull(reader["regionName"])) {
								// Region is offline, update the RDB connection in case that's changed.
								region._rdbConnectionString = rdbhost;
							}
							else { // The DB has the freshest information.  Does not imply the region is online - it could have crashed.
								var locationX = GetDBValueOrNull<int>(reader, "locX");
								var locationY = GetDBValueOrNull<int>(reader, "locY");

								region = new Region(rdbhost) {
									Id = region_id,
									Location = locationX == null || locationY == null ? (Vector2?)null : new Vector2((float)locationX, (float)locationY),
									Name = reader.IsDBNull(reader.GetOrdinal("regionName")) ? null : Convert.ToString(reader["regionName"]),
									ServerIP = reader.IsDBNull(reader.GetOrdinal("serverIP")) ? null : Convert.ToString(reader["serverIP"]),
									ServerPort = GetDBValueOrNull<int>(reader, "serverPort"),
									Size = new Vector2(256f, 256f), // DB always has 0 as far as I'm aware. Looks like regions.sizeX and .sizeY are never even read by Halcyon code as of 9/3/2017.
								};
							}

							if (!region_list.TryAdd(region_id, region)) {
								LOG.Warn($"[RDB_MAP] Attempted to add a duplicate region entry for RDB {rdbHostName}: Region is '{region.Name}' with UUID '{region_id}'.  Check to see if you have duplicate entries in your 'regions' table, or if you have multiple entries in the 'RegionRdbMapping' for the same region UUID.");
							}
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			LOG.Debug("[RDB_MAP] Preparing updated region map.");
			Parallel.ForEach(region_list.Values.ToList(), PARALLELISM_OPTIONS, region => {
				var oldPriority = Thread.CurrentThread.Priority;
				Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

				if (!MAP.TryAdd(region.Id, region)) {
					Region orig;
					MAP.TryGetValue(region.Id, out orig);
					LOG.Warn($"[RDB_MAP] Region '{region.Id}' is a duplicate: name is '{region.Name}' and is duplicating a region with name '{orig?.Name}'.");
				}

				// Not all regions returned have a position, after all some could be in an offline state and never been seen before.
				if (region.HasKnownCoordinates()) {
					var coord = (Vector2)region.Location;
					if (!COORD_MAP.TryAdd(coord, region)) {
						Region orig;
						COORD_MAP.TryGetValue(coord, out orig);
						// Might be a bug here when this method is executed multiple times in a single execution of Anax, and a region was removed and another region was MOVED into the old region's location.
						// That would be resolved I think by executing DeleteOldMapEntries() before this method.
						LOG.Warn($"[RDB_MAP] Region {region.Id} named '{region.Name}' at <{region.Location?.X},{region.Location?.Y}> at same location as {orig?.Id} named '{orig?.Name}' at <{orig?.Location?.X},{orig?.Location?.Y}>. Both of these regions are listed as online in the 'regions' table.");
					}
				}

				Thread.CurrentThread.Priority = oldPriority;
			});
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
			var index = new Vector2(locationX, locationY);
			Region region;
			if (COORD_MAP.TryGetValue(index, out region)) {
				return region;
			}
			return null;
		}

		public void UpdateRegionLocation(Guid region_id, int locationX, int locationY) {
			var region = GetRegionByUUID(region_id);
			var coord = new Vector2(locationX, locationY);

			// Clean up the reverse lookup.
			Region temp;
			COORD_MAP.TryRemove(coord, out temp);

			region.Location = coord;

			COORD_MAP.TryAdd(coord, region);
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

		private string GetHostNameFromConnectionString(string connstr) {
			// Format: Data Source=127.0.0.1;Port=3307;Database=TABLENAME;User ID=USERNAME;password=PASSWORD;Pooling=True;Min Pool Size=0;
			return connstr.ToLowerInvariant().Split(';').FirstOrDefault(stanza => stanza.StartsWith("data source", StringComparison.InvariantCulture))?.Substring(12);
		}

		private string GetDatabaseNameFromConnectionString(string connstr) {
			// Format: Data Source=127.0.0.1;Port=3307;Database=TABLENAME;User ID=USERNAME;password=PASSWORD;Pooling=True;Min Pool Size=0;
			return connstr.ToLowerInvariant().Split(';').FirstOrDefault(stanza => stanza.StartsWith("database", StringComparison.InvariantCulture))?.Substring(9);
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

