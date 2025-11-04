using System.Collections;
using System.Collections.Generic;
using BBTimes.CustomComponents;
using BBTimes.CustomComponents.NpcSpecificComponents;
using BBTimes.Extensions;
using BBTimes.Plugin;
using MTM101BaldAPI;
using MTM101BaldAPI.Components;
using PixelInternalAPI.Classes;
using PixelInternalAPI.Extensions;
using UnityEngine;

namespace BBTimes.CustomContent.NPCs
{
	public class Watcher : NPC, INPCPrefab
	{
		public void SetupPrefab()
		{
			Color
			normalColor = new(0.6f, 0.6f, 0.6f),
			shadowColor = new(0.45f, 0.45f, 0.45f);

			SoundObject[] soundObjects = [this.GetSound("WCH_ambience.wav", "Vfx_Wch_Idle", SoundType.Effect, normalColor),
		this.GetSoundNoSub("WCH_see.wav", SoundType.Effect),
		this.GetSound("WCH_teleport.wav", "Vfx_Wch_Teleport", SoundType.Effect, normalColor),
		this.GetSound("WCH_angered.wav", "Vfx_Wch_Angered", SoundType.Effect, normalColor),
		this.GetSound("SHDWCH_spawn.wav", "Vfx_Wch_Spawn", SoundType.Effect, shadowColor),
		this.GetSound("SHDWCH_ambience.wav", "Vfx_Wch_Idle", SoundType.Effect, shadowColor)
			];
			var storedSprites = this.GetSpriteSheet(3, 1, 30f, "watcher.png");
			spriteRenderer[0].sprite = storedSprites[0];
			normalSprite = storedSprites[0];
			angrySprite = storedSprites[1];

			audMan = GetComponent<PropagatedAudioManager>();
			screenAudMan = gameObject.CreateAudioManager(35f, 60f).MakeAudioManagerNonPositional();

			audAmbience = soundObjects[0];
			audSpot = soundObjects[1];
			audTeleport = soundObjects[2];
			audAngered = soundObjects[3];

			spriteToHide = spriteRenderer[0];

			// Hallucination Setup
			var hallObj = ObjectCreationExtensions.CreateSpriteBillboard(storedSprites[2]).AddSpriteHolder(out var hallRender, 0f, LayerStorage.ignoreRaycast);
			hallRender.gameObject.layer = LayerMask.NameToLayer("Overlay");
			hallObj.name = "WatcherHallucination";
			hallObj.gameObject.ConvertToPrefab(true);

			var col = hallObj.gameObject.AddComponent<CapsuleCollider>();
			col.isTrigger = true;
			col.radius = 2f;
			col.height = 10f;
			col.center = new Vector3(0f, 5f, 0f);

			var hall = hallObj.gameObject.AddComponent<Hallucinations>();
			hall.audMan = hall.gameObject.CreateAudioManager(15f, 25f);
			hall.audSpawn = soundObjects[4];
			hall.audLoop = soundObjects[5];
			hall.renderer = hallRender;

			hall.nav = hallObj.gameObject.AddComponent<MomentumNavigator>();
			hall.nav.pathType = PathType.Const; // It ignores obstacles, it's literally a hallucination
			hall.nav.maxSpeed = 20f;
			hall.nav.accel = 2.5f;
			hall.nav.useAcceleration = true;

			hallPre = hall;

			Navigator.accel = 20f;
			gaugeSprite = this.GetSprite(Storage.GaugeSprite_PixelsPerUnit, "gaugeIcon.png");
		}
		public void SetupPrefabPost() { }
		public string Name { get; set; }
		public string Category => "npcs";

		public NPC Npc { get; set; }
		[SerializeField] Character[] replacementNPCs; public Character[] GetReplacementNPCs() => replacementNPCs; public void SetReplacementNPCs(params Character[] chars) => replacementNPCs = chars;
		public int ReplacementWeight { get; set; }
		// --------------------------------------------------
		public override void Initialize()
		{
			base.Initialize();
			audMan.maintainLoop = true;

			behaviorStateMachine.ChangeState(new Watcher_Inactive(this));
		}

		public override void Despawn()
		{
			base.Despawn();
			DespawnHallucinations(true);
			ec.RemoveFog(obscurityDebuff);
			gauge?.Deactivate();
		}

		public void DespawnHallucinations(bool instaDespawn)
		{
			while (hallucinations.Count != 0)
			{
				if (hallucinations[0])
				{
					if (instaDespawn)
						hallucinations[0].InstantDespawn();
					else
						hallucinations[0].Despawn();
				}
				hallucinations.RemoveAt(0);
			}
		}

		public override void VirtualUpdate()
		{
			base.VirtualUpdate();
			for (int i = 0; i < hallucinations.Count; i++)
			{
				if (!hallucinations[i]) // remove any destroyed hallucination
					hallucinations.RemoveAt(i--);
			}

			obscurityDebuff.strength += (currentFogStrength - obscurityDebuff.strength) / fogStrengthSmoothness * Time.deltaTime * TimeScale * 25f;
			if (hasFog)
			{
				ec.UpdateFog();
				if (obscurityDebuff.strength.CompareFloats(0f))
				{
					hasFog = false;
					ec.RemoveFog(obscurityDebuff);
				}
			}
		}

		internal void TeleportAwayFromPlayer(PlayerManager pm)
		{
			var map = pm.DijkstraMap;
			bool storeCell = map.storeFoundCells;
			map.StoreFoundCells = true;
			map.Calculate();
			map.StoreFoundCells = storeCell;

			var cam = Singleton<CoreGameManager>.Instance.GetCamera(pm.playerNumber);
			var validCells = new List<Cell>();

			int maxDist = 0;
			foreach (var cell in ec.AllCells())
			{
				if (cell.room.type == RoomType.Hall && !cell.HasAnyHardCoverage)
				{
					int dist = map.Value(cell.position);
					if (dist > maxDist)
						maxDist = dist;
				}
			}

			float fov = cam.camCom.fieldOfView;
			Vector3 camPos = cam.transform.position;
			Vector3 camFwd = cam.transform.forward;

			foreach (var cell in ec.AllCells())
			{
				if (cell.room.type == RoomType.Hall && !cell.HasAnyHardCoverage)
				{
					int dist = map.Value(cell.position);
					Vector3 targetDir = cell.CenterWorldPosition - camPos;
					float angle = Vector3.Angle(camFwd, targetDir);

					// Far away AND outside player's Field of View.
					if (dist > maxDist * 0.75f && angle > fov * 0.5f)
					{
						validCells.Add(cell);
					}
				}
			}

			if (validCells.Count > 0)
			{
				Cell targetCell = validCells[Random.Range(0, validCells.Count)];
				navigator.Entity.Teleport(targetCell.CenterWorldPosition);
			}
			else // Failsafe if no ideal spot is found
			{
				navigator.Entity.Teleport(ec.RandomCell(false, false, true).CenterWorldPosition);
			}
		}

		public void CreateOrIncreaseFog()
		{
			hasFog = true;
			ec.AddFog(obscurityDebuff);
			currentFogStrength = Mathf.Min(currentFogStrength + fogIncrement, fogStrengthLimit);
		}

		public void DecrementFog()
		{
			currentFogStrength -= fogIncrement;
			if (currentFogStrength < 0)
				currentFogStrength = 0;
		}

		public void SetTimeToWatcherEffect(float setTime)
		{
			if (behaviorStateMachine.CurrentState is Watcher_Dead wDead)
				wDead.DeadCooldown = setTime;
		}

		public void AngryHide()
		{
			spriteToHide.sprite = angrySprite;
			StartCoroutine(GradualHide());
		}

		public void Hide(bool hide)
		{
			spriteToHide.enabled = !hide;
			for (int i = 0; i < baseTrigger.Length; i++)
				baseTrigger[i].enabled = !hide;

			if (hide)
			{
				audMan.FlushQueue(true);
				return;
			}
			audMan.SetLoop(true);
			audMan.QueueAudio(audAmbience);
		}

		public void SetFrozen(bool freeze)
		{
			if (freeze)
			{
				if (!navigator.Entity.ExternalActivity.moveMods.Contains(moveMod))
					navigator.Entity.ExternalActivity.moveMods.Add(moveMod);
			}
			else
				navigator.Entity.ExternalActivity.moveMods.Remove(moveMod);

		}

		public void SpawnHallucinations(PlayerManager pm) =>
			StartCoroutine(HallucinationSpawner(pm));

		IEnumerator HallucinationSpawner(PlayerManager pm)
		{
			int halls = Random.Range(minHallucinations, maxHallucinations);
			for (int i = 0; i < halls; i++)
			{
				var hal = Instantiate(hallPre);
				hal.AttachToPlayer(pm, this);
				hallucinations.Add(hal);
				yield return new WaitForSecondsNPCTimescale(this, Random.Range(0.2f, 0.8f));
			}
		}

		IEnumerator GradualHide()
		{
			Color alpha = spriteToHide.color;
			while (alpha.a > 0f)
			{
				alpha.a -= ec.EnvironmentTimeScale * Time.deltaTime * 3f;
				spriteToHide.color = alpha;
				yield return null;
			}
			behaviorStateMachine.ChangeState(new Watcher_Dead(this, true));
			spriteToHide.color = Color.white;
		}


		[SerializeField]
		internal SpriteRenderer spriteToHide;

		[SerializeField]
		internal AudioManager audMan, screenAudMan;

		[SerializeField]
		internal SoundObject audAmbience, audSpot, audTeleport, audAngered;

		[SerializeField]
		internal Hallucinations hallPre;

		[SerializeField]
		internal Sprite angrySprite, normalSprite;

		[SerializeField]
		internal int minHallucinations = 7, maxHallucinations = 9;

		[SerializeField]
		internal float runSpeed = 125f, sustainedStareDuration = 1.5f, glimpzeLimit = 0.15f, minActiveCooldown = 30f, maxActiveCooldown = 60f, deadCooldown = 20f, fogIncrement = 0.125f, fogStrengthLimit = 1f, fogStrengthSmoothness = 2.25f;

		[SerializeField]
		internal Sprite gaugeSprite;

		readonly List<Hallucinations> hallucinations = [];
		internal HudGauge gauge;

		readonly MovementModifier moveMod = new(Vector3.zero, 0f);

		readonly internal Fog obscurityDebuff = new()
		{
			color = Color.black,
			startDist = 5f,
			maxDist = 17.5f,
			strength = 0f,
			priority = 1
		};
		float currentFogStrength = 0f;
		bool hasFog = false;
	}

	internal class Watcher_StateBase(Watcher w) : NpcState(w)
	{
		protected Watcher w = w;
	}

	internal class Watcher_Inactive(Watcher w) : Watcher_StateBase(w)
	{
		float glimpseTime = 0f;
		PlayerManager sightedPlayer = null;
		bool glimpzed = false;

		public override void Enter()
		{
			base.Enter();
			w.TeleportAwayFromPlayer(Singleton<CoreGameManager>.Instance.GetPlayer(0)); // Just use the default player
			w.spriteToHide.sprite = w.normalSprite;
			w.spriteToHide.enabled = true; // frozen
			w.SetFrozen(true);
			w.audMan.FlushQueue(true); // no ambient sound
			ChangeNavigationState(new NavigationState_DoNothing(w, 0));
		}

		public override void Update()
		{
			base.Update();
			if (glimpzed)
			{
				glimpseTime += w.TimeScale * Time.deltaTime;
				if (glimpseTime >= w.glimpzeLimit) // Player has glimpsed for 0.15s
					w.behaviorStateMachine.ChangeState(new Watcher_Teleporting(w, sightedPlayer));
			}
		}

		public override void InPlayerSight(PlayerManager player)
		{
			base.InPlayerSight(player);
			if (glimpzed)
			{
				sightedPlayer = player;
				glimpzed = true;
			}
		}
	}

	internal class Watcher_Teleporting(Watcher w, PlayerManager pm) : Watcher_StateBase(w)
	{
		public override void Enter()
		{
			base.Enter();
			w.TeleportAwayFromPlayer(pm);
			w.behaviorStateMachine.ChangeState(new Watcher_Active(w));
		}
	}

	internal class Watcher_Active(Watcher w) : Watcher_StateBase(w)
	{
		PlayerManager staredPlayer = null;

		public override void Enter()
		{
			base.Enter();
			w.spriteToHide.sprite = w.angrySprite;
			w.Hide(false);
			w.SetFrozen(false);
			ChangeNavigationState(new NavigationState_DoNothing(w, 0));

		}

		public override void InPlayerSight(PlayerManager player)
		{
			base.InPlayerSight(player);

			if (!moveMods.TryGetValue(player, out var moveMod))
				return;

			staredPlayer = player;
			spotStrength += w.TimeScale * Time.deltaTime * 6.5f;
			if (Time.timeScale > 0)
				mod.addend = spotStrength * (-1f + Random.value * 2f) * 2f;
			Vector3 distance = w.transform.position - player.transform.position;
			moveMod.movementAddend = distance.normalized * Mathf.Min(15f, distance.magnitude * 0.6f);

			player.transform.RotateSmoothlyToNextPoint(w.transform.position, 0.35f);
			if (spotStrength > strengthLimit)
			{
				player.GetCustomCam().RemoveModifier(mod);
				w.behaviorStateMachine.ChangeState(new Watcher_Attack(w, player));
			}
		}

		public override void PlayerSighted(PlayerManager player)
		{
			base.PlayerSighted(player);
			if (moveMods.ContainsKey(player))
				return;

			var moveMod = new MovementModifier(Vector3.zero, 1f);
			player.Am.moveMods.Add(moveMod);
			moveMods.Add(player, moveMod);

			mod.addend = 0;
			player.GetCustomCam().AddModifier(mod);
		}

		public override void PlayerLost(PlayerManager player)
		{
			base.PlayerLost(player);
			staredPlayer = null;
		}

		public override void Sighted()
		{
			base.Sighted();
			if (!hasPlayed)
			{
				hasPlayed = true;

				w.screenAudMan.SetLoop(true);
				w.screenAudMan.QueueAudio(w.audSpot);
			}
			w.screenAudMan.Pause(false);
			stillInSight = true;
		}

		public override void Unsighted()
		{
			base.Unsighted();
			w.screenAudMan.Pause(true);
			mod.addend = 0;
			stillInSight = false;
			if (staredPlayer && moveMods.TryGetValue(staredPlayer, out var mmod))
				mmod.movementAddend = Vector3.zero;
		}

		public override void Update()
		{
			base.Update();
			if (stillInSight) return;

			leaveCooldown -= w.TimeScale * Time.deltaTime;
			if (leaveCooldown <= 0f)
				w.behaviorStateMachine.ChangeState(new Watcher_Dead(w, false));
		}

		public override void Exit()
		{
			base.Exit();
			foreach (var move in moveMods)
				move.Key?.Am.moveMods.Remove(move.Value);
		}

		bool hasPlayed = false, stillInSight = false;


		float spotStrength = 0f, leaveCooldown = Random.Range(w.minActiveCooldown, w.maxActiveCooldown);
		const float strengthLimit = 12f;
		readonly ValueModifier mod = new();

		readonly Dictionary<PlayerManager, MovementModifier> moveMods = [];

	}

	internal class Watcher_Attack(Watcher w, PlayerManager pm) : Watcher_StateBase(w)
	{
		readonly NavigationState_TargetPlayer targetState = new(w, 63, pm.transform.position);

		public override void Enter()
		{
			base.Enter();
			w.Navigator.maxSpeed = w.runSpeed;
			w.Navigator.SetSpeed(0f); // Go by acceleration

			w.screenAudMan.FlushQueue(true);
			w.screenAudMan.SetLoop(true);
			w.screenAudMan.maintainLoop = true;
			w.screenAudMan.QueueAudio(w.audAngered);

			ChangeNavigationState(targetState);
		}
		public override void PlayerInSight(PlayerManager player)
		{
			base.PlayerInSight(player);
			if (player == pm)
				targetState.UpdatePosition(player.transform.position);
		}
		public override void OnStateTriggerEnter(Collider other, bool validCollision)
		{
			base.OnStateTriggerEnter(other, validCollision);
			if (validCollision && other.CompareTag("Player") && other.gameObject == pm.gameObject)
			{
				// summon hallucinations
				w.behaviorStateMachine.ChangeState(new Watcher_Summoning(w, pm));
			}
		}

		public override void Exit()
		{
			base.Exit();
			targetState.priority = 0;
			w.screenAudMan.maintainLoop = false;
			w.screenAudMan.FlushQueue(true);
		}
	}

	internal class Watcher_Summoning(Watcher w, PlayerManager pm) : Watcher_StateBase(w)
	{
		readonly PlayerManager pm = pm;
		public override void Enter()
		{
			base.Enter();
			ChangeNavigationState(new NavigationState_DoNothing(w, 0));
			w.SpawnHallucinations(pm);
			w.AngryHide();
		}
	}

	internal class Watcher_Dead(Watcher w, bool activateGauge) : Watcher_StateBase(w)
	{
		readonly bool hasGauge = activateGauge;
		public override void Enter()
		{
			base.Enter();
			w.Hide(true);
			w.SetFrozen(true);
			ChangeNavigationState(new NavigationState_DoNothing(w, 0));
			if (hasGauge)
				w.gauge = Singleton<CoreGameManager>.Instance.GetHud(0).gaugeManager.ActivateNewGauge(w.gaugeSprite, deadCooldown);
		}

		public override void Update()
		{
			base.Update();
			if (hasGauge)
				w.gauge?.SetValue(maxCooldown, deadCooldown);

			deadCooldown -= w.TimeScale * Time.deltaTime;
			if (deadCooldown <= 0f)
				w.behaviorStateMachine.ChangeState(new Watcher_Inactive(w));
		}

		public override void Exit()
		{
			base.Exit();
			if (hasGauge)
				w.gauge?.Deactivate();
		}

		float deadCooldown = w.deadCooldown, maxCooldown = w.deadCooldown;
		public float DeadCooldown
		{
			get => deadCooldown;
			set
			{
				deadCooldown = value;
				maxCooldown = Mathf.Max(maxCooldown, deadCooldown);
			}
		}
	}
}