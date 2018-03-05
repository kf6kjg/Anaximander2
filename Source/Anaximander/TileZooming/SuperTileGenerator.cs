// SuperTileGenerator.cs
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
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace Anaximander {
	public class SuperTileGenerator {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private readonly int _tilePixelSize;

		private readonly int _maxZoomLevel;

		private readonly RDBMap _rdbMap;

		private readonly Dictionary<string, TileTreeNode> _allNodesById = new Dictionary<string, TileTreeNode>();
		public IEnumerable<string> AllNodesById => _allNodesById.Keys;
		private readonly List<string> _rootNodeIds = new List<string>();
		private readonly TileImageWriter _imageWriter;

		private readonly Color _oceanColor;

		private readonly bool _serverMode;

		public SuperTileGenerator(IConfigSource config, RDBMap rdbmap) {
			var tileInfo = config.Configs["MapTileInfo"];
			_tilePixelSize = tileInfo?.GetInt("PixelScale", Constants.PixelScale) ?? Constants.PixelScale;

			var zoomInfo = config.Configs["TileZooming"];
			_maxZoomLevel = zoomInfo?.GetInt("HighestZoomLevel", Constants.HighestZoomLevel) ?? Constants.HighestZoomLevel;

			_imageWriter = new TileImageWriter(config);

			_rdbMap = rdbmap;

			var _tileInfo = config.Configs["MapTileInfo"];

			_oceanColor = Color.FromArgb(
				_tileInfo?.GetInt("OceanColorRed", Constants.OceanColor.R) ?? Constants.OceanColor.R,
				_tileInfo?.GetInt("OceanColorGreen", Constants.OceanColor.G) ?? Constants.OceanColor.G,
				_tileInfo?.GetInt("OceanColorBlue", Constants.OceanColor.B) ?? Constants.OceanColor.B
			);

			_serverMode = config.Configs["Startup"].GetBoolean("ServerMode", Constants.KeepRunningDefault);
		}

		public void PreloadTileTrees(IEnumerable<Guid> region_ids) {
			_rootNodeIds.Clear();
			_allNodesById.Clear();

			LOG.Debug($"Preparing tree: loading bottom layer of {region_ids?.Count()} tiles...");
			// Preload the base layer as given.
			foreach (var region_id in region_ids) {
				var region = _rdbMap.GetRegionByUUID(region_id);
				if (!region.HasKnownCoordinates()) {
					continue; // Skip over the regions that have no known location.
				}

				// Add all regions that would show on the same super tile to make sure a full picture is generated.
				var super_x = ((int)region.Location?.X >> 1) << 1;
				var super_y = ((int)region.Location?.Y >> 1) << 1;

				if (_rdbMap.GetRegionByLocation(super_x, super_y) != null) {
					var node = new TileTreeNode(super_x, super_y, 1);

					try { // Might already be added.
						_allNodesById.Add(node.Id, node);
						_rootNodeIds.Add(node.Id);
					}
					catch (ArgumentException) {
						// Don't care.
					}
				}

				if (_rdbMap.GetRegionByLocation(super_x + 1, super_y) != null) {
					var node = new TileTreeNode(super_x + 1, super_y, 1);

					try { // Might already be added.
						_allNodesById.Add(node.Id, node);
						_rootNodeIds.Add(node.Id);
					}
					catch (ArgumentException) {
						// Don't care.
					}
				}

				if (_rdbMap.GetRegionByLocation(super_x, super_y + 1) != null) {
					var node = new TileTreeNode(super_x, super_y + 1, 1);

					try { // Might already be added.
						_allNodesById.Add(node.Id, node);
						_rootNodeIds.Add(node.Id);
					}
					catch (ArgumentException) {
						// Don't care.
					}
				}

				if (_rdbMap.GetRegionByLocation(super_x + 1, super_y + 1) != null) {
					var node = new TileTreeNode(super_x + 1, super_y + 1, 1);

					try { // Might already be added.
						_allNodesById.Add(node.Id, node);
						_rootNodeIds.Add(node.Id);
					}
					catch (ArgumentException) {
						// Don't care.
					}
				}
			}

			// Generate tree of tiles using a bottom-up breadth-first algorithm.
			for (int zoom_level = 1; zoom_level < _maxZoomLevel; ++zoom_level) {
				LOG.Debug($"Preparing tree: loading zoom level {zoom_level + 1}...");
				var current_layer_ids = new List<string>();

				// Move the top layer into the current layer for the next pass.
				current_layer_ids.AddRange(_rootNodeIds);
				_rootNodeIds.Clear();

				foreach (var node_id in current_layer_ids) {
					var branch = _allNodesById[node_id];

					// Find super tile.
					var super_x = (branch.X >> zoom_level) << zoom_level; // = Math.floor(region.x / Math.pow(2, zoom_level)) * Math.pow(2, zoom_level)
					var super_y = (branch.Y >> zoom_level) << zoom_level; // = Math.floor(region.y / Math.pow(2, zoom_level)) * Math.pow(2, zoom_level)
					var super = new TileTreeNode(super_x, super_y, zoom_level + 1);
					try {
						// Super tile might not exist, so try adding it.
						_allNodesById.Add(super.Id, super);
						_rootNodeIds.Add(super.Id);
					}
					catch (ArgumentException) {
						// Tile exists, go get it using the already computed ID.
						super = _allNodesById[super.Id];
					}

					// Graft the current branch onto the tree.
					branch.SetParent(super.Id);
					super.AddChild(node_id);
				}
			}

			LOG.Debug($"Preparing tree complete.");
		}

		public void GeneratePreloadedTree() {
			// Build the tile images using a post-order depth-first algorithm on the above trees.
			// Turns out this is not a trivial problem to solve.  Many thanks to Dave Remy: http://blogs.msdn.com/b/daveremy/archive/2010/03/16/non-recursive-post-order-depth-first-traversal.aspx
			var count = 0;
			foreach (var node_id in _rootNodeIds) {
				var ids_to_visit = new Stack<string>();
				var visited_ancestor_ids = new Stack<string>();
				ids_to_visit.Push(node_id);

				LOG.Debug($"Drawing tree: processing root node {++count} of {_rootNodeIds.Count}...");

				var children = 0;
				while (ids_to_visit.Count > 0) {
					LOG.Debug($"Drawing tree: processing (grand)child node {++children} of {ids_to_visit.Count} and appending any further children to this list...");

					var branch_id = ids_to_visit.Peek();
					var branch = _allNodesById[branch_id];

					if (branch.ChildNodeIds.Any()) {
						if (visited_ancestor_ids.Count == 0 || visited_ancestor_ids.Peek() != branch_id) {
							visited_ancestor_ids.Push(branch_id);

							// Append the child list, but in reverse.
							var index = branch.ChildNodeCount;
							while (--index >= 0) {
								ids_to_visit.Push(branch.ChildNodeIds[index]);
							}

							continue;
						}

						visited_ancestor_ids.Pop();
					}

					if (branch.Zoom == 1) {
						// Zoom 1 is the definition of a leaf.
						var image = _imageWriter.LoadTile(branch.X, branch.Y, branch.Zoom);

						if (image != null) {
							branch.CreateImage(_tilePixelSize, _tilePixelSize, image);
						}
						else {
							// We couldn't find or load the image for the region! Just fill with ocean as this meant DB trouble or something else that caused a failure to render.
							branch.CreateImage(_tilePixelSize, _tilePixelSize, _oceanColor);
						}
					}

					// Ah, you are ready for reduction, export, and compiling into your parent then!

					// But if you are a leaf (region tile) then we'll let you slide...
					else { //if (branch.Zoom > 1) {
						// Save off fullres uncompressed bitmaps of the super tile for helping with future partial updates.
						if (_serverMode) {
							_imageWriter.WriteRawTile(branch.X, branch.Y, branch.Zoom, branch.Image);
						}

						// Scale down to _tilePixelSize
						branch.CreateImage(_tilePixelSize, _tilePixelSize, branch.Image);

						// Save to disk.
						_imageWriter.WriteTile(branch.X, branch.Y, branch.Zoom, Guid.Empty, branch.Image);
					}

					// Compile into parent tile.  Unless we are at the root of this tree!
					if (!string.IsNullOrWhiteSpace(branch.ParentNodeId) && branch.Image != null) {
						var parent = _allNodesById[branch.ParentNodeId];

						if (parent.Image == null) {
							// if this is server mode, attempt to load from disk and update it instead of just wiping out all the data during partial updates.
							var image = _serverMode ? _imageWriter.LoadRawTile(parent.X, parent.Y, parent.Zoom) : null;

							if (image != null) {
								parent.CreateImage(_tilePixelSize * 2, _tilePixelSize * 2, image); // In theory there won't be any scaling - unless the resolution was changed between runs.
							}
							else {
								// Just fill with ocean.
								parent.CreateImage(_tilePixelSize * 2, _tilePixelSize * 2, _oceanColor);
							}
						}

						var offset_x = Math.Abs(((branch.X - parent.X) * _tilePixelSize) >> (branch.Zoom - 1));
						var offset_y = _tilePixelSize - Math.Abs(((branch.Y - parent.Y) * _tilePixelSize) >> (branch.Zoom - 1)); // Y coordinates are reversed between images (+Y is down) and grid maps (+Y is up).

						// Composite the branch image onto the parent image at the offset position.
						using (var g = Graphics.FromImage(parent.Image)) {
							g.DrawImage(branch.Image, offset_x, offset_y, _tilePixelSize, _tilePixelSize);
						}
					}

					// Remove from memory.
					branch.DisposeImage();

					// Done.
					ids_to_visit.Pop();
				}
			}


			LOG.Debug($"Drawing tree: cleanup...");

			// All trees have been compiled, no need to keep this data in memory!
			_rootNodeIds.Clear();
			_allNodesById.Clear();

			LOG.Debug($"Drawing tree complete.");
		}
	}
}

