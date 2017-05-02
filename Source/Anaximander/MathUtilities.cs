// MathUtilities.cs
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
using DataReader;
using OpenMetaverse;

namespace Anaximander {
	public static class MathUtilities {
		public static Vector3 ComputeWorldPosition(Prim prim) {
			var groupPosition = new Vector3(prim.GroupPositionX, prim.GroupPositionY, prim.GroupPositionZ);

			if (prim.RootRotationX == null) {
				// Is a root or childless prim.
				return groupPosition;
			}
			// Is a child prim.

			var parentRot = new Quaternion((float)prim.RootRotationX, (float)prim.RootRotationY, (float)prim.RootRotationZ, (float)prim.RootRotationW); //ParentGroup.RootPart.RotationOffset;

			var axPos = new Vector3(prim.PositionX, prim.PositionY, prim.PositionZ); //OffsetPosition;
			axPos *= parentRot;
			var translationOffsetPosition = axPos;

			return groupPosition/*GroupPosition*/ + translationOffsetPosition;
		}

		public static Quaternion ComputeWorldRotation(Prim prim) {
			var rotationOffset = new Quaternion(prim.RotationX, prim.RotationY, prim.RotationZ, prim.RotationW); // RotationOffset;

			if (prim.RootRotationX == null) {
				// Is a root or childless prim.
				return rotationOffset;
			}
			// Is a child prim.

			var parentRot = new Quaternion((float)prim.RootRotationX, (float)prim.RootRotationY, (float)prim.RootRotationZ, (float)prim.RootRotationW); //ParentGroup.RootPart.RotationOffset;
			return parentRot * rotationOffset;
		}

		public static float ZOfCrossDiff(ref Vector3 P, ref Vector3 Q, ref Vector3 R) {
			// let A = Q - P
			// let B = R - P
			// Vz = AxBy - AyBx
			//    = (Qx - Px)(Ry - Py) - (Qy - Py)(Rx - Px)
			return (Q.X - P.X) * (R.Y - P.Y) - (Q.Y - P.Y) * (R.X - P.X);
		}

		// S-curve: f(x) = 3x² - 2x³:
		// f(0) = 0, f(0.5) = 0.5, f(1) = 1,
		// f'(x) = 0 at x = 0 and x = 1; f'(0.5) = 1.5,
		// f''(0.5) = 0, f''(x) != 0 for x != 0.5
		public static double SCurve(double v) {
			return (v * v * (3f - 2f * v));
		}

		/// <summary>
		/// Gets the height at the specified location, blending the value depending on the proximity to the pixel center.
		/// AKA: bilinear filtering.
		/// </summary>
		/// <returns>The height in meters.</returns>
		/// <param name="hm">The heightmap array.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		public static double GetBlendedHeight(double[,] hm, double x, double y) {
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
	}
}
