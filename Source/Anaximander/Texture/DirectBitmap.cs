// DirectBitmap.cs
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
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Anaximander {
	/// <summary>
	/// A very fast, but special purpose, tool for doing lots of drawing operations.
	/// </summary>
	public class DirectBitmap : IDisposable {
		public Bitmap Bitmap { get; private set; }

		public Int32[] Bits { get; private set; }

		public bool Disposed { get; private set; }

		public int Height { get; private set; }

		public int Width { get; private set; }

		protected GCHandle BitsHandle { get; private set; }

		public DirectBitmap(int width, int height) {
			Width = width;
			Height = height;
			Bits = new int[width * height];
			BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
			Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, BitsHandle.AddrOfPinnedObject());
		}

		public void Dispose() {
			if (Disposed)
				return;
			Disposed = true;
			Bitmap.Dispose();
			BitsHandle.Free();
		}
	}
}
