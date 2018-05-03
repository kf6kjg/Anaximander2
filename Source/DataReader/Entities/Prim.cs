// Prim.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2017 Richard Curtice
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
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace DataReader {
	public class Prim {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		internal static IEnumerable<Prim> LoadPrims(string rdbConnectionString, Guid regionId) {
			using (var conn = DBHelpers.GetConnection(rdbConnectionString)) {
				if (conn == null) {
					LOG.Warn($"Could not get connection to DB for region '{regionId}'.");
					return null;
				}
				using (var cmd = conn.CreateCommand()) {
					cmd.CommandText = @"SELECT
							pr.UUID,
							pr.SceneGroupID,
							pr.LinkNumber,
							pr.Name,
							pr.ObjectFlags,
							ps.State,
							pr.PositionX, pr.PositionY, pr.PositionZ,
							pr.GroupPositionX, pr.GroupPositionY, pr.GroupPositionZ,
							ps.ScaleX, ps.ScaleY, ps.ScaleZ,
							pr.RotationX, pr.RotationY, pr.RotationZ, pr.RotationW,
							ps.Texture
						FROM
							prims pr
							INNER JOIN primshapes ps USING (UUID)
						WHERE
							pr.GroupPositionZ < 766 /* = max terrain height (510) + render height (256) */
							AND LENGTH(ps.Texture) > 0
							AND pr.ObjectFlags & (0 | 0x40000 | 0x20000) = 0
							AND ps.ScaleX > 1.0
							AND ps.ScaleY > 1.0
							AND ps.ScaleZ > 1.0
							AND ps.PCode NOT IN (255, 111, 95)
							AND RegionUUID = @region_id
						ORDER BY
							GroupPositionZ, PositionZ
					";
					cmd.Parameters.AddWithValue("region_id", regionId);
					cmd.CommandTimeout = 600;
					cmd.Prepare();
					IDataReader reader = null;
					try {
						reader = DBHelpers.ExecuteReader(cmd);
					}
					catch (Exception e) {
						LOG.Warn($"Prims DB reader query threw an error when attempting to get prims for region '{regionId}'.", e);
					}

					if (reader == null) {
						LOG.Warn($"Prims DB reader query returned nothing for region '{regionId}'.");
						return null;
					}

					var prims = new List<Prim>(); // List was chosen because it guarantees that insertion order will be preserved unless explictly sorted.

					var rootPrims = new Dictionary<Guid, Prim>();
					var childPrims = new List<Prim>(); // List was chosen because it guarantees that insertion order will be preserved unless explictly sorted.

					try {
						while (reader.Read()) {
							// Nullables start here
							var groupPosX = RDBMap.GetDBValueOrNull<double>(reader, "GroupPositionX");
							var groupPosY = RDBMap.GetDBValueOrNull<double>(reader, "GroupPositionY");
							var groupPosZ = RDBMap.GetDBValueOrNull<double>(reader, "GroupPositionZ");
							var posX = RDBMap.GetDBValueOrNull<double>(reader, "PositionX");
							var posY = RDBMap.GetDBValueOrNull<double>(reader, "PositionY");
							var posZ = RDBMap.GetDBValueOrNull<double>(reader, "PositionZ");
							var rotW = RDBMap.GetDBValueOrNull<double>(reader, "RotationW");
							var rotX = RDBMap.GetDBValueOrNull<double>(reader, "RotationX");
							var rotY = RDBMap.GetDBValueOrNull<double>(reader, "RotationY");
							var rotZ = RDBMap.GetDBValueOrNull<double>(reader, "RotationZ");

							var prim = new Prim {
								GroupPosition = groupPosX != null && groupPosY != null && groupPosZ != null ? new Vector3(
									(float)groupPosX,
									(float)groupPosY,
									(float)groupPosZ
								) : (Vector3?)null,
								Id = Guid.Parse(RDBMap.GetDBValue(reader, "UUID")),
								Name = RDBMap.GetDBValue(reader, "Name"),
								ObjectFlags = RDBMap.GetDBValue<int>(reader, "ObjectFlags"),
								Position = posX != null && posY != null && posZ != null ? new Vector3(
									(float)posX,
									(float)posY,
									(float)posZ
								) : (Vector3?)null,
								RegionId = regionId,
								RootRotation = null,
								Rotation = rotW != null && rotX != null && rotY != null && rotZ != null ? new Quaternion(
									(float)rotX,
									(float)rotY,
									(float)rotZ,
									(float)rotW
								) : (Quaternion?)null,
								Scale = new Vector3(
									(float)RDBMap.GetDBValue<double>(reader, "ScaleX"),
									(float)RDBMap.GetDBValue<double>(reader, "ScaleY"),
									(float)RDBMap.GetDBValue<double>(reader, "ScaleZ")
								),
								State = RDBMap.GetDBValue<int>(reader, "State"),
								Texture = (byte[])reader["Texture"],
							};

							prims.Add(prim);

							var sogIdStr = RDBMap.GetDBValue(reader, "SceneGroupID");
							if (Guid.TryParse(sogIdStr, out var sogId)) {
								prim.SceneGroupId = sogId;

								var linkNumber = RDBMap.GetDBValue<int>(reader, "LinkNumber");
								if (linkNumber == 1) {
									rootPrims.Add(sogId, prim);
								}
								else if (linkNumber > 1) {
									childPrims.Add(prim);
								}
							}
						}
					}
					finally {
						reader.Close();
					}

					foreach (var prim in childPrims) {
						if (rootPrims.TryGetValue(prim.SceneGroupId, out var rootPrim)) {
							prim.RootRotation = rootPrim.Rotation;
						}
					}

					return prims;
				}
			}
		}

		#region Public Properties and Accessors

		public Guid Id { get; private set; }

		public string Name { get; private set; }

		private Guid SceneGroupId;

		public Guid? RegionId { get; private set; }

		public int? ObjectFlags { get; private set; }

		public int? State { get; private set; }

		public Vector3? Position { get; private set; }

		public Vector3? GroupPosition { get; private set; }

		public Quaternion? Rotation { get; private set; }

		public Vector3 Scale { get; private set; }

		public Quaternion? RootRotation { get; private set; } = null;

		public byte[] Texture { get; private set; }

		#endregion

		public Vector3? ComputeWorldPosition() {
			if (RootRotation == null) {
				// Is a root or childless prim.
				return GroupPosition;
			}
			if (RootRotation == null || Position == null || GroupPosition == null) {
				return null;
			}
			// Is a child prim.

			var translationOffsetPosition = Position * RootRotation;

			return GroupPosition + translationOffsetPosition;
		}

		public Quaternion? ComputeWorldRotation() {
			if (RootRotation == null) {
				// Is a root or childless prim.
				return Rotation;
			}
			if (RootRotation == null || Rotation == null) {
				return null;
			}
			// Is a child prim.

			return RootRotation * Rotation;
		}

		/// <summary>
		/// Allows the prim position to be shifted by the given amount.  Good for when the prim is actually owned by an adjacent region.
		/// </summary>
		/// <param name="offset">Offset.</param>
		internal void Offset(Vector2 offset) {
			if (GroupPosition != null) {
				var position = (Vector3)GroupPosition;
				position.X += offset.X;
				position.Y += offset.Y;
				GroupPosition = position;
			}
		}

		#region Constructors

		private Prim() {
		}

		#endregion
	}
}
