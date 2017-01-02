// PrimColoredOBBRenderer.cs
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
using DataReader;
using log4net;
using Nini.Config;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;

namespace Anaximander {
	public static class PrimColoredOBBRenderer {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public struct DrawStruct {
			public float sort_order;
			public SolidBrush brush;
			public Point[] vertices;
		}

		public static void SetConfig(IConfigSource config) {
		}

		public static DirectBitmap DrawObjects(IEnumerable<Prim> prims, double[,] heightMap, DirectBitmap mapbmp) {
			float scale_factor = (float)mapbmp.Height / 256f;

			var drawdata_for_sorting = new List<DrawStruct>();

			// Get all the faces for valid prims and prep them for drawing.
			foreach (var prim in prims) {
				DrawStruct drawdata;

				var vertices = new Vector3[8];

				var pos = ComputeWorldPosition(prim);

				if (
					// skip prim in non-finite position
					Single.IsNaN(pos.X) || Single.IsNaN(pos.Y) ||
					Single.IsInfinity(pos.X) || Single.IsInfinity(pos.Y) ||

					// skip prim outside of region (REVISIT: prims can be outside of region and still overlap into the region.)
					pos.X < 0f || pos.X >= 256f || pos.Y < 0f || pos.Y >= 256f ||

					// skip prim Z at or above 256m above the terrain at that position.
					pos.Z >= (getHeight(heightMap, pos.X, pos.Y) + 256f))
					return null;

				var rot = ComputeWorldRotation(prim);

				var radial_scale = new Vector3() {
					X = (float)prim.ScaleX * 0.5f,
					Y = (float)prim.ScaleY * 0.5f,
					Z = (float)prim.ScaleZ * 0.5f,
				};
				Vector3 rotated_radial_scale;

				/* * * * * * * * * * * * * * * * * * */
				// OBB VERTEX COMPUTATION
				/* * * * * * * * * * * * * * * * * * */

				/*
				Vertex pattern:
				# XYZ
				0 --+
				1 +-+
				2 +++
				3 -++
				4 ---
				5 +--
				6 ++-
				7 -+-
				*/
				rotated_radial_scale.X = -radial_scale.X;
				rotated_radial_scale.Y = -radial_scale.Y;
				rotated_radial_scale.Z = radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[0].X = pos.X + rotated_radial_scale.X;
				vertices[0].Y = pos.Y + rotated_radial_scale.Y;
				vertices[0].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = radial_scale.X;
				rotated_radial_scale.Y = -radial_scale.Y;
				rotated_radial_scale.Z = radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[1].X = pos.X + rotated_radial_scale.X;
				vertices[1].Y = pos.Y + rotated_radial_scale.Y;
				vertices[1].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = radial_scale.X;
				rotated_radial_scale.Y = radial_scale.Y;
				rotated_radial_scale.Z = radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[2].X = pos.X + rotated_radial_scale.X;
				vertices[2].Y = pos.Y + rotated_radial_scale.Y;
				vertices[2].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = -radial_scale.X;
				rotated_radial_scale.Y = radial_scale.Y;
				rotated_radial_scale.Z = radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[3].X = pos.X + rotated_radial_scale.X;
				vertices[3].Y = pos.Y + rotated_radial_scale.Y;
				vertices[3].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = -radial_scale.X;
				rotated_radial_scale.Y = -radial_scale.Y;
				rotated_radial_scale.Z = -radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[4].X = pos.X + rotated_radial_scale.X;
				vertices[4].Y = pos.Y + rotated_radial_scale.Y;
				vertices[4].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = radial_scale.X;
				rotated_radial_scale.Y = -radial_scale.Y;
				rotated_radial_scale.Z = -radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[5].X = pos.X + rotated_radial_scale.X;
				vertices[5].Y = pos.Y + rotated_radial_scale.Y;
				vertices[5].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = radial_scale.X;
				rotated_radial_scale.Y = radial_scale.Y;
				rotated_radial_scale.Z = -radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[6].X = pos.X + rotated_radial_scale.X;
				vertices[6].Y = pos.Y + rotated_radial_scale.Y;
				vertices[6].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = -radial_scale.X;
				rotated_radial_scale.Y = radial_scale.Y;
				rotated_radial_scale.Z = -radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[7].X = pos.X + rotated_radial_scale.X;
				vertices[7].Y = pos.Y + rotated_radial_scale.Y;
				vertices[7].Z = pos.Z + rotated_radial_scale.Z;

				/* * * * * * * * * * * * * * * * * * */
				// SORT HEIGHT CALC
				/* * * * * * * * * * * * * * * * * * */
				drawdata.sort_order =
					Math.Max(vertices[0].Z,
						Math.Max(vertices[1].Z,
							Math.Max(vertices[2].Z,
								Math.Max(vertices[3].Z,
									Math.Max(vertices[4].Z,
										Math.Max(vertices[5].Z,
											Math.Max(vertices[6].Z,
												vertices[7].Z
											)
										)
									)
								)
							)
						)
					)
				;
				
				/* * * * * * * * * * * * * * * * * * */
				// OBB DRAWING PREPARATION PASS
				/* * * * * * * * * * * * * * * * * * */

				// Compute face 0 of OBB and add if facing up.
				//time_start_temp = Environment.TickCount;
				//if (Vector3.Cross(Vector3.Subtract(vertices[1], vertices[0]), Vector3.Subtract(vertices[3], vertices[0])).Z > 0)
				if (ZOfCrossDiff(ref vertices[0], ref vertices[1], ref vertices[3]) > 0)
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.brush = GetFaceBrush(prim, 0);
					//time_obb_brush += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.vertices = new Point[4];
					drawdata.vertices[0].X = (int)(vertices[0].X * scale_factor);
					drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);

					drawdata.vertices[1].X = (int)(vertices[1].X * scale_factor);
					drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);

					drawdata.vertices[2].X = (int)(vertices[2].X * scale_factor);
					drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);

					drawdata.vertices[3].X = (int)(vertices[3].X * scale_factor);
					drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);
					//time_obb_calc += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata_for_sorting.Add(drawdata);
					//time_obb_addtolist += Environment.TickCount - time_start_temp;
				}
				else
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;
				}

				// Compute face 1 of OBB and add if facing up.
				//time_start_temp = Environment.TickCount;
				//if (Vector3.Cross(Vector3.Subtract(vertices[5], vertices[4]), Vector3.Subtract(vertices[0], vertices[4])).Z > 0)
				if (ZOfCrossDiff(ref vertices[4], ref vertices[5], ref vertices[0]) > 0)
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.brush = GetFaceBrush(prim, 1);
					//time_obb_brush += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.vertices = new Point[4];
					drawdata.vertices[0].X = (int)(vertices[4].X * scale_factor);
					drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);

					drawdata.vertices[1].X = (int)(vertices[5].X * scale_factor);
					drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

					drawdata.vertices[2].X = (int)(vertices[1].X * scale_factor);
					drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);

					drawdata.vertices[3].X = (int)(vertices[0].X * scale_factor);
					drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);
					//time_obb_calc += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata_for_sorting.Add(drawdata);
					//time_obb_addtolist += Environment.TickCount - time_start_temp;
				}
				else
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;
				}

				// Compute face 2 of OBB and add if facing up.
				//time_start_temp = Environment.TickCount;
				//if (Vector3.Cross(Vector3.Subtract(vertices[6], vertices[5]), Vector3.Subtract(vertices[1], vertices[5])).Z > 0)
				if (ZOfCrossDiff(ref vertices[5], ref vertices[6], ref vertices[1]) > 0)
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.brush = GetFaceBrush(prim, 2);
					//time_obb_brush += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.vertices = new Point[4];
					drawdata.vertices[0].X = (int)(vertices[5].X * scale_factor);
					drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

					drawdata.vertices[1].X = (int)(vertices[6].X * scale_factor);
					drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

					drawdata.vertices[2].X = (int)(vertices[2].X * scale_factor);
					drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);

					drawdata.vertices[3].X = (int)(vertices[1].X * scale_factor);
					drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);
					//time_obb_calc += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata_for_sorting.Add(drawdata);
					//time_obb_addtolist += Environment.TickCount - time_start_temp;
				}
				else
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;
				}

				// Compute face 3 of OBB and add if facing up.
				//time_start_temp = Environment.TickCount;
				//if (Vector3.Cross(Vector3.Subtract(vertices[7], vertices[6]), Vector3.Subtract(vertices[2], vertices[6])).Z > 0)
				if (ZOfCrossDiff(ref vertices[6], ref vertices[7], ref vertices[2]) > 0)
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.brush = GetFaceBrush(prim, 3);
					//time_obb_brush += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.vertices = new Point[4];
					drawdata.vertices[0].X = (int)(vertices[6].X * scale_factor);
					drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

					drawdata.vertices[1].X = (int)(vertices[7].X * scale_factor);
					drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

					drawdata.vertices[2].X = (int)(vertices[3].X * scale_factor);
					drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);

					drawdata.vertices[3].X = (int)(vertices[2].X * scale_factor);
					drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);
					//time_obb_calc += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata_for_sorting.Add(drawdata);
					//time_obb_addtolist += Environment.TickCount - time_start_temp;
				}
				else
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;
				}

				// Compute face 4 of OBB and add if facing up.
				//time_start_temp = Environment.TickCount;
				//if (Vector3.Cross(Vector3.Subtract(vertices[4], vertices[7]), Vector3.Subtract(vertices[3], vertices[7])).Z > 0)
				if (ZOfCrossDiff(ref vertices[7], ref vertices[4], ref vertices[3]) > 0)
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.brush = GetFaceBrush(prim, 4);
					//time_obb_brush += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.vertices = new Point[4];
					drawdata.vertices[0].X = (int)(vertices[7].X * scale_factor);
					drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

					drawdata.vertices[1].X = (int)(vertices[4].X * scale_factor);
					drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);

					drawdata.vertices[2].X = (int)(vertices[0].X * scale_factor);
					drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);

					drawdata.vertices[3].X = (int)(vertices[3].X * scale_factor);
					drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);
					//time_obb_calc += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata_for_sorting.Add(drawdata);
					//time_obb_addtolist += Environment.TickCount - time_start_temp;
				}
				else
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;
				}

				// Compute face 5 of OBB and add if facing up.
				//time_start_temp = Environment.TickCount;
				//if (Vector3.Cross(Vector3.Subtract(vertices[6], vertices[7]), Vector3.Subtract(vertices[4], vertices[7])).Z > 0)
				if (ZOfCrossDiff(ref vertices[7], ref vertices[6], ref vertices[4]) > 0)
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.brush = GetFaceBrush(prim, 5);
					//time_obb_brush += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata.vertices = new Point[4];
					drawdata.vertices[0].X = (int)(vertices[7].X * scale_factor);
					drawdata.vertices[0].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

					drawdata.vertices[1].X = (int)(vertices[6].X * scale_factor);
					drawdata.vertices[1].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

					drawdata.vertices[2].X = (int)(vertices[5].X * scale_factor);
					drawdata.vertices[2].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

					drawdata.vertices[3].X = (int)(vertices[4].X * scale_factor);
					drawdata.vertices[3].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);
					//time_obb_calc += Environment.TickCount - time_start_temp;

					//time_start_temp = Environment.TickCount;
					drawdata_for_sorting.Add(drawdata);
					//time_obb_addtolist += Environment.TickCount - time_start_temp;
				}
				else
				{
					//time_obb_norm += Environment.TickCount - time_start_temp;
				}
			}

			// Sort faces by Z position
			drawdata_for_sorting.Sort((h1, h2) => h1.sort_order.CompareTo(h2.sort_order));;

			// Draw the faces
			using (Graphics g = Graphics.FromImage(mapbmp.Bitmap))
			{
				g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
				g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
				g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

				for (int s = 0; s < drawdata_for_sorting.Count; s++)
				{
					g.FillPolygon(drawdata_for_sorting[s].brush, drawdata_for_sorting[s].vertices);
				}
			}

			return mapbmp;
		}

		#region Utility Helpers

		private static Vector3 ComputeWorldPosition(Prim prim) {
			if (prim.RootRotationX == null) {
				var position = new Vector3(prim.GroupPositionX, prim.GroupPositionY, prim.GroupPositionZ);

				return position/*GroupPosition*/;
			}

			var parentRot = new Quaternion((float)prim.RootRotationX, (float)prim.RootRotationY, (float)prim.RootRotationZ, (float)prim.RootRotationW); //ParentGroup.RootPart.RotationOffset;

			var axPos = new Vector3(prim.PositionX, prim.PositionY, prim.PositionZ); //OffsetPosition;
			axPos *= parentRot;
			var translationOffsetPosition = axPos;

			var groupPosition = new Vector3(prim.GroupPositionX, prim.GroupPositionY, prim.GroupPositionZ);

			return groupPosition/*GroupPosition*/ + translationOffsetPosition;
		}
		
		private static Quaternion ComputeWorldRotation(Prim prim) {
			if (prim.RootRotationX == null) {
				var rot = new Quaternion(prim.RotationX, prim.RotationY, prim.RotationZ, prim.RotationW); // RotationOffset;
				return rot;
			}

			var parentRot = new Quaternion((float)prim.RootRotationX, (float)prim.RootRotationY, (float)prim.RootRotationZ, (float)prim.RootRotationW); //ParentGroup.RootPart.RotationOffset;
			var oldRot = new Quaternion(prim.RotationX, prim.RotationY, prim.RotationZ, prim.RotationW); // RotationOffset;
			return parentRot * oldRot;
		}

		private static readonly SolidBrush DefaultBrush = new SolidBrush(Color.Black);
		private static SolidBrush GetFaceBrush(Prim prim, uint face)
		{
			// Block sillyness that would cause an exception.
			if (face >= OpenMetaverse.Primitive.TextureEntry.MAX_FACES)
				return DefaultBrush;
			var textures = new OpenMetaverse.Primitive.TextureEntry(prim.Texture, 0, prim.Texture.Length);

			var facetexture = textures.GetFace(face);
			// GetFace throws a generic exception if the parameter is greater than MAX_FACES.

			// TODO: compute a better color from the texture data AND the color applied.

			return new SolidBrush(Color.FromArgb(
				Math.Max(0, Math.Min(255, (int)(facetexture.RGBA.R * 255f))),
				Math.Max(0, Math.Min(255, (int)(facetexture.RGBA.G * 255f))),
				Math.Max(0, Math.Min(255, (int)(facetexture.RGBA.B * 255f)))
			));
			// FromARGB can throw an exception if a parameter is outside 0-255, but that is prevented.
		}

		private static float ZOfCrossDiff(ref Vector3 P, ref Vector3 Q, ref Vector3 R) {
			// let A = Q - P
			// let B = R - P
			// Vz = AxBy - AyBx
			//    = (Qx - Px)(Ry - Py) - (Qy - Py)(Rx - Px)
			return (Q.X - P.X)* (R.Y - P.Y) - (Q.Y - P.Y) * (R.X - P.X);
		}

		/// <summary>
		/// Gets the height at the specified location, blending the value depending on the proximity to the pixel center.
		/// AKA: bilinear filtering.
		/// </summary>
		/// <returns>The height in meters.</returns>
		/// <param name="hm">The heightmap array.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		private static double getHeight(double[,] hm, double x, double y) {
			int x_0 = (int)x, y_0 = (int)y;
			int x_1 = x_0 + 1, y_1 = y_0 + 1;

			var x_ratio = x - x_0; // The fractional part gives the 0-1 ratio needed.
			var y_ratio = y - y_0;

			// Unit square interpretation of bilinear filtering.
			if (x_0 < hm.GetLength(0) - 1 && y_0 < hm.GetLength(1) - 1)
				return
					hm[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					hm[x_1, y_0] * x_ratio * (1 - y_ratio) +
					hm[x_0, y_1] * (1 - x_ratio) * y_ratio +
					hm[x_1, y_1] * x_ratio * y_ratio;
			else if (x_0 < hm.GetLength(0) - 1)
				return
					hm[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					hm[x_1, y_0] * x_ratio * (1 - y_ratio);
			else if (y_0 < hm.GetLength(1) - 1)
				return
					hm[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					hm[x_0, y_1] * (1 - x_ratio) * y_ratio;
			else
				return
					hm[x_0, y_0];
		}

		#endregion
	}
}

