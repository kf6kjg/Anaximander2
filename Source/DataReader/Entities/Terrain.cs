// RegionTerrainData.cs
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
using System.Data;
using System.IO;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace DataReader {
	public class Terrain {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public UUID TerrainTexture1 { get; private set; }
		public UUID TerrainTexture2 { get; private set; }
		public UUID TerrainTexture3 { get; private set; }
		public UUID TerrainTexture4 { get; private set; }

		public double ElevationNWLow { get; private set; }
		public double ElevationNWHigh { get; private set; }
		public double ElevationNELow { get; private set; }
		public double ElevationNEHigh { get; private set; }
		public double ElevationSWLow { get; private set; }
		public double ElevationSWHigh { get; private set; }
		public double ElevationSELow { get; private set; }
		public double ElevationSEHigh { get; private set; }

		public double WaterHeight { get; private set; }

		private readonly double[,] _heightmap = new double[256, 256];
		private readonly Guid _regionId;
		private readonly string _rdbConnectionString;

		internal Terrain(string rdbConnectionString, Guid regionId) {
			_rdbConnectionString = rdbConnectionString;
			_regionId = regionId;
		}

		public bool Update() {
			using (var conn = DBHelpers.GetConnection(_rdbConnectionString)) {
				if (conn == null) {
					LOG.Warn($"[TERRAIN] Could not get connection to DB for region '{_regionId}'.");
					return false;
				}
				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT terrain_texture_1, terrain_texture_2, terrain_texture_3, terrain_texture_4, elevation_1_nw, elevation_2_nw, elevation_1_ne, elevation_2_ne, elevation_1_sw, elevation_2_sw, elevation_1_se, elevation_2_se, water_height, Heightfield
						FROM regionsettings NATURAL JOIN terrain
						WHERE RegionUUID = @region_id
					";
					cmd.Parameters.AddWithValue("region_id", _regionId.ToString());
					cmd.Prepare();
					IDataReader reader = null;
					try {
						reader = DBHelpers.ExecuteReader(cmd);
					}
					catch (Exception e) {
						LOG.Warn($"[PRIM] Prims query DB reader threw an error when attempting to get prims for region '{_regionId}'.", e);
					}

					if (reader == null) {
						LOG.Warn($"[TERRAIN] Terrain DB reader query returned nothing for region '{_regionId}'.");
						return false;
					}

					try {
						reader.Read();

						TerrainTexture1 = UUID.Parse(RDBMap.GetDBValue(reader, "terrain_texture_1"));
						TerrainTexture2 = UUID.Parse(RDBMap.GetDBValue(reader, "terrain_texture_2"));
						TerrainTexture3 = UUID.Parse(RDBMap.GetDBValue(reader, "terrain_texture_3"));
						TerrainTexture4 = UUID.Parse(RDBMap.GetDBValue(reader, "terrain_texture_4"));

						ElevationNWLow = RDBMap.GetDBValue<double>(reader, "elevation_1_nw");
						ElevationNWHigh = RDBMap.GetDBValue<double>(reader, "elevation_2_nw");
						ElevationNELow = RDBMap.GetDBValue<double>(reader, "elevation_1_ne");
						ElevationNEHigh = RDBMap.GetDBValue<double>(reader, "elevation_2_ne");
						ElevationSWLow = RDBMap.GetDBValue<double>(reader, "elevation_1_sw");
						ElevationSWHigh = RDBMap.GetDBValue<double>(reader, "elevation_2_sw");
						ElevationSELow = RDBMap.GetDBValue<double>(reader, "elevation_1_se");
						ElevationSEHigh = RDBMap.GetDBValue<double>(reader, "elevation_2_se");

						WaterHeight = RDBMap.GetDBValue<double>(reader, "water_height");

						_heightmap.Initialize();

						var br = new BinaryReader(new MemoryStream((byte[])reader["Heightfield"]));
						for (int x = 0; x < _heightmap.GetLength(0); x++) {
							for (int y = 0; y < _heightmap.GetLength(1); y++) {
								_heightmap[x, y] = br.ReadDouble();
							}
						}
					}
					finally {
						reader.Close();
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Gets the height at the specified location, blending the value depending on the proximity to the pixel center.
		/// AKA: bilinear filtering.
		/// </summary>
		/// <returns>The height in meters.</returns>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		public double GetBlendedHeight(double x, double y) {
			int x_0 = (int)x, y_0 = (int)y;
			int x_1 = x_0 + 1, y_1 = y_0 + 1;

			var x_ratio = x - x_0; // The fractional part gives the 0-1 ratio needed.
			var y_ratio = y - y_0;

			// Unit square interpretation of bilinear filtering.
			if (x_0 < _heightmap.GetLength(0) - 1 && y_0 < _heightmap.GetLength(1) - 1) {
				return
					_heightmap[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					_heightmap[x_1, y_0] * x_ratio * (1 - y_ratio) +
					_heightmap[x_0, y_1] * (1 - x_ratio) * y_ratio +
					_heightmap[x_1, y_1] * x_ratio * y_ratio;
			}

			if (x_0 < _heightmap.GetLength(0) - 1) {
				return
					_heightmap[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					_heightmap[x_1, y_0] * x_ratio * (1 - y_ratio);
			}

			if (y_0 < _heightmap.GetLength(1) - 1) {
				return
					_heightmap[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					_heightmap[x_0, y_1] * (1 - x_ratio) * y_ratio;
			}

			return
				_heightmap[x_0, y_0];
		}
	}
}

