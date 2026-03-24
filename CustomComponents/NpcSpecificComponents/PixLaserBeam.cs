using System.Collections;
using BBTimes.CustomContent.NPCs;
using UnityEngine;

namespace BBTimes.CustomComponents.NpcSpecificComponents
{
	public class PixLaserBeam : MonoBehaviour, IEntityTrigger
	{
		public void InitBeam(Pix pixc, Entity target)
		{
			pix = pixc;
			targetEntity = target;
			ec = pixc.ec;

			entity.Initialize(ec, pix.transform.position);
			transform.LookAt(targetEntity.transform);
			direction = ec.CellFromPosition(transform.position).open ? transform.forward : Directions.DirFromVector3(targetEntity.transform.position - transform.position, 45f).ToVector3(); // If not in an open cell, just shoot a straight line

			entity.OnEntityMoveInitialCollision += (hit) =>
			{
				if (flying)
				{
					flying = false;
					pix?.DecrementBeamCount();
					Destroy(gameObject);
				}
			};
		}

		public void EntityTriggerStay(Entity otherEntity, Collider other, bool validCollision) { }

		public void EntityTriggerExit(Entity otherEntity, Collider other, bool validCollision)
		{
			if (validCollision && other.gameObject == pix.gameObject)
				ignorePix = false;
		}

		public void EntityTriggerEnter(Entity otherEntity, Collider other, bool validCollision)
		{
			if (!validCollision || !flying || (other.gameObject == pix.gameObject && ignorePix)) return;

			bool wasPlayer = other.CompareTag("Player");
			if (other.isTrigger && (other.CompareTag("NPC") || wasPlayer))
			{
				var hitEntity = other.GetComponent<Entity>();
				if (hitEntity)
				{
					flying = false;
					if (other.gameObject == targetEntity.gameObject)
					{
						pix?.SetAsSuccess();
						renderer.gameObject.SetActive(false);
					}


					audMan.QueueAudio(audShock);
					audMan.SetLoop(true);
					audMan.maintainLoop = true;

					pix?.DecrementBeamCount();
					actMod = hitEntity.ExternalActivity;
					actMod.moveMods.Add(moveMod);
					hitEntity.AddForce(new(other.transform.position - transform.position, 9f, -8.5f));

					var p = other.GetComponent<PlayerManager>();
					if (p)
						gauge = Singleton<CoreGameManager>.Instance.GetHud(p.playerNumber).gaugeManager.ActivateNewGauge(gaugeSprite, lifeTime);

					StartCoroutine(Timer());
				}
			}
		}

		void Update()
		{
			if (flying)
			{
				frame += 10 * ec.EnvironmentTimeScale * Time.deltaTime;
				frame %= flyingSprites.Length;
				renderer.sprite = flyingSprites[Mathf.FloorToInt(frame)];
				entity.UpdateInternalMovement(direction * speed * ec.EnvironmentTimeScale);
			}
			else
			{
				if (actMod != null && actMod.entity != null)
				{
					entity.UpdateInternalMovement(Vector3.zero);
					transform.position = actMod.entity.transform.position;
				}
				frame += 14 * ec.EnvironmentTimeScale * Time.deltaTime;
				frame %= shockSprites.Length;
				renderer.sprite = shockSprites[Mathf.FloorToInt(frame)];
			}
		}

		IEnumerator Timer()
		{
			float time = lifeTime;
			while (time > 0f)
			{
				time -= ec.EnvironmentTimeScale * Time.deltaTime;
				gauge?.SetValue(lifeTime, time);
				yield return null;
			}
			actMod?.moveMods.Remove(moveMod);
			gauge?.Deactivate();

			Destroy(gameObject);
		}

		bool flying = true, ignorePix = true;
		float frame = 0f;

		[SerializeField]
		internal Entity entity;

		[SerializeField]
		internal SpriteRenderer renderer;

		[SerializeField]
		internal Sprite[] flyingSprites;

		[SerializeField]
		internal Sprite[] shockSprites;

		[SerializeField]
		internal SoundObject audShock;

		[SerializeField]
		internal AudioManager audMan;

		[SerializeField]
		internal Sprite gaugeSprite;

		[SerializeField]
		internal float lifeTime = 15f;

		HudGauge gauge;
		Pix pix;
		Entity targetEntity;
		EnvironmentController ec;

		ActivityModifier actMod = null;

		readonly MovementModifier moveMod = new(Vector3.zero, 0.65f);
		Vector3 direction;

		const float speed = 25f;
	}
}