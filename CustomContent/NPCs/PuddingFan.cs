using System.Collections.Generic;
using System.Linq;
using BBTimes.CustomComponents;
using BBTimes.CustomContent.RoomFunctions;
using BBTimes.Extensions;
using BBTimes.Extensions.ObjectCreationExtensions;
using BBTimes.Manager;
using PixelInternalAPI.Extensions;
using UnityEngine;

namespace BBTimes.CustomContent.NPCs
{
	public class PuddingFan : NPC, INPCPrefab, IItemAcceptor, ISlipperOwner
	{
		public void SetupPrefab()
		{
			audMan = GetComponent<AudioManager>();
			audActive = this.GetSound("acRunning.wav", "Vfx_PuddingFan_Blow", SoundType.Effect, Color.white);

			var sprs = this.GetSpriteSheet(4, 2, 25f, "puddingFan.png");
			spriteRenderer[0].CreateAnimatedSpriteRotator(
				[new() { angleCount = 8, spriteSheet = sprs }]
				);
			spriteRenderer[0].sprite = sprs[0];

			var system = GameExtensions.GetNewParticleSystem();
			system.name = "FanParticles";
			system.transform.SetParent(transform);
			system.transform.localPosition = Vector3.forward * 1.25f;
			system.GetComponent<ParticleSystemRenderer>().material = new Material(ObjectCreationExtension.defaultDustMaterial) { mainTexture = this.GetTexture("puddingParticles.png") };

			var main = system.main;
			main.gravityModifierMultiplier = 0.05f;
			main.startLifetimeMultiplier = 1.8f;
			main.startSpeedMultiplier = 2f;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.startSize = new(0.5f, 1f);

			var emission = system.emission;
			emission.rateOverTimeMultiplier = 100f;
			emission.enabled = false;

			var vel = system.velocityOverLifetime;
			vel.enabled = true;
			vel.space = ParticleSystemSimulationSpace.Local;
			vel.x = new(-35f, 35f);
			vel.y = new(-4.5f, 4.5f);
			vel.z = new(25f, 65f);

			var an = system.textureSheetAnimation;
			an.enabled = true;
			an.numTilesX = 2;
			an.numTilesY = 2;
			an.animation = ParticleSystemAnimationType.WholeSheet;
			an.fps = 0f;
			an.timeMode = ParticleSystemAnimationTimeMode.FPS;
			an.cycleCount = 1;
			an.startFrame = new(0, 3); // 2x2

			var col = system.collision;
			col.enabled = true;
			col.type = ParticleSystemCollisionType.World;
			col.enableDynamicColliders = false;

			parts = system;

			SlipperController.CreateSlipperPackPrefab(this, this.GetSprite(12.799f, "Pudding.png"));
		}
		public void SetupPrefabPost() { }
		public string Name { get; set; }
		public string Category => "npcs";

		public NPC Npc { get; set; }
		[SerializeField] Character[] replacementNPCs; public Character[] GetReplacementNPCs() => replacementNPCs; public void SetReplacementNPCs(params Character[] chars) => replacementNPCs = chars;
		public int ReplacementWeight { get; set; }
		// --------------------------------------------------

		[SerializeField]
		internal AudioManager audMan;

		[SerializeField]
		internal SoundObject audActive;

		[SerializeField]
		internal ParticleSystem parts;

		[SerializeField]
		internal float minActive = 30f, maxActive = 30f; // Set to 30 seconds
		[SerializeField]
		internal Slipper slipperPre;
		[SerializeField]
		internal SlipperEffector slipperEffectorPre;

		// ISlipperOwner
		Slipper ISlipperOwner.slipperPre { get => slipperPre; set => slipperPre = value; }
		SlipperEffector ISlipperOwner.slipperEffectorPre { get => slipperEffectorPre; set => slipperEffectorPre = value; }
		EnvironmentController ISlipperOwner.ec => ec;
		GameObject ISlipperOwner.gameObject => gameObject;

		public override void Initialize()
		{
			base.Initialize();
			foreach (var room in ec.rooms)
			{
				if (room.type == RoomType.Room && room.category != RoomCategory.Special)
					cells.AddRange(room.AllEntitySafeCellsNoGarbage().Where(x => x.open && !x.HasAnyHardCoverage && x.shape.HasFlag(TileShapeMask.Corner)));
			}

			if (cells.Count == 0)
			{
				Debug.LogWarning("Pudding Fan has failed to find any good spot!");
				behaviorStateMachine.ChangeNavigationState(new NavigationState_DoNothing(this, 0));
				behaviorStateMachine.ChangeState(new NpcState(this));
				return;
			}

			behaviorStateMachine.ChangeState(new PuddingFan_GoToRoom(this));
		}

		public override void Despawn()
		{
			base.Despawn();
			RemoveFuncIfExists();
		}

		public void RollingOn()
		{
			audMan.FlushQueue(true);

			navigator.maxSpeed = 24.5f;
			navigator.SetSpeed(24.5f);
			nextPos = zero;

			var em = parts.emission;
			em.enabled = false;

			RemoveFuncIfExists();
		}

		public void ActivateAirConditioner(RoomController room)
		{
			behaviorStateMachine.ChangeNavigationState(new NavigationState_DoNothing(this, 0));
			navigator.maxSpeed = 0f;
			navigator.SetSpeed(0f);

			var em = parts.emission;
			em.enabled = true;

			audMan.FlushQueue(true);
			audMan.maintainLoop = true;
			audMan.SetLoop(true);
			audMan.QueueAudio(audActive);

			RemoveFuncIfExists();
			lastCreatedFunction = room.functionObject.AddComponent<FreezingRoomFunction>();
			lastCreatedFunction.AssignImmunityToEntity(Navigator.Entity);
			lastCreatedFunction.owner = this;
			room.functions.AddFunction(lastCreatedFunction);
			lastCreatedFunction.Initialize(room);

			var cell = ec.CellFromPosition(transform.position);
			if (!cell.shape.HasFlag(TileShapeMask.Corner)) return;

			Vector3 fwd = new();
			cell.AllWallDirections.ForEach(x => fwd += x.GetOpposite().ToVector3());

			nextPos = transform.position + fwd.normalized;
		}

		void RemoveFuncIfExists()
		{
			if (lastCreatedFunction)
			{
				var room = lastCreatedFunction.Room;
				var driedPudding = room.functionObject.AddComponent<DriedPuddingRoomFunction>();
				room.functions.AddFunction(driedPudding);
				driedPudding.slipMatPre = slipperPre.gameObject;
				driedPudding.Initialize(room);

				lastCreatedFunction.Room.functions.RemoveFunction(lastCreatedFunction);
				Destroy(lastCreatedFunction);
				lastCreatedFunction = null;
			}
		}

		public override void VirtualUpdate()
		{
			base.VirtualUpdate();
			if (nextPos != zero)
				transform.RotateSmoothlyToNextPoint(nextPos, 1f);
		}

		public bool ItemFits(Items itm) =>
			behaviorStateMachine.CurrentState is PuddingFan_Activate && disablingItems.Contains(itm);

		public void InsertItem(PlayerManager pm, EnvironmentController ec) =>
			behaviorStateMachine.ChangeState(new PuddingFan_GoToRoom(this));

		Vector3 nextPos;
		readonly Vector3 zero = Vector3.zero;

		FreezingRoomFunction lastCreatedFunction;
		public Cell GetRandomSpotToGo
		{
			get
			{
				var player = Singleton<CoreGameManager>.Instance.GetPlayer(0);
				if (player == null)
					return cells[Random.Range(0, cells.Count)];

				var myRoom = ec.CellFromPosition(transform.position).room;
				var playerRoom = player.plm.Entity.CurrentRoom;

				List<RoomController> potentialRooms = [..
				cells.Select(x => x.room).Distinct()
					.Where(r => r != myRoom && r != playerRoom)
					.OrderBy(r => Vector3.Distance(ec.RealRoomMid(r), player.transform.position))];

				if (potentialRooms.Count == 0) // Fallback if no other rooms available
				{
					potentialRooms = [.. cells.Select(x => x.room).Distinct()
						.Where(r => r != myRoom)
						.OrderBy(r => Vector3.Distance(ec.RealRoomMid(r), player.transform.position))];
				}

				if (potentialRooms.Count == 0) // Ultimate fallback
				{
					Debug.LogWarning("Pudding Fan couldn't find ANY valid room to go to!");
					return cells[Random.Range(0, cells.Count)];
				}

				var targetRoom = potentialRooms.First();
				List<Cell> spotsInRoom = [.. cells.Where(c => c.room == targetRoom)];
				return spotsInRoom[Random.Range(0, spotsInRoom.Count)];
			}
		}

		public float ActiveCooldown => Random.Range(minActive, maxActive);
		readonly List<Cell> cells = [];

		readonly static HashSet<Items> disablingItems = [Items.Scissors];
		public static void AddDisablingItem(Items item) => disablingItems.Add(item);

	}

	internal class PuddingFan_StateBase(PuddingFan pud) : NpcState(pud)
	{
		protected PuddingFan pud = pud;
	}

	internal class PuddingFan_GoToRoom(PuddingFan pud) : PuddingFan_StateBase(pud)
	{
		NavigationState_TargetPosition spotGo;
		Cell spot = pud.GetRandomSpotToGo;
		public override void Enter()
		{
			base.Enter();
			pud.RollingOn();
			spotGo = new(pud, 64, spot.FloorWorldPosition);
			ChangeNavigationState(spotGo);
		}

		public override void DestinationEmpty()
		{
			base.DestinationEmpty();
			if (pud.ec.CellFromPosition(pud.transform.position) != spot) // Could only happen if Jerry was interrupted or if the target cell isn't available anymore
			{
				spot = pud.GetRandomSpotToGo; // Change spots
				spotGo.UpdatePosition(spot.FloorWorldPosition);
				ChangeNavigationState(spotGo); // Get to a new path again
			}
			else
				pud.behaviorStateMachine.ChangeState(new PuddingFan_Activate(pud, spot.room));
		}
		public override void Exit()
		{
			base.Exit();
			spotGo.priority = 0;
		}
	}

	internal class PuddingFan_Activate(PuddingFan pud, RoomController room) : PuddingFan_StateBase(pud)
	{
		float activeCooldown = pud.ActiveCooldown;
		Vector3 pos;
		public override void Enter()
		{
			base.Enter();
			pud.ActivateAirConditioner(room);
			pos = pud.transform.position;
		}
		public override void Update()
		{
			base.Update();
			activeCooldown -= pud.TimeScale * Time.deltaTime;
			if (activeCooldown <= 0f || pud.transform.position != pos)
				pud.behaviorStateMachine.ChangeState(new PuddingFan_GoToRoom(pud));
		}
	}
}