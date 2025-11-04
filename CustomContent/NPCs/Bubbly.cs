using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BBTimes.CustomComponents;
using BBTimes.CustomComponents.NpcSpecificComponents;
using BBTimes.Extensions;
using BBTimes.Manager;
using MTM101BaldAPI;
using PixelInternalAPI.Classes;
using PixelInternalAPI.Components;
using PixelInternalAPI.Extensions;
using UnityEngine;


namespace BBTimes.CustomContent.NPCs
{
	public class Bubbly : NPC, INPCPrefab
	{
		public void SetupPrefab()
		{
			Color subColor = new(1f, 0.345f, 0.886f);
			var sprs = this.GetSpriteSheet(3, 3, pixs, "bubblySheet.png");
			spriteRenderer[0].sprite = sprs[0];
			audMan = GetComponent<PropagatedAudioManager>();

			sprWalkingAnim = [.. sprs.Take(7)];
			sprPrepareBub = sprs[8];
			renderer = spriteRenderer[0];
			audFillUp = this.GetSound("Bubbly_BubbleSpawn.wav", "Vfx_Bubbly_Fillup", SoundType.Effect, subColor);

			var bubble = new GameObject("Bubble").AddComponent<Bubble>();
			bubble.gameObject.ConvertToPrefab(true);
			bubble.audPop = BBTimesManager.man.Get<SoundObject>("audPop");
			bubble.audMan = bubble.gameObject.CreatePropagatedAudioManager(85, 105);

			var visual = ObjectCreationExtensions.CreateSpriteBillboard(this.GetSprite(16f, "bubble.png")).AddSpriteHolder(out var bubbleVisual, 0f, 0);
			visual.transform.SetParent(bubble.transform);
			visual.transform.localPosition = Vector3.zero;
			visual.gameObject.AddComponent<BillboardRotator>().invertFace = true;

			bubbleVisual.transform.localPosition = Vector3.forward * 0.5f;

			bubble.renderer = bubbleVisual;
			bubble.gameObject.layer = LayerStorage.standardEntities;
			bubble.entity = bubble.gameObject.CreateEntity(1f, 4f, visual.transform);
			bubble.entity.SetGrounded(false);
			var canvas = ObjectCreationExtensions.CreateCanvas();
			canvas.transform.SetParent(bubble.transform);
			ObjectCreationExtensions.CreateImage(canvas, TextureExtensions.CreateSolidTexture(1, 1, new(0f, 0.5f, 0.5f, 0.35f)));
			bubble.bubbleCanvas = canvas;
			canvas.gameObject.SetActive(false);

			var bucketObj = ObjectCreationExtensions.CreateSpriteBillboard(BBTimesManager.man.Get<Sprite>("fieldTripBucket")) // FireFuel_Sheet_0 is bucket
				.AddSpriteHolder(out var bucketRenderer, 1.2f);
			bucketRenderer.name = "Bucket_Renderer";
			bucketObj.name = "Bucket";
			bucketObj.gameObject.ConvertToPrefab(true);
			bucketPre = bucketObj.gameObject.CreatePropagatedAudioManager(15f, 85f);

			bubPre = bubble;
		}
		public void SetupPrefabPost() =>
			audRefill = ((Mopliss)BBTimesManager.man.Get<NPC>("NPC_Mopliss")).audRefill; // Same refill noise


		const float pixs = 21f;

		public string Name { get; set; }
		public string Category => "npcs";

		public NPC Npc { get; set; }
		[SerializeField] Character[] replacementNPCs; public Character[] GetReplacementNPCs() => replacementNPCs; public void SetReplacementNPCs(params Character[] chars) => replacementNPCs = chars;
		public int ReplacementWeight { get; set; }



		// prefab ^^
		public override void Initialize()
		{
			base.Initialize();
			bubbleAmmo = bubbleMaxAmmo;
			map = new(ec, PathType.Nav, int.MaxValue, transform);
			navigator.maxSpeed = speed;
			navigator.SetSpeed(speed);

			// Make buckets
			var classrooms = new List<RoomController>();
			foreach (var room in ec.rooms)
			{
				if (acceptableClassroomCategories.Contains(room.category))
					classrooms.Add(room);
			}

			if (classrooms.Count == 0)
				classrooms.AddRange(ec.rooms);

			classrooms.Shuffle();

			for (int i = 0; i < 3 && i < classrooms.Count; i++)
			{
				var cell = classrooms[i].RandomEntitySafeCellNoGarbage();
				buckets.Add(Instantiate(bucketPre, cell.FloorWorldPosition, Quaternion.identity, ec.transform));
			}

			behaviorStateMachine.ChangeState(new Bubbly_Navigating(this));
		}

		internal Bubble SpitBubbleAtDirection(Vector3 dir)
		{
			var b = Instantiate(bubPre);
			bubbles.Add(b);

			for (int i = 0; i < bubbles.Count; i++)
				if (!bubbles[i])
					bubbles.RemoveAt(i--); // Bubble is null? Remove it!

			b.Spawn(ec, navigator.Entity, transform.position, dir, Random.Range(minBubbleSpeed, maxBubbleSpeed));
			StartCoroutine(FillupBubble(b));
			return b;
		}

		public override void Despawn()
		{
			base.Despawn();
			for (int i = 0; i < bubbles.Count; i++)
				bubbles[i]?.Pop();

			foreach (var bucket in buckets)
			{
				if (bucket)
					Destroy(bucket);
			}
		}

		IEnumerator FillupBubble(Bubble b)
		{
			audMan.PlaySingle(audFillUp);
			float scale = 0f;
			b.entity.SetFrozen(true);

			float speed = Random.Range(minSpeedToFillupBubble, maxSpeedToFillupBubble);
			while (true)
			{
				scale += (1.03f - scale) * speed * TimeScale * Time.deltaTime;
				if (scale >= 1f)
					break;
				b.renderer.transform.localScale = Vector3.one * scale;
				b.entity.Teleport(transform.position);
				yield return null;
			}
			b.renderer.transform.localScale = Vector3.one;
			b.entity.SetFrozen(false);

			b.Initialize();
			behaviorStateMachine.ChangeState(new Bubbly_Navigating(this));
			yield break;
		}

		internal void FindNewTarget()
		{
			// If there's any ammo
			if (bubbleAmmo > 0)
			{
				List<Cell> spotsToGo = [.. ec.mainHall.AllTilesNoGarbage(false, true)];

				for (int i = 0; i < spotsToGo.Count; i++)
					if (spotsToGo[i] == lastSpotGone || (spotsToGo[i].shape != TileShapeMask.Corner && spotsToGo[i].shape != TileShapeMask.Single)) // Filter to the ones that are corners or singles
						spotsToGo.RemoveAt(i--);

				if (spotsToGo.Count <= 1) // Just one spot is not valid.
				{
					lastSpotGone = null;
					TargetPosition(ec.mainHall.RandomEntitySafeCellNoGarbage().FloorWorldPosition);
					return;
				}
				lastSpotGone = spotsToGo[Random.Range(0, spotsToGo.Count)];
				TargetPosition(lastSpotGone.FloorWorldPosition);
				return;
			}

			// Out of ammo, find a bucket
			if (buckets.Count == 0)
			{
				// No buckets left, just wander
				TargetPosition(ec.mainHall.RandomEntitySafeCellNoGarbage().FloorWorldPosition);
				return;
			}

			// Find all buckets
			map.Calculate(int.MaxValue, true, [.. buckets.ConvertAll(b => IntVector2.GetGridPosition(b.transform.position))]);

			nextBucket = null;
			int closestDist = int.MaxValue;
			foreach (var bucket in buckets)
			{
				int dist = map.Value(IntVector2.GetGridPosition(bucket.transform.position));
				if (dist < closestDist)
				{
					closestDist = dist;
					nextBucket = bucket;
				}
			}
			if (nextBucket)
				TargetPosition(nextBucket.transform.position);
			else // Failsafe
				TargetPosition(ec.mainHall.RandomEntitySafeCellNoGarbage().FloorWorldPosition);
		}

		[SerializeField]
		internal float speed = 17f;

		[SerializeField]
		internal float minBubbleCooldown = 0.5f, maxBubbleCooldown = 1.5f, minSpeedToFillupBubble = 2.5f, maxSpeedToFillupBubble = 5.5f, minBubbleSpeed = 16f, maxBubbleSpeed = 22f;

		[SerializeField]
		internal int bubbleMaxAmmo = 3;

		[SerializeField]
		internal AudioManager bucketPre;

		[SerializeField]
		internal Bubble bubPre;

		[SerializeField]
		internal SpriteRenderer renderer;

		[SerializeField]
		internal Sprite[] sprWalkingAnim;

		[SerializeField]
		internal Sprite sprPrepareBub;

		[SerializeField]
		internal SoundObject audFillUp, audRefill;

		[SerializeField]
		internal PropagatedAudioManager audMan;

		public AudioManager NextBucket => nextBucket;

		internal int bubbleAmmo;
		DijkstraMap map;
		readonly List<Bubble> bubbles = [];
		readonly internal List<AudioManager> buckets = [];
		AudioManager nextBucket;
		readonly public static HashSet<RoomCategory> acceptableClassroomCategories = [RoomCategory.Class];
		Cell lastSpotGone = null;
	}

	internal class Bubbly_StateBase(Bubbly bub) : NpcState(bub)
	{
		protected Bubbly bub = bub;
	}

	internal class Bubbly_WalkingStateBase(Bubbly bub) : Bubbly_StateBase(bub)
	{
		float frame = 0f;

		public override void Update()
		{
			base.Update();
			frame += bub.TimeScale * Time.deltaTime * 8.5f;
			frame %= bub.sprWalkingAnim.Length;
			bub.renderer.sprite = bub.sprWalkingAnim[Mathf.FloorToInt(frame)];
		}
	}

	internal class Bubbly_Navigating(Bubbly bub) : Bubbly_WalkingStateBase(bub)
	{
		public override void Enter()
		{
			base.Enter();
			bub.FindNewTarget();
		}

		public override void DestinationEmpty()
		{
			base.DestinationEmpty();
			if (bub.bubbleAmmo > 0)
			{
				bub.behaviorStateMachine.ChangeState(new Bubbly_SpawnBubble(bub));
			}
			else
			{
				// Reached a bucket
				if (bub.NextBucket)
				{
					bub.NextBucket.PlaySingle(bub.audRefill);
					bub.bubbleAmmo = bub.bubbleMaxAmmo;
				}
				// Find a new target immediately
				bub.behaviorStateMachine.ChangeState(new Bubbly_Navigating(bub));
			}
		}
	}

	internal class Bubbly_SpawnBubble(Bubbly bub) : Bubbly_StateBase(bub)
	{
		public override void Enter()
		{
			base.Enter();
			bub.renderer.sprite = bub.sprPrepareBub;
			var pos = bub.transform.position;
			ChangeNavigationState(new NavigationState_DoNothing(bub, 0));

			var dirsToSpit = new List<Vector3>();
			Vector3 direction = Direction.North.ToVector3();
			var cell = bub.ec.CellFromPosition(pos);
			var room = cell.room;

			if (cell.open)
			{
				for (int i = 0; i < 8; i++)
				{
					if (bub.ec.CellFromPosition(pos + (direction * 10f)).TileMatches(room))
						dirsToSpit.Add(direction);
					direction = Quaternion.AngleAxis(45, Vector3.up) * direction;
				}
			}
			else
			{
				for (int i = 0; i < 4; i++)
				{
					if (bub.ec.CellFromPosition(pos + (direction * 10f)).TileMatches(room))
						dirsToSpit.Add(direction);
					direction = Quaternion.AngleAxis(90, Vector3.up) * direction;
				}
			}

			if (dirsToSpit.Count > 0)
			{
				int i = Random.Range(0, dirsToSpit.Count);
				bub.SpitBubbleAtDirection(dirsToSpit[i]);
				bub.bubbleAmmo--;
			}
		}
	}
}
