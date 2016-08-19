// MyClass.cs
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
using log4net;
using MySql.Data.MySqlClient;
using Nini.Config;

namespace DataReader {
	public class RDBMap {
		//private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly Dictionary<string, Region> _map = new Dictionary<string, Region>();

		private readonly string connection_string;
		private readonly string rdb_connection_string_partial;

		#region Constructors

		public RDBMap(IConfigSource config) {
			var dataConfig = config.Configs["Database"];

			connection_string = dataConfig.GetString("MasterDatabaseConnectionString", connection_string).Trim();

			if (String.IsNullOrWhiteSpace(connection_string)) {
				// Analysis disable once NotResolvedInText
				throw new ArgumentNullException("MasterDatabaseConnectionString", "Missing or empty key in section [Database] of the ini file.");
			}

			rdb_connection_string_partial = dataConfig.GetString("RDBConnectionStringPartial", rdb_connection_string_partial).Trim();

			if (String.IsNullOrWhiteSpace(rdb_connection_string_partial)) {
				// Analysis disable once NotResolvedInText
				throw new ArgumentNullException("RDBConnectionStringPartial", "Missing or empty key in section [Database] of the ini file.");
			}

			if (!rdb_connection_string_partial.Contains("Data Source")) {
				rdb_connection_string_partial = "Data Source={0};" + rdb_connection_string_partial;
			}

			UpdateMap();
		}

		#endregion

		#region Public Methods

		public void UpdateMap() {
			using (MySqlConnection conn = DBHelpers.GetConnection(connection_string)) {
				using (MySqlCommand cmd = conn.CreateCommand()) {
					/* Gets the full list of what regions are on what host.
					A null host_name indicates that that region's data is on this host, otherwise contains the host for the region's data.
					A null regionName indicates that the region is shut down, otherwise that the region is up or crashed.
					*/
					cmd.CommandText = @"SELECT
						regionID AS region_id, host_name, regionName, locX, locY, sizeX, sizeY, serverIP, serverPort
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

							string region_id = Convert.ToString(reader["region_id"]);

							Region old_entry = null;
							Region new_entry;

							// Check to see if the map already has this entry and if the new entry is shut down.
							if (_map.ContainsKey(region_id) && Convert.IsDBNull(reader["regionName"])) {
								old_entry = _map[region_id];

								new_entry = new Region(
									region_id,
									String.Format(rdb_connection_string_partial, rdbhost),
									old_entry.RegionName,
									old_entry.LocationX,
									old_entry.LocationY,
									old_entry.SizeX,
									old_entry.SizeY,
									old_entry.ServerIP,
									old_entry.ServerPort
								);
							}
							else { // The DB has the freshest information.  Does not imply the region is online - it could have crashed.
								new_entry = new Region(
									region_id,
									String.Format(rdb_connection_string_partial, rdbhost),
									reader.IsDBNull(reader.GetOrdinal("regionName")) ? null : Convert.ToString(reader["regionName"]),
									GetDBValueOrNull<int>(reader, "locX"),
									GetDBValueOrNull<int>(reader, "locY"),
									GetDBValueOrNull<int>(reader, "sizeX"),
									GetDBValueOrNull<int>(reader, "sizeY"),
									reader.IsDBNull(reader.GetOrdinal("serverIP")) ? null : Convert.ToString(reader["serverIP"]),
									GetDBValueOrNull<int>(reader, "serverPort")
								);
							}

							_map.Add(region_id, new_entry);
						}
					}
					finally {
						reader.Close();
					}
				}
			}
		}

		public int GetRegionCount() {
			return _map.Count;
		}

		public IEnumerable<string> GetRegionUUIDsAsStrings() {
			return _map.Keys;
		}

		public Region GetRegionByUUID(string uuid) {
			return _map[uuid];
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
			catch{
			}

			return result;
		}
	}
}

