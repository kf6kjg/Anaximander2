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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace DataReader {
	public class Region {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		#region Public Properties and Accessors

		public Guid Id { get; internal set; }

		public Vector2? Location { get; internal set; }

		public string Name { get; internal set; }

		public string ServerIP { get; internal set; }

		public int? ServerPort { get; internal set; }

		public Vector2 Size { get; internal set; }

		#endregion

		#region Private/Internal Properties

		internal IEnumerable<Region> _adjacentRegions;

		internal string _rdbConnectionString = null;

		#endregion

		#region Public Methods

		/// <summary>
		/// Gets the prims for this region and all adjacent from the DB.
		/// </summary>
		/// <returns>The prims.</returns>
		public IEnumerable<Prim> GetPrims() {
			if (!HasKnownCoordinates()) {
				return null;
			}

			var prims = Prim.LoadPrims(_rdbConnectionString, Id);

			if (_adjacentRegions != null) {
				prims = _adjacentRegions.Select(adjacentRegion => {
					if (adjacentRegion.HasKnownCoordinates()) {
						var adjacentRegionPrims = Prim.LoadPrims(adjacentRegion._rdbConnectionString, adjacentRegion.Id);

						foreach (var prim in adjacentRegionPrims) {
							var regionOffset = (Vector2)adjacentRegion.Location - (Vector2)Location;

							prim.Offset(regionOffset * Size); // Assumes constant size regions.
						}

						return adjacentRegionPrims;
					}

					return null;
				})
				.Where(region => region != null)
				.Aggregate(prims, (primAccumulation, regionPrims) => primAccumulation.Concat(regionPrims));
			}

			return prims;
		}

		/// <summary>
		/// Gets the current terrain data for the current region from the DB.  Make sure you've run Update first!
		/// </summary>
		/// <returns>The terrain.</returns>
		public Terrain GetTerrain() {
			var terrain = new Terrain(_rdbConnectionString, Id);

			if (terrain.Update()) {
				return terrain;
			}

			return null;
		}

		/// <summary>
		/// Whether or not the region is listed as online or not, were we able to dig up some coordinates for this region?
		/// </summary>
		/// <returns><c>true</c>, if region has good coordinates, <c>false</c> otherwise.</returns>
		public bool HasKnownCoordinates() {
			return Location != null;
		}

		/// <summary>
		/// Queries the region server simstatus to detect if the region is online and accessable at this moment.
		/// </summary>
		/// <returns><c>true</c> if this instance is region currently up; otherwise, <c>false</c>.</returns>
		public bool IsCurrentlyAccessable() {
			if (ServerIP != null) {
				var wrGETURL = WebRequest.Create($"http://{ServerIP}:{ServerPort}/simstatus/");
				wrGETURL.Timeout = 10000; // Limit to something reasonable.  If the region can't respond in that time, it's not really accessable ATM.
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

		/// <summary>
		/// Is the region marked in the database as online.  Does not mean the region is actually online as it could have crashed or locked up.
		/// </summary>
		/// <returns><c>true</c>, if region is listed as online, <c>false</c> otherwise.</returns>
		public bool IsListedAsOnline() {
			return Name != null;
		}

		#endregion

		#region Constructors

		internal Region(string rdbConnectionString) {
			_rdbConnectionString = rdbConnectionString;
		}

		#endregion
	}
}

