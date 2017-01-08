// TileTreeNode.cs
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
using System.Drawing;

namespace Anaximander {
	public class TileTreeNode : IDisposable {
		public int X { get; private set; }
		public int Y { get; private set; }
		public int Zoom { get; private set; }

		public string Id { get { return MakeId(X, Y, Zoom); } }

		public Bitmap Image { get; private set; } = null;

		public bool Disposed { get; private set; }

		public string ParentNodeId { get; private set; }

		private readonly string[] _childNodeIds = new string[4];
		public int ChildNodeCount { get; private set; } = 0;
		public IList<string> ChildNodeIds { get { return _childNodeIds; } }

		#region Ctor and cleanup tools

		public TileTreeNode(int x, int y, int zoom) {
			X = x;
			Y = y;
			Zoom = zoom;

			_childNodeIds.Initialize();
		}

		~TileTreeNode() {
			Image?.Dispose();
			Image = null;
		}

		public void Dispose() {
			if (Disposed)
				return;
			Disposed = true;
			DisposeImage();
		}

		#endregion

		public static string MakeId(int x, int y, int zoom) {
			return $"{zoom}-{x}-{y}";
		}

		public void CreateImage(int width, int height, Color fill_color) {
			DisposeImage();

			Image = new Bitmap(width, height);
			using (var gfx = Graphics.FromImage(Image))
			using (var brush = new SolidBrush(fill_color)) {
				gfx.FillRectangle(brush, 0, 0, width, height);
			}
		}

		public void CreateImage(int width, int height, Bitmap old_image) {
			// Don't dispose of the old until the new is made in case someone uses this to scale the image in place.  Which I do.
			var image = new Bitmap(old_image, width, height);

			DisposeImage();

			Image = image;
		}

		public void DisposeImage() {
			Image?.Dispose();
			Image = null;
		}

		public void SetParent(string node_id) {
			if (!string.IsNullOrWhiteSpace(node_id)) {
				ParentNodeId = node_id;
			}
		}

		public void AddChild(string node_id) {
			if (ChildNodeCount < _childNodeIds.Length) {
				_childNodeIds[ChildNodeCount++] = node_id;
			}
		}
	}
}

