// Texture.cs
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
using OpenMetaverse;
using System.Drawing;

namespace Anaximander {
	/// <summary>
	/// An immutable representation of a texture.
	/// </summary>
	public class Texture {
		#region Properties

		public UUID UUID { get; private set; }

		private Color _averageColor;
		private bool _averageColorHasBeenSet = false;
		public Color AverageColor {
			get {
				if (!_averageColorHasBeenSet) {
					AverageColor = computeAverageColor(UUID, _averageColor);
				}
				return _averageColor;
			}
			private set {
				_averageColor = value;
				_averageColorHasBeenSet = true;
			}
		}

		#endregion

		#region Constructors

		public Texture(UUID id, Color defaultAverageColor) {
			UUID = id;
			_averageColor = defaultAverageColor;
		}

		#endregion

		#region Helpers

		// Compute the average color of a texture.
		private static Color computeAverageColor(Bitmap bmp)
		{
			// we have 256 x 256 pixel, each with 256 possible color-values per
			// color-channel, so 2^24 is the maximum value we can get, adding everything.
			// int is be big enough for that.
			int r = 0, g = 0, b = 0;
			for (int y = 0; y < bmp.Height; ++y)
			{
				for (int x = 0; x < bmp.Width; ++x)
				{
					Color c = bmp.GetPixel(x, y);
					r += (int)c.R & 0xff;
					g += (int)c.G & 0xff;
					b += (int)c.B & 0xff;
				}
			}

			int pixels = bmp.Width * bmp.Height;
			return Color.FromArgb(r / pixels, g / pixels, b / pixels);
		}

		// return either the average color of the texture, or the defaultColor if the texturID is invalid
		// or the texture couldn't be found
		private static Color computeAverageColor(UUID textureID, Color defaultColor) {
			if (textureID == UUID.Zero) return defaultColor; // not set

			Bitmap bmp = null; // TODO fetchTexture(textureID);
			return bmp == null ? defaultColor : computeAverageColor(bmp);
		}

		#endregion
	}
}

