// HSV.cs
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
using System.Drawing;

namespace Anaximander {
	/// <summary>
	/// Hue, Saturation, Value; used for color-interpolation
	/// </summary>
	public struct HSV : IEquatable<HSV> {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private float h;
		private float s;
		private float v;

		public HSV(float h, float s, float v) {
			this.h = h;
			this.s = s;
			this.v = v;
		}

		// (for info about algorithm, see http://en.wikipedia.org/wiki/HSL_and_HSV)
		public HSV(Color c) {
			var r = c.R / 255f;
			var g = c.G / 255f;
			var b = c.B / 255f;
			var max = Math.Max(Math.Max(r, g), b);
			var min = Math.Min(Math.Min(r, g), b);
			var diff = max - min;

			if (Math.Abs(max - min) < 0.001) {
				h = 0f;
			}
			else if (Math.Abs(max - r) < 0.001) {
				h = (g - b) / diff * 60f;
			}
			else if (Math.Abs(max - g) < 0.001) {
				h = (b - r) / diff * 60f + 120f;
			}
			else {
				h = (r - g) / diff * 60f + 240f;
			}

			if (h < 0f) {
				h += 360f;
			}

			if (Math.Abs(max) <= 0.001f) {
				s = 0f;
			}
			else {
				s = diff / max;
			}

			v = max;
		}

		// (for info about algorithm, see http://en.wikipedia.org/wiki/HSL_and_HSV)
		public Color ToColor() {
			if (s < 0f) {
				LOG.Debug("S < 0: " + s);
			}
			else if (s > 1f) {
				LOG.Debug("S > 1: " + s);
			}

			if (v < 0f) {
				LOG.Debug("V < 0: " + v);
			}
			else if (v > 1f) {
				LOG.Debug("V > 1: " + v);
			}

			var f = h / 60f;
			var sector = (int)f % 6;
			f = f - (int)f;
			var pi = Math.Max(0, Math.Min(255, (int)(v * (1f - s) * 255f)));
			var qi = Math.Max(0, Math.Min(255, (int)(v * (1f - s * f) * 255f)));
			var ti = Math.Max(0, Math.Min(255, (int)(v * (1f - (1f - f) * s) * 255f)));
			var vi = Math.Max(0, Math.Min(255, (int)(v * 255f)));

			switch (sector) {
				case 0:
					return Color.FromArgb(vi, ti, pi);
				case 1:
					return Color.FromArgb(qi, vi, pi);
				case 2:
					return Color.FromArgb(pi, vi, ti);
				case 3:
					return Color.FromArgb(pi, qi, vi);
				case 4:
					return Color.FromArgb(ti, pi, vi);
				default:
					return Color.FromArgb(vi, pi, qi);
			}
		}

		// interpolate two colors in HSV space and return the resulting color
		public HSV InterpolateHSV(ref HSV c2, float ratio) {
			if (ratio <= 0f) {
				return this;
			}
			if (ratio >= 1f) {
				return c2;
			}

			// make sure we are on the same side on the hue-circle for interpolation
			// We change the hue of the parameters here, but we don't change the color
			// represented by that value
			if (h - c2.h > 180f) {
				h -= 360f;
			}
			else if (c2.h - h > 180f) {
				h += 360f;
			}

			return new HSV(h * (1f - ratio) + c2.h * ratio,
				s * (1f - ratio) + c2.s * ratio,
				v * (1f - ratio) + c2.v * ratio);
		}

		#region IEquatable

		bool IEquatable<HSV>.Equals(HSV other) {
			return
				Math.Abs(h - other.h) < .000001
				&& Math.Abs(s - other.s) < .000001
				&& Math.Abs(v - other.v) < .000001
			;
		}

		#endregion
	}
}
