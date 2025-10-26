using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NullCullingManager : MonoBehaviour
{
	private readonly List<ChunkGroup> _chunkGroups = [];


	private readonly Dictionary<Renderer, ChunkGroup> _rendererToGroupMap = [];
	private readonly Dictionary<Cell, ChunkGroup> _cellToGroupMap = [];

	[SerializeField]
	internal CullingManager cullMan;


	public void CheckAllChunks() => _chunkGroups.ForEach(group => group.UpdateRendererVisibility());

	public void AddRendererToCell(Cell cell, Renderer newRend)
	{
		// Find the existing groups for the new renderer and cell
		bool hasRendererGroup = _rendererToGroupMap.TryGetValue(newRend, out ChunkGroup rendererGroup);
		bool hasCellGroup = _cellToGroupMap.TryGetValue(cell, out ChunkGroup cellGroup);

		// Case 1: Both renderer and cell are already in groups
		if (hasRendererGroup && hasCellGroup)
		{
			if (rendererGroup != cellGroup)
			{
				// They are in different groups, so we must merge them
				MergeGroups(rendererGroup, cellGroup);
			}
			// Otherwise, there's nothing to do
		}
		// Case 2: Only the renderer is in a group. Add the cell to it
		else if (hasRendererGroup)
		{
			rendererGroup.AddCell(cell);
			_cellToGroupMap[cell] = rendererGroup;
		}
		// Case 3: Only the cell is in a group. Add the renderer to it
		else if (hasCellGroup)
		{
			cellGroup.AddRenderer(newRend);
			_rendererToGroupMap[newRend] = cellGroup;
		}
		// Case 4: Neither is in a group. Create a new group for them
		else
		{
			// make group
			var newGroup = new ChunkGroup();
			newGroup.AddRenderer(newRend);
			newGroup.AddCell(cell);

			// Register the groups
			_chunkGroups.Add(newGroup);
			_rendererToGroupMap[newRend] = newGroup;
			_cellToGroupMap[cell] = newGroup;
		}
	}

	private void MergeGroups(ChunkGroup groupA, ChunkGroup groupB)
	{
		// Determine which group is smaller to minimize the number of items to move
		ChunkGroup smallerGroup = groupA.TotalCount < groupB.TotalCount ? groupA : groupB;
		ChunkGroup largerGroup = smallerGroup == groupA ? groupB : groupA;

		// Move all renderers from the smaller group to the larger one.
		foreach (var renderer in smallerGroup.Renderers)
		{
			largerGroup.AddRenderer(renderer);
			_rendererToGroupMap[renderer] = largerGroup; // Update the map entry.
		}

		// Move all cells from the smaller group to the larger one.
		foreach (var cell in smallerGroup.Cells)
		{
			largerGroup.AddCell(cell);
			_cellToGroupMap[cell] = largerGroup; // Update the map entry.
		}

		// The smaller group is now empty and obsolete, so remove it.
		_chunkGroups.Remove(smallerGroup);
	}


}

public class ChunkGroup
{
	// collections
	private readonly List<Chunk> _chunks = [];
	private readonly HashSet<Cell> _cells = [];
	private readonly HashSet<Renderer> _renderers = [];

	// Public getters (fancy IReadOnlyCollection... I should use this for safe readonly collections, huh)
	public IReadOnlyCollection<Cell> Cells => _cells;
	public IReadOnlyCollection<Renderer> Renderers => _renderers;
	public int TotalCount => _cells.Count + _renderers.Count;


	public bool IsVisible => _chunks.Exists(chunk => chunk.Rendering);

	public void AddRenderer(Renderer renderer) => _renderers.Add(renderer);
	public void AddCell(Cell cell) { _cells.Add(cell); _chunks.Add(cell.Chunk); }

	public void UpdateRendererVisibility()
	{
		bool shouldBeEnabled = IsVisible;

		foreach (var renderer in _renderers)
			if (renderer)
				renderer.enabled = shouldBeEnabled;
	}
}