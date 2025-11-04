using System.Collections;
using System.Collections.Generic;
using BBTimes.CustomComponents;
using BBTimes.Extensions;
using BBTimes.Extensions.ObjectCreationExtensions;
using BBTimes.Plugin;
using UnityEngine;

namespace BBTimes.CustomContent.NPCs
{
	public class HappyHolidays : NPC, INPCPrefab, IClickable<int>, IItemAcceptor
	{
		public void SetupPrefab()
		{
			audMan = GetComponent<AudioManager>();
			var sprites = this.GetSpriteSheet(2, 2, 45f, "happyholidays.png");
			spriteRenderer[0].sprite = sprites[0];
			renderer = spriteRenderer[0];
			unwrapSprites = sprites;

			audHappyHolidays = this.GetSound("HappyHolidays.wav", "Vfx_HapH_MerryChristmas", SoundType.Voice, new(0.796875f, 0f, 0f));
			audUnbox = this.GetSound("unbox.wav", "Vfx_HapH_Unwrap", SoundType.Effect, new(0.796875f, 0f, 0f));

			var myCol = (CapsuleCollider)baseTrigger[0];
			var col = this.CreateClickableLink().gameObject.AddComponent<CapsuleCollider>();
			col.isTrigger = true;
			col.height = myCol.height;
			col.direction = myCol.direction;
			col.radius = myCol.radius;

			gaugeSprite = this.GetSprite(Storage.GaugeSprite_PixelsPerUnit, "gaugeIcon.png");
		}
		public void SetupPrefabPost() =>
			objects = [.. GameExtensions.GetAllShoppingItems()];
		public string Name { get; set; }
		public string Category => "npcs";

		public NPC Npc { get; set; }
		[SerializeField] Character[] replacementNPCs; public Character[] GetReplacementNPCs() => replacementNPCs; public void SetReplacementNPCs(params Character[] chars) => replacementNPCs = chars;
		public int ReplacementWeight { get; set; }
		// --------------------------------------------------

		public override void Initialize()
		{
			base.Initialize();
			behaviorStateMachine.ChangeState(new HappyHolidays_Wondering(this));
		}

		internal void GivePlayerItem(PlayerManager pm)
		{
			pm.itm.AddItem(objects[Random.Range(0, objects.Length)]);
			pm.RuleBreak(guiltRule, guiltTimeOnGift, 0.25f);
			if (gauge)
				gauge.Deactivate();
			gauge = Singleton<CoreGameManager>.Instance.GetHud(pm.playerNumber).gaugeManager.ActivateNewGauge(gaugeSprite, guiltTimeOnGift);
			StartCoroutine(GuiltTimer(pm));

			behaviorStateMachine.ChangeState(new HappyHolidays_WaitToRespawn(this));
			audMan.PlaySingle(audHappyHolidays);
		}

		public bool ItemFits(Items itm) =>
			!IsDisabled && cutters.Contains(itm);

		public void InsertItem(PlayerManager pm, EnvironmentController ec)
		{
			unwraps = unwrapSprites.Length;
			clickDelay = 0f;
			StartCoroutine(InsertItemDelay(pm));
		}

		IEnumerator InsertItemDelay(PlayerManager pm)
		{
			yield return null;
			Clicked(pm.playerNumber); // To prevent adding the item too soon and being overriden by the ItemManager
		}

		public void Clicked(int player)
		{
			if (clickDelay > 0f || IsDisabled) return;
			clickDelay += maxClickDelay;
			audMan.PlaySingle(audUnbox);

			if (++unwraps >= unwrapSprites.Length)
			{
				unwraps = 0;
				GivePlayerItem(Singleton<CoreGameManager>.Instance.GetPlayer(player));
				return;
			}
			renderer.sprite = unwrapSprites[unwraps];

			behaviorStateMachine.ChangeState(
				new HappyHolidays_FleeFromPlayer(this, (HappyHolidays_StateBase)behaviorStateMachine.CurrentState, Singleton<CoreGameManager>.Instance.GetPlayer(player).transform));

		}
		public void ClickableSighted(int player) { }
		public void ClickableUnsighted(int player) { }
		public bool ClickableHidden() => IsDisabled || clickDelay > 0f;
		public bool ClickableRequiresNormalHeight() => true;

		public void ResetSprite() => renderer.sprite = unwrapSprites[0];
		public override void VirtualUpdate()
		{
			base.VirtualUpdate();
			if (clickDelay > 0f)
				clickDelay -= TimeScale * Time.deltaTime;
		}
		public void MarkAsDisabled(bool disable)
		{
			if (disable)
				hhDisables++;
			else
				hhDisables--;
			if (hhDisables < 0)
				hhDisables = 0;
		}

		IEnumerator GuiltTimer(PlayerManager pm)
		{
			float time = guiltTimeOnGift;
			while (time > 0f && pm.ruleBreak == guiltRule)
			{
				time -= Time.deltaTime * pm.PlayerTimeScale;
				if (gauge)
					gauge.SetValue(guiltTimeOnGift, time);
				yield return null;
			}
			if (gauge)
				gauge.Deactivate();
		}

		[SerializeField]
		internal ItemObject[] objects = [];

		[SerializeField]
		internal SoundObject audHappyHolidays, audUnbox;

		[SerializeField]
		internal SpriteRenderer renderer;

		[SerializeField]
		internal Sprite[] unwrapSprites;
		[SerializeField]

		internal float maxClickDelay = 1.25f, normSpeed = 14.5f, maxRunSpeed = 35f, fleeTime = 12f, despawnHeight = -15f, respawnCooldown = 50f, aboutToRespawnCooldown = 5f;
		[SerializeField]
		internal float guiltTimeOnGift = 15f;
		[SerializeField]
		internal string guiltRule = "Bullying";

		[SerializeField]
		internal AudioManager audMan;

		HudGauge gauge;

		[SerializeField]
		internal Sprite gaugeSprite;

		int unwraps = 0;
		float clickDelay = 0;
		int hhDisables = 0;
		bool IsDisabled => hhDisables > 0;

		public static void AddItemThatCanCut(Items itm) => cutters.Add(itm);

		readonly static HashSet<Items> cutters = [Items.Scissors];
	}

	internal class HappyHolidays_StateBase(HappyHolidays hh) : NpcState(hh) // A default npc state
	{
		protected HappyHolidays hh = hh;
	}

	internal class HappyHolidays_Wondering(HappyHolidays hh) : HappyHolidays_StateBase(hh)
	{
		public override void Enter()
		{
			base.Enter();
			ChangeNavigationState(new NavigationState_WanderRandom(hh, 0));
			hh.Navigator.maxSpeed = hh.normSpeed;
			hh.Navigator.SetSpeed(hh.normSpeed);
		}
	}

	internal class HappyHolidays_FleeFromPlayer(HappyHolidays hh, HappyHolidays_StateBase prevState, params Transform[] runningFrom) : HappyHolidays_StateBase(hh)
	{
		float fleeCooldown = hh.fleeTime;
		readonly HappyHolidays_StateBase prevState = prevState;
		readonly DijkstraMap map = new(hh.ec, PathType.Nav, int.MaxValue, runningFrom);
		public override void Enter()
		{
			base.Enter();
			hh.Navigator.maxSpeed = hh.maxRunSpeed;
			hh.Navigator.SetSpeed(hh.maxRunSpeed);
			map.QueueUpdate();
			map.Activate();
			ChangeNavigationState(new NavigationState_WanderFlee(hh, 0, map));
		}

		public override void Update()
		{
			base.Update();
			fleeCooldown -= hh.TimeScale * Time.deltaTime;
			if (fleeCooldown <= 0f)
				hh.behaviorStateMachine.ChangeState(prevState);
			else
			{
				float newSpeed = Mathf.Lerp(hh.maxRunSpeed, hh.normSpeed, 1f / fleeCooldown);
				hh.Navigator.maxSpeed = newSpeed;
				hh.Navigator.SetSpeed(newSpeed);
			}

		}

		public override void Exit()
		{
			base.Exit();
			map.Deactivate();
		}
	}

	internal class HappyHolidays_WaitToRespawn(HappyHolidays hh) : HappyHolidays_StateBase(hh)
	{
		public override void Enter()
		{
			base.Enter();
			hh.MarkAsDisabled(true);
			hh.Navigator.maxSpeed = 0;
			hh.Navigator.SetSpeed(0);
			ChangeNavigationState(new NavigationState_DoNothing(hh, 0));
			prevHeight = hh.Entity.InternalHeight;
			hh.Entity.SetHeight(hh.despawnHeight);
		}

		public override void Update()
		{
			base.Update();
			cooldown -= hh.TimeScale * Time.deltaTime;
			if (cooldown < 0f)
				hh.behaviorStateMachine.ChangeState(new HappyHolidays_AboutToRespawn(hh, prevHeight));

		}

		public override void Exit()
		{
			base.Exit();
			var cells = hh.ec.mainHall.AllTilesNoGarbage(false, false);
			if (cells.Count > 0)
				hh.transform.position = cells[Random.Range(0, cells.Count)].CenterWorldPosition;

		}

		float prevHeight;
		float cooldown = hh.respawnCooldown;
	}

	internal class HappyHolidays_AboutToRespawn(HappyHolidays hh, float height) : HappyHolidays_StateBase(hh)
	{
		public override void Update()
		{
			base.Update();
			ableOfRespawning -= hh.TimeScale * Time.deltaTime;
			if (ableOfRespawning < 0f)
			{
				hh.MarkAsDisabled(false);
				hh.Entity.SetHeight(prevHeight);
				hh.behaviorStateMachine.ChangeState(new HappyHolidays_Wondering(hh));
			}
		}

		public override void InPlayerSight(PlayerManager player)
		{
			base.InPlayerSight(player);
			ableOfRespawning = hh.aboutToRespawnCooldown;
		}

		public override void PlayerInSight(PlayerManager player)
		{
			base.InPlayerSight(player);
			ableOfRespawning = hh.aboutToRespawnCooldown;
		}

		public override void Exit()
		{
			base.Exit();
			hh.ResetSprite();
		}

		readonly float prevHeight = height;

		float ableOfRespawning = hh.aboutToRespawnCooldown;
	}
}
