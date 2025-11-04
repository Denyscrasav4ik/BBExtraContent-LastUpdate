using System.Collections.Generic;
using BBTimes.CustomComponents;
using BBTimes.CustomContent.NPCs;
using BBTimes.Extensions;
using BBTimes.Plugin;
using UnityEngine;

namespace BBTimes.CustomContent.RoomFunctions
{
	public class FreezingRoomFunction : RoomFunction
	{
		public PuddingFan owner;
		public override void Initialize(RoomController room)
		{
			base.Initialize(room);
			slipperController = SlipperController.CreateSlipperController(owner);
			foreach (var cell in room.cells)
			{
				slipperController.CreateStain(cell);
			}
		}
		public override void OnEntityEnter(Entity entity)
		{
			base.OnEntityEnter(entity);
			allEntities.Add(entity);
			if (immuneEntity == entity)
				return;

			if (entity.CompareTag("Player") && entity.TryGetComponent<PlayerMovement>(out var pm))
				pm.pm.GetAttribute().AddAttribute(Storage.ATTR_STOP_PLAYER_MOVEMENT_RUN_TAG);
		}

		public override void OnEntityExit(Entity entity)
		{
			base.OnEntityExit(entity);
			allEntities.Remove(entity);
			if (immuneEntity == entity)
				return;

			if (entity.CompareTag("Player") && entity.TryGetComponent<PlayerMovement>(out var pm))
				pm.pm.GetAttribute().RemoveAttribute(Storage.ATTR_STOP_PLAYER_MOVEMENT_RUN_TAG);
		}
		void OnDestroy()
		{
			if (slipperController)
				Destroy(slipperController.gameObject);

			foreach (var entity in allEntities)
			{
				if (entity && entity.CompareTag("Player") && entity.TryGetComponent<PlayerMovement>(out var pm))
					pm.pm.GetAttribute().RemoveAttribute(Storage.ATTR_STOP_PLAYER_MOVEMENT_RUN_TAG);
			}
		}

		public void AssignImmunityToEntity(Entity e) =>
			immuneEntity = e;

		Entity immuneEntity;
		private SlipperController slipperController;
		readonly List<Entity> allEntities = [];
	}

	public class DriedPuddingRoomFunction : RoomFunction
	{
		public float lifeTime = 30f;
		public override void OnPlayerStay(PlayerManager player)
		{
			base.OnPlayerStay(player);
			// Allow running without losing stamina
			player.plm.stamina = player.plm.staminaMax;
		}

		public override void Initialize(RoomController room)
		{
			base.Initialize(room);
			var cells = room.cells;

			int am = Random.Range(4, 8); // Spawn some visual puddles
			for (int i = 0; i <= am; i++)
			{
				if (cells.Count == 0)
					return;

				int index = Random.Range(0, cells.Count);

				var slip = Instantiate(slipMatPre);
				slip.transform.position = cells[index].FloorWorldPosition;
				slippingMaterials.Add(slip);

				// Disable functionality, keep visuals
				var collider = slip.GetComponent<Collider>();
				if (collider)
					collider.enabled = false;

				var materialComponent = slip.GetComponent<SlippingMaterial>();
				if (materialComponent)
					Destroy(materialComponent);

				cells.RemoveAt(index);
			}
		}

		void Update()
		{
			lifeTime -= room.ec.EnvironmentTimeScale * Time.deltaTime;
			if (lifeTime <= 0f)
				Destroy(this);
		}
		private void OnDestroy()
		{
			foreach (var slip in slippingMaterials)
				Destroy(slip);
		}
		public GameObject slipMatPre;
		readonly List<GameObject> slippingMaterials = [];
	}
}