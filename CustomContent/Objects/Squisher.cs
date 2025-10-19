using System.Collections;
using System.Collections.Generic;
using BBTimes.Extensions;
using UnityEngine;

namespace BBTimes.CustomContent.Objects
{
	public class Squisher : EnvironmentObject, IButtonReceiver
	{
		public void ConnectButton(GameButtonBase button) { }
		public void ButtonPressed(bool val)
		{
			if (!active)
			{
				forceSquish = true;
				cooldown = 0f;
			}
		}

		public void TurnMe(bool on)
		{
			waitingForSquish = on;
			cooldown = Random.Range(squishCooldownMin, squishCooldownMax);
			if (!on)
			{
				StopAllCoroutines();
				StartCoroutine(ResetPosition());
			}
		}

		public void Setup(float speed, float fixedCooldown)
		{
			this.fixedCooldown = fixedCooldown;
			Setup(speed);
		}

		public void Setup(float speed)
		{
			ogPos = transform.position;
			cooldown = UsesFixedCooldown ? fixedCooldown : Random.Range(squishCooldownMin, squishCooldownMax);
			this.speed = speed;
		}

		void Update()
		{
			if (!waitingForSquish)
				return;

			cooldown -= ec.EnvironmentTimeScale * Time.deltaTime;
			if (cooldown < 0f)
			{
				waitingForSquish = false;
				StartCoroutine(SquishSequence());
			}
		}

		void OnTriggerEnter(Collider other)
		{
			if (canSquish && other.isTrigger)
			{
				var e = other.GetComponent<Entity>();
				if (e)
				{
					if (!e.Squished) // Should fix the noclip bug
						e.Squish(squishForce);
					e.SetFrozen(true);
					squishedEntities.Add(e);
				}

			}
		}

		IEnumerator ResetPosition()
		{
			blockCollider.enabled = false;
			ec.BlockAllDirs(ogPos.ZeroOutY(), false);
			canSquish = false;
			audMan.FlushQueue(true);
			Vector3 pos = transform.position;
			float t = 0f;
			while (true) // *squishing*
			{
				t += ec.EnvironmentTimeScale * Time.deltaTime * speed;

				if (t >= 1f)
					break;

				transform.position = Vector3.Lerp(pos, ogPos, t);
				yield return null;
			}
			transform.position = ogPos;
			yield break;
		}

		IEnumerator SquishSequence()
		{
			active = true;
			float cool = forceSquish ? Random.Range(forceSquishPrepareMin, forceSquishPrepareMax) : Random.Range(prepareSquishMin, prepareSquishMax);
			forceSquish = false;

			audMan.QueueAudio(audPrepare);
			audMan.SetLoop(true);

			while (cool > 0f) // preparing to squish
			{
				if (Time.timeScale > 0f)
					transform.position = new(ogPos.x + Random.Range(-prepareShakeAmount, prepareShakeAmount), ogPos.y, ogPos.z + Random.Range(-prepareShakeAmount, prepareShakeAmount));
				cool -= ec.EnvironmentTimeScale * Time.deltaTime;
				yield return null;
			}


			audMan.FlushQueue(true);
			audMan.QueueAudio(audRun);
			audMan.SetLoop(true);

			transform.position = ogPos;
			float t = 0;
			Vector3 squishPos = ogPos.ZeroOutY();
			ec.BlockAllDirs(squishPos, true);
			collider.enabled = true;
			canSquish = true;

			while (true) // *squishing*
			{
				t += ec.EnvironmentTimeScale * Time.deltaTime * speed;

				if (t >= 1f)
					break;

				transform.position = Vector3.Lerp(ogPos, squishPos, t);
				yield return null;
			}
			transform.position = squishPos;
			canSquish = false;
			blockCollider.enabled = true; // This should be called after any frozen entity is done
			collider.enabled = false;
			cool = Random.Range(stayGroundMin, stayGroundMax);
			audMan.FlushQueue(true);
			audMan.QueueAudio(audHit);

			while (cool > 0f) // stay in the ground for a while
			{
				cool -= ec.EnvironmentTimeScale * Time.deltaTime;
				yield return null;
			}

			blockCollider.enabled = false;
			ec.BlockAllDirs(squishPos, false);
			while (squishedEntities.Count != 0)
			{
				squishedEntities[0].SetFrozen(false);
				squishedEntities.RemoveAt(0);
			}

			audMan.QueueAudio(audPrepare);
			audMan.SetLoop(true);

			t = 0f;
			while (true) // go back up
			{
				t += ec.EnvironmentTimeScale * Time.deltaTime * (speed * returnSpeedMultiplier);

				if (t >= 1f)
					break;

				transform.position = Vector3.Lerp(squishPos, ogPos, t);
				yield return null;
			}
			transform.position = ogPos;
			waitingForSquish = true;
			cooldown = UsesFixedCooldown ? fixedCooldown : Random.Range(squishCooldownMin, squishCooldownMax);
			audMan.FlushQueue(true);
			active = false;

			yield break;
		}

		public bool UsesFixedCooldown => fixedCooldown != -1f;

		[SerializeField]
		internal BoxCollider collider, blockCollider;

		[SerializeField]
		internal PropagatedAudioManager audMan;

		[SerializeField]
		internal SoundObject audPrepare, audRun, audHit;

		[SerializeField]
		internal float squishCooldownMin = 5f, squishCooldownMax = 10f, squishForce = 12f, prepareSquishMin = 3f, prepareSquishMax = 5f, forceSquishPrepareMin = 0.25f,
		forceSquishPrepareMax = 0.75f, prepareShakeAmount = 0.2f, stayGroundMin = 2f, stayGroundMax = 3.5f, returnSpeedMultiplier = 0.5f;

		float cooldown, fixedCooldown = -1f;
		float speed;
		bool waitingForSquish = true, canSquish = false, forceSquish = false, active = false;

		readonly List<Entity> squishedEntities = [];

		Vector3 ogPos;
	}
}
