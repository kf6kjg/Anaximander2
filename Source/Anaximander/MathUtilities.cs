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
using OpenMetaverse;

namespace Anaximander {
	public static class MathUtilities {
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
	}
}
