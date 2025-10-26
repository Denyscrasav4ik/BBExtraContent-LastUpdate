using System.Collections;
using System.Linq;
using BBTimes.CustomContent.RoomFunctions;
using BBTimes.Extensions;
using MTM101BaldAPI;
using UnityEngine;

namespace BBTimes.CustomContent.Misc
{
	public class FocusedStudent : EnvironmentObject
	{
		private Vector3 pos;
		private bool shaking = false;
		private bool speaking = false;
		private bool initialized = false;
		private bool lecturing = false;

		private int disturbedCount = 0;
		private Appearances activeAppearance;
		private Coroutine showUpBubblesCor;
		readonly MovementModifier moveMod = new(Vector3.zero, 0.45f);

		[SerializeField]
		internal PropagatedAudioManager audMan;
		[SerializeField]
		internal SpriteRenderer renderer;
		[SerializeField]
		internal SpriteRenderer[] readingIndicatorRenderers;
		[SerializeField]
		internal float readingIndicatorShowUpDelay = 0.35f;
		public SoundObject audBookNoise; // Sound played when switching states
		public bool IsBusy => lecturing || speaking;

		internal static Appearances[] appearanceSet;

		public override void LoadingFinished()
		{
			base.LoadingFinished();

			var room = ec.CellFromPosition(transform.position).room;
			var fun = room.functionObject.GetComponent<FocusRoomFunction>();
			if (!fun)
			{
				fun = room.functionObject.AddComponent<FocusRoomFunction>();
				room.functions.AddFunction(fun);
				fun.Initialize(room);
			}
			fun.Setup(this);

			pos = transform.position;
			initialized = true;

			activeAppearance = appearanceSet[Random.Range(0, appearanceSet.Length)];
			renderer.sprite = activeAppearance.Reading;
			audMan.subtitleColor = activeAppearance.subtitleColor;
		}

		public bool Disturbed(PlayerManager player)
		{
			if (IsBusy) return false; // ignore if already busy

			speaking = true;
			audMan.FlushQueue(true);

			disturbedCount++;

			// Screaming (Principal call)
			if (disturbedCount >= 3)
			{
				StartCoroutine(ScreamPhase());
				return true;
			}

			// Otherwise, lecture
			StartCoroutine(LecturePlayer(player));
			return false;
		}

		// LECTURE COROUTINE
		private IEnumerator LecturePlayer(PlayerManager player)
		{
			SwitchState(false, false);
			lecturing = true;
			player.Am.moveMods.Add(moveMod);
			renderer.sprite = activeAppearance.Speaking;
			audMan.QueueAudio(disturbedCount == 1 ? activeAppearance.audAskSilence : activeAppearance.audAskSilence2);

			// Lock player view toward student and apply slowdown (handled externally by MovementModifier placeholder)

			while (audMan.QueuedAudioIsPlaying)
			{
				if (!player) yield break;

				player.transform.RotateSmoothlyToNextPoint(transform.position, 0.95f);
				yield return null;
			}

			player.Am.moveMods.Remove(moveMod);
			lecturing = false;
			speaking = false;
			renderer.sprite = activeAppearance.Reading;
		}

		// SCREAM PHASE
		private IEnumerator ScreamPhase()
		{
			SwitchState(false, false);
			shaking = true;
			renderer.sprite = activeAppearance.Screaming;
			FullyRelax();

			audMan.QueueAudio(activeAppearance.audDisturbed);
			ec.CallOutPrincipals(transform.position);

			yield return new WaitUntil(() => !audMan.QueuedAudioIsPlaying);

			shaking = false;
			speaking = false;
			renderer.sprite = activeAppearance.Reading;
		}

		IEnumerator ShowUpReadingIndicators()
		{
			for (int i = 0; i < readingIndicatorRenderers.Length; i++)
			{
				var renderer = readingIndicatorRenderers[i];
				yield return new WaitForSecondsEnvironmentTimescale(ec, readingIndicatorShowUpDelay);
				renderer.enabled = true;
				yield return null;
			}
		}

		public void Relax() => disturbedCount = Mathf.Max(0, disturbedCount - 1);
		public void FullyRelax() => disturbedCount = 0;
		public void SwitchState(bool isReading) => SwitchState(isReading, true);
		private void SwitchState(bool isReading, bool makeBookNoise)
		{
			if (makeBookNoise) audMan.PlaySingle(audBookNoise);
			for (int i = 0; i < readingIndicatorRenderers.Length; i++) readingIndicatorRenderers[i].enabled = false; // Disable all by default

			if (showUpBubblesCor != null)
				StopCoroutine(showUpBubblesCor); // stop the coroutine if it is active
			if (isReading) // If it is reading, then play the animation again
				showUpBubblesCor = StartCoroutine(ShowUpReadingIndicators());
		}

		void Update()
		{
			if (!initialized) return;

			// Handle idle shaking and sprite reset
			if (!audMan.QueuedAudioIsPlaying && !speaking && !lecturing)
			{
				renderer.sprite = activeAppearance.Reading;
				shaking = false;
				transform.position = pos;
			}

			if (shaking && Time.timeScale != 0)
			{
				transform.position = pos + new Vector3(
					Random.Range(-0.5f, 0.5f),
					Random.Range(-0.5f, 0.5f),
					Random.Range(-0.5f, 0.5f)
				);
			}
		}

		public bool IsSpeaking => speaking || lecturing;

		// --- APPEARANCE DATA STRUCTURE ---
		[System.Serializable]
		public class Appearances
		{
			public Sprite Reading;
			public Sprite Speaking;
			public Sprite Screaming;

			public SoundObject audAskSilence;
			public SoundObject audAskSilence2;
			public SoundObject audDisturbed;

			public Color subtitleColor;
		}
	}
}
