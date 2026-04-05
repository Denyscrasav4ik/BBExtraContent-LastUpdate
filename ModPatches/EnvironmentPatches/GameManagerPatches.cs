using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BBTimes.CustomComponents;
using BBTimes.CustomContent.Misc;
using BBTimes.Extensions;
using BBTimes.Manager;
using HarmonyLib;
using MTM101BaldAPI.Components;
using PixelInternalAPI.Extensions;
using UnityEngine;

namespace BBTimes.ModPatches.EnvironmentPatches
{
    [HarmonyPatch(typeof(MainGameManager))]
    internal static class MainGameManagerPatches
    {
        [HarmonyPatch(typeof(CoreGameManager), nameof(CoreGameManager.Quit))]
        [HarmonyPostfix]
        static void ResetSecretEnding() => allowEndingToBePlayed = false;

        [HarmonyPatch("LoadSceneObject")]
        [HarmonyPrefix]
        private static void RedirectEndingIfPossible(ref SceneObject sceneObject, bool restarting)
        {
            if (!allowEndingToBePlayed) return;
            if (restarting)
            {
                allowEndingToBePlayed = false;
                return;
            }
            sceneObject = secretEndingObj;
            allowEndingToBePlayed = false;
        }

        public static bool allowEndingToBePlayed = false;
        internal static SceneObject secretEndingObj;

        [HarmonyPatch("AllNotebooks")]
        [HarmonyPostfix]
        private static void BaldiAngerPhase(MainGameManager __instance)
        {
            var core = Singleton<CoreGameManager>.Instance;
            if (core.currentMode == Mode.Free)
            {
                core.audMan.FlushQueue(true);
                return;
            }

            if (!BBTimesManager.plug.disableSchoolhouseEscape.Value &&
                !__instance.Ec.timeOut &&
                __instance.levelObject != null &&
                !__instance.levelObject.finalLevel)
                Singleton<MusicManager>.Instance.PlayMidi("Level_1_End", true);
        }

        [HarmonyPatch("LoadNextLevel")]
        [HarmonyReversePatch(HarmonyReversePatchType.Original)]
        static void LoadNextLevel(object instance) => throw new System.NotImplementedException("stub");

        [HarmonyPatch("LoadNextLevel")]
        [HarmonyPrefix]
        static bool PlayCutscene(MainGameManager __instance, bool ___allNotebooksFound)
        {
            if (__instance.levelObject == null || !__instance.levelObject.finalLevel || !___allNotebooksFound || BBTimesManager.plug.disableRedEndingCutscene.Value) return true;

            bool explorerMode = Singleton<CoreGameManager>.Instance.currentMode == Mode.Free;

            var elevatorsInScene = Object.FindObjectsOfType<Elevator>();
            var elevator = elevatorsInScene.FirstOrDefault(x => x.CurrentState == ElevatorState.FinishingLevel)
                           ?? elevatorsInScene.FirstOrDefault(x => x.GateIsOpen);

            if (!elevator) return true;

            __instance.Ec.AddTimeScale(new(0f, 1f, 0f));
            var cam = new GameObject("CameraView").AddComponent<Camera>();
            cam.gameObject.AddComponent<CullAffector>();
            var player = Singleton<CoreGameManager>.Instance.GetPlayer(0);

            while (!player.plm.Entity.InteractionDisabled || !player.plm.Entity.Frozen)
            {
                player.plm.Entity.SetInteractionState(false);
                player.plm.Entity.SetFrozen(true);
            }

            PlayerVisual.GetPlayerVisual(0).gameObject.SetActive(false);
            Singleton<CoreGameManager>.Instance.GetCamera(0).SetControllable(false);
            Singleton<CoreGameManager>.Instance.disablePause = true;

            SoundObject slapSound = null;
            Sprite baldiSlap = null, normalBaldiSprite = null;

            var schoolBaldi = __instance.Ec.GetBaldi();
            if (schoolBaldi)
            {
                schoolBaldi.AudMan.volumeMultiplier = 0f;
                schoolBaldi.AudMan.UpdateAudioDeviceVolume();
                normalBaldiSprite = baldiSlap = schoolBaldi.spriteRenderer[0].sprite;
                slapSound = schoolBaldi.slap;
                schoolBaldi.SlapNormal();
                baldiSlap = schoolBaldi.spriteRenderer[0].sprite;
                schoolBaldi.Entity.SetActive(false);
            }
            else explorerMode = true;

            Direction elevatorDir = elevator.Door.direction.GetOpposite(), elevatorFacingDir = elevator.Door.direction;
            Vector3 elvCenterPos = elevator.Door.bTile.CenterWorldPosition;
            Vector3 elvFrontPos = elevator.Door.aTile.CenterWorldPosition;
            Vector3 frontOfElevatorPos = __instance.Ec.CellFromPosition(elevator.Door.aTile.position + elevatorDir.ToIntVector2()).CenterWorldPosition;
            frontOfElevatorPos.y = explorerMode ? 3.9f : (schoolBaldi ? schoolBaldi.spriteRenderer[0].transform.position.y : 0);

            var baldi = Object.Instantiate(placeholderBaldi);
            if (!explorerMode) baldi.sprite = normalBaldiSprite;

            __instance.StartCoroutine(Animation(cam, __instance, elevator));

            IEnumerator Animation(Camera cam, MainGameManager man, Elevator el)
            {
                player.Teleport(elvCenterPos);
                bool subs = Singleton<PlayerFileManager>.Instance.subtitles;
                Singleton<PlayerFileManager>.Instance.subtitles = false;

                Vector3 startPos, endPos;
                Quaternion rotStart, rotEnd;
                int maxIndex = 2;
                const float ENTRANCE_TO_ELEVATOR_POSITION = 1.75f, ENTRANCE_TO_ELEVATOR_ROTATION = 1.25f, ENTRANCE_TO_ELEVATOR_NOTICE_DELAY = 0.15f,
                            BALDI_ENCOUNTER_NOTICE_DELAY = 0.65f, BALDI_ENCOUNTER_NOTICE_ROTATION = 0.28f,
                            BALDI_ENCOUNTER_CONFRONT_DELAY = 1.25f, BALDI_ENCOUNTER_CONFRONT_PLAYERSCARE_ROTATION = 0.55f, BALDI_ENCOUNTER_CONFRONT_PLAYERSCARE_POSITION = 0.45f,
                            BALDI_ENCOUNTER_CONFRONT_BALDI_POSITION = 0.45f, POST_ENCOUNTER_DELAY = 0.95f, POST_ENCOUNTER_ROTATION = 2.15f, EXPLOSION_ROTATION = 0.55f;

                float[] times = new float[maxIndex], deltas = new float[maxIndex], maxTs = new float[maxIndex];
                int finishedTimes = 0;
                float bobFrequency = 0f, bobMagnitude = 0f;

                cam.transform.position = elvFrontPos + elevatorDir.ToVector3() * 1.75f;
                cam.transform.rotation = elevatorFacingDir.ToRotation();

                bobFrequency = 4f; bobMagnitude = 0.04f;
                startPos = cam.transform.position; endPos = elvFrontPos + elevatorDir.ToVector3();
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(-15f, elevatorFacingDir.ToDegrees() + 12f, 0f);
                maxTs[0] = ENTRANCE_TO_ELEVATOR_POSITION; maxTs[1] = ENTRANCE_TO_ELEVATOR_ROTATION;

                while (finishedTimes < maxIndex)
                {
                    finishedTimes = 0;
                    for (int i = 0; i < maxIndex; i++) { times[i] += Time.deltaTime; if (times[i] >= maxTs[i]) finishedTimes++; deltas[i] = Mathf.Clamp01(times[i] / maxTs[i]); }
                    cam.transform.position = Vector3.Lerp(startPos, endPos, Easing.EaseInCubic(deltas[0])) + new Vector3(0, Mathf.Sin(Time.time * bobFrequency) * bobMagnitude, 0);
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseOutBack(deltas[1]));
                    yield return null;
                }

                startPos = cam.transform.position; endPos = elvFrontPos - elevatorDir.ToVector3() * 1.25f;
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(-15f, elevatorFacingDir.ToDegrees() - 15f, 0f);
                Quaternion rotMid_look2 = Quaternion.Euler(10f, rotStart.eulerAngles.y, 0f);
                for (int i = 0; i < maxIndex; i++) { times[i] = 0f; deltas[i] = 0f; }
                finishedTimes = 0;

                while (finishedTimes < maxIndex)
                {
                    finishedTimes = 0;
                    for (int i = 0; i < maxIndex; i++) { times[i] += Time.deltaTime; if (times[i] >= maxTs[i]) finishedTimes++; deltas[i] = Mathf.Clamp01(times[i] / maxTs[i]); }
                    cam.transform.position = Vector3.Slerp(startPos, endPos, Easing.EaseOutCubic(deltas[0])) + new Vector3(0, Mathf.Sin(Time.time * bobFrequency) * bobMagnitude, 0);
                    Quaternion midPath = Quaternion.Slerp(rotStart, rotMid_look2, Easing.EaseOutBackWeak(deltas[1]));
                    Quaternion endPath = Quaternion.Slerp(rotMid_look2, rotEnd, Easing.EaseOutBackWeak(deltas[1]));
                    cam.transform.rotation = Quaternion.Slerp(midPath, endPath, Easing.EaseOutBackWeak(deltas[1]));
                    yield return null;
                }

                bobFrequency = 5f; bobMagnitude = 0.06f;
                startPos = cam.transform.position; endPos = elvCenterPos + (elevatorDir.ToVector3() * (explorerMode ? -2.25f : 3.5f));
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(25f, elevatorFacingDir.ToDegrees(), 0f);
                for (int i = 0; i < maxIndex; i++) { times[i] = 0f; deltas[i] = 0f; }
                finishedTimes = 0; maxTs[0] = ENTRANCE_TO_ELEVATOR_POSITION * 1.25f; maxTs[1] = ENTRANCE_TO_ELEVATOR_ROTATION * 1.55f;

                while (finishedTimes < maxIndex)
                {
                    finishedTimes = 0;
                    for (int i = 0; i < maxIndex; i++) { times[i] += Time.deltaTime; if (times[i] >= maxTs[i]) finishedTimes++; deltas[i] = Mathf.Clamp01(times[i] / maxTs[i]); }
                    cam.transform.position = Vector3.Lerp(startPos, endPos, Easing.EaseInOutCubic(deltas[0])) + new Vector3(0, Mathf.Sin(Time.time * bobFrequency) * bobMagnitude, 0);
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseOutCubic(deltas[1]));
                    yield return null;
                }

                if (!explorerMode && slapSound) Singleton<CoreGameManager>.Instance.audMan.PlaySingle(slapSound);
                yield return new WaitForSecondsRealtime(ENTRANCE_TO_ELEVATOR_NOTICE_DELAY);

                Cell[] cellsToSpawnFire = [
                    man.Ec.CellFromPosition(man.Ec.CellFromPosition(frontOfElevatorPos).position + elevatorDir.PerpendicularList()[0].ToIntVector2()),
                    man.Ec.CellFromPosition(man.Ec.CellFromPosition(frontOfElevatorPos).position + elevatorDir.PerpendicularList()[1].ToIntVector2()),
                    man.Ec.CellFromPosition(el.Door.aTile.position + elevatorDir.PerpendicularList()[0].ToIntVector2()),
                    man.Ec.CellFromPosition(el.Door.aTile.position + elevatorDir.PerpendicularList()[1].ToIntVector2()),
                ];

                for (int amount = 0; amount < 4; amount++) { foreach (var cell_f in cellsToSpawnFire) AddFire(cell_f, man.Ec, 1f); }

                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(0f, elevatorFacingDir.ToDegrees(), 0f);
                for (int i = 0; i < maxIndex; i++) { times[i] = 0f; deltas[i] = 0f; }
                finishedTimes = 0; maxTs[0] = 1f; maxTs[1] = BALDI_ENCOUNTER_NOTICE_ROTATION;

                while (finishedTimes < maxIndex)
                {
                    finishedTimes = 0;
                    for (int i = 0; i < maxIndex; i++) { times[i] += Time.deltaTime; if (times[i] >= maxTs[i]) finishedTimes++; deltas[i] = Mathf.Clamp01(times[i] / maxTs[i]); }
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Mathf.Lerp(deltas[1], Easing.EaseOutBack(deltas[1]), 0.4f));
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(BALDI_ENCOUNTER_NOTICE_DELAY * 1.15f);
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(2.5f, elevatorFacingDir.ToDegrees() - 16f, 0f);
                for (int i = 0; i < maxIndex; i++) { times[i] = 0f; deltas[i] = 0f; }
                finishedTimes = 0; maxTs[1] = BALDI_ENCOUNTER_NOTICE_ROTATION * 0.85f;

                while (finishedTimes < maxIndex)
                {
                    finishedTimes = 0;
                    for (int i = 0; i < maxIndex; i++) { times[i] += Time.deltaTime; if (times[i] >= maxTs[i]) finishedTimes++; deltas[i] = Mathf.Clamp01(times[i] / maxTs[i]); }
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseOutBackWeak(deltas[1]));
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(BALDI_ENCOUNTER_NOTICE_DELAY * 0.56f);
                baldi.transform.position = frontOfElevatorPos;
                startPos = cam.transform.position; rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(0f, elevatorDir.ToDegrees(), 0f);
                for (int i = 0; i < maxIndex; i++) { times[i] = 0f; deltas[i] = 0f; }
                finishedTimes = 0; maxTs[1] = BALDI_ENCOUNTER_NOTICE_ROTATION * 1.25f;

                while (finishedTimes < maxIndex)
                {
                    finishedTimes = 0;
                    for (int i = 0; i < maxIndex; i++) { times[i] += Time.deltaTime; if (times[i] >= maxTs[i]) finishedTimes++; deltas[i] = Mathf.Clamp01(times[i] / maxTs[i]); }
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseOutBackWeak(deltas[1]));
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(BALDI_ENCOUNTER_CONFRONT_DELAY);
                if (explorerMode) goto explorerModeSkip;

                baldi.sprite = baldiSlap; if (slapSound) Singleton<CoreGameManager>.Instance.audMan.PlaySingle(slapSound);
                Vector3 baldi_startPos = baldi.transform.position; Vector3 baldi_endPos = elvFrontPos; baldi_endPos.y = baldi_startPos.y;
                maxIndex = 3; times = new float[maxIndex]; deltas = new float[maxIndex]; maxTs = new float[maxIndex];
                startPos = cam.transform.position; endPos = elvCenterPos - elevatorDir.ToVector3() * 2.25f;
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(25f, elevatorDir.ToDegrees() + 80f, 0f);
                maxTs[0] = BALDI_ENCOUNTER_CONFRONT_PLAYERSCARE_POSITION; maxTs[1] = BALDI_ENCOUNTER_CONFRONT_PLAYERSCARE_ROTATION; maxTs[2] = BALDI_ENCOUNTER_CONFRONT_BALDI_POSITION;
                finishedTimes = 0;

                while (finishedTimes < maxIndex)
                {
                    finishedTimes = 0;
                    for (int i = 0; i < maxIndex; i++) { times[i] += Time.deltaTime; if (times[i] >= maxTs[i]) finishedTimes++; deltas[i] = Mathf.Clamp01(times[i] / maxTs[i]); }
                    if (deltas[1] >= 0.85f && el.Door.IsOpen) el.OpenDoor(false);
                    cam.transform.position = Vector3.Lerp(startPos, endPos, deltas[0]);
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseOutBackWeak(deltas[1]));
                    baldi.transform.position = Vector3.Lerp(baldi_startPos, baldi_endPos, deltas[2]);
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(POST_ENCOUNTER_DELAY);
                int bangs = 0; float bangDelay = 0f; const float minBangAdd = 0.25f, maxBangAdd = 0.45f, bangMaxDelay = 0.35f;
                bobFrequency = 1.0f; bobMagnitude = 0.02f; maxIndex = 1; startPos = cam.transform.position; startPos.y = 5f;
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(9.5f, elevatorDir.ToDegrees() + 65f, 0f);
                times = new float[maxIndex]; deltas = new float[maxIndex]; maxTs = new float[maxIndex]; maxTs[0] = POST_ENCOUNTER_ROTATION * 0.25f;
                finishedTimes = 0;

                while (finishedTimes < maxIndex || bangs < 4)
                {
                    finishedTimes = 0; times[0] += Time.deltaTime; if (times[0] >= maxTs[0]) finishedTimes++; deltas[0] = Mathf.Clamp01(times[0] / maxTs[0]);
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseInOutCubic(deltas[0]));
                    bangDelay += Time.deltaTime; if (bangDelay > bangMaxDelay) { el.audMan.PlaySingle(bal_bangDoor); bangDelay -= Random.Range(minBangAdd, maxBangAdd); bangs++; }
                    cam.transform.position = startPos + new Vector3(0, Mathf.Sin(Time.time * bobFrequency) * bobMagnitude, 0);
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(POST_ENCOUNTER_DELAY * 0.65f);
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(0f, elevatorDir.ToDegrees(), 0f);
                Vector3 midEuler = Vector3.Lerp(rotStart.eulerAngles, rotEnd.eulerAngles, 0.5f); midEuler.x = Mathf.Max(rotStart.eulerAngles.x, rotEnd.eulerAngles.x) + 20f;
                Quaternion rotMid = Quaternion.Euler(midEuler);
                times[0] = 0f; bangs = 0; bangDelay = 0f; finishedTimes = 0; maxTs[0] = POST_ENCOUNTER_ROTATION * 0.85f;

                while (finishedTimes < maxIndex || bangs < 10)
                {
                    finishedTimes = 0; times[0] += Time.deltaTime;
                    if (times[0] < maxTs[0]) cam.transform.position = startPos + new Vector3(0, Mathf.Sin(Time.time * bobFrequency) * bobMagnitude, 0);
                    else { finishedTimes++; cam.transform.position = startPos; }
                    deltas[0] = Mathf.Clamp01(times[0] / maxTs[0]);
                    Quaternion part1 = Quaternion.Slerp(rotStart, rotMid, Easing.EaseInOutCubic(deltas[0]));
                    Quaternion part2 = Quaternion.Slerp(rotMid, rotEnd, Easing.EaseInOutCubic(deltas[0]));
                    cam.transform.rotation = Quaternion.Slerp(part1, part2, Easing.EaseInOutCubic(deltas[0]));
                    bangDelay += Time.deltaTime; if (bangDelay > bangMaxDelay) { el.audMan.PlaySingle(bal_bangDoor); bangDelay -= Random.Range(minBangAdd, maxBangAdd); bangs++; }
                    yield return null;
                }

                cam.transform.position = startPos;
                el.audMan.PlaySingle(bal_explosionOutside);
                man.StartCoroutine(TriggerExplosions());

            explorerModeSkip:
                yield return new WaitForSecondsRealtime(POST_ENCOUNTER_DELAY * (explorerMode ? 2.25f : 0.25f));
                if (explorerMode) { maxIndex = 1; startPos = cam.transform.position; startPos.y = 5f; if (el.Door.IsOpen) el.OpenDoor(false); }
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(-7f, elevatorDir.ToDegrees() + 15f, 0f);
                times[0] = 0f; maxTs[0] = EXPLOSION_ROTATION * 0.35f; finishedTimes = 0;

                while (finishedTimes < maxIndex)
                {
                    times[0] += Time.deltaTime; if (times[0] >= maxTs[0]) finishedTimes++;
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseInOutCubic(Mathf.Clamp01(times[0] / maxTs[0])));
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(POST_ENCOUNTER_DELAY * 0.75f);
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(-7f, elevatorDir.ToDegrees() - 15f, 0f);
                times[0] = 0f; maxTs[0] = EXPLOSION_ROTATION; finishedTimes = 0;

                while (finishedTimes < maxIndex)
                {
                    times[0] += Time.deltaTime; if (times[0] >= maxTs[0]) finishedTimes++;
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseInOutCubic(Mathf.Clamp01(times[0] / maxTs[0])));
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(POST_ENCOUNTER_DELAY * 2.5f);
                rotStart = cam.transform.rotation; rotEnd = Quaternion.Euler(0f, elevatorDir.ToDegrees(), 0f);
                times[0] = 0f; maxTs[0] = explorerMode ? 0.85f : 2.75f; finishedTimes = 0;

                while (finishedTimes < maxIndex)
                {
                    times[0] += Time.deltaTime; if (times[0] >= maxTs[0]) finishedTimes++;
                    cam.transform.rotation = Quaternion.Slerp(rotStart, rotEnd, Easing.EaseInOutCubic(Mathf.Clamp01(times[0] / maxTs[0])));
                    yield return null;
                }

                if (explorerMode) yield return new WaitForSecondsRealtime(0.75f);
                while (el.audMan.AnyAudioIsPlaying) yield return null;

                var ogCam = Singleton<CoreGameManager>.Instance.GetCamera(0);
                ogCam.transform.position = cam.transform.position; ogCam.transform.rotation = cam.transform.rotation;
                player.transform.position = ogCam.transform.position; player.transform.rotation = ogCam.transform.rotation;
                Singleton<MusicManager>.Instance.StopMidi(); Singleton<MusicManager>.Instance.StopFile();
                Singleton<PlayerFileManager>.Instance.subtitles = subs; Object.Destroy(cam);
                PlayerVisual.GetPlayerVisual(0).gameObject.SetActive(true); PlayerVisual.GetPlayerVisual(0).SetEmotion(0);
                LoadNextLevel(man);

                IEnumerator TriggerExplosions()
                {
                    int b = 0; float d = 0f; int m = Random.Range(3, 5);
                    while (b <= m) { d += Time.deltaTime; if (d > 0.65f) { d -= 0.65f + Random.Range(-0.25f, 0.95f); el.audMan.PlaySingle(bal_explosionOutside); b++; } yield return null; }
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(TimeOut), "Begin")]
        [HarmonyPostfix]
        static void FixMusicSpeed() => Singleton<MusicManager>.Instance.SetSpeed(1f);

        [HarmonyPatch(typeof(Elevator), "SetState")]
        [HarmonyPrefix]
        private static bool PreventDoorCloseOnFinish(Elevator __instance, ElevatorState state, ref Cell ___lightCell)
        {
            var manager = Singleton<BaseGameManager>.Instance;

            if (state == ElevatorState.FinishingLevel &&
                manager != null &&
                manager.levelObject != null &&
                manager.levelObject.finalLevel &&
                !BBTimesManager.plug.disableRedEndingCutscene.Value)
            {
                if (___lightCell != null)
                    ___lightCell.SetLight(true);

                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(Elevator), "SetState")]
        [HarmonyPostfix]
        private static void REDAnimation(Elevator __instance, EnvironmentController ___ec)
        {
            var manager = Singleton<BaseGameManager>.Instance;
            if (___ec.timeOut || manager.levelObject == null || manager is not MainGameManager || Singleton<CoreGameManager>.Instance.currentMode == Mode.Free || BBTimesManager.plug.disableSchoolhouseEscape.Value)
                return;

            if (!manager.AllNotebooksFound) return;

            int brokenCount = ___ec.ElevatorManager.Elevators.Count(e => e.CurrentState == ElevatorState.OutOfOrder);

            if (brokenCount == 1 && __instance.CurrentState == ElevatorState.OutOfOrder)
            {
                Shader.SetGlobalColor("_SkyboxColor", Color.red);
                var levelbox = Object.FindObjectOfType<Structure_LevelBox>();
                if (levelbox)
                {
                    foreach (var meshRenderer in levelbox.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (meshRenderer.material.shader.name == "Shader Graphs/Standard")
                            meshRenderer.material.SetColor("_TextureColor", Color.red);
                    }
                }
                Singleton<MusicManager>.Instance.SetSpeed(0.1f);
                manager.StartCoroutine(___ec.LightChanger(___ec.AllExistentCells(), 0.2f));
                if (manager.levelObject.finalLevel)
                    Singleton<MusicManager>.Instance.QueueFile(chaos0, true);
                return;
            }

            if (brokenCount == 2 && __instance.CurrentState == ElevatorState.OutOfOrder)
            {
                manager.StartCoroutine(BreakTheTimer(___ec));
                Singleton<MusicManager>.Instance.StopFile();
                Singleton<MusicManager>.Instance.QueueFile(chaos1, true);
                Singleton<MusicManager>.Instance.MidiPlayer.MPTK_Transpose = Random.Range(-24, -12);
                ___ec.standardDarkLevel = new Color(1f, 0f, 0f);
                foreach (var c in ___ec.AllExistentCells()) { c.permanentLight = true; ___ec.GenerateLight(c, Color.red, 1, true); c.SetPower(true); }
                return;
            }

            if (brokenCount == 3 && __instance.CurrentState == ElevatorState.OutOfOrder && manager.levelObject.finalLevel)
            {
                ___ec.GetComponent<EnvironmentControllerData>()?.OngoingEvents.ForEach(x => { if (x != null) ___ec.StopCoroutine(x); });
                for (int i = 0; i < ___ec.CurrentEventTypes.Count; i++)
                {
                    var v2 = ___ec.GetEvent(___ec.CurrentEventTypes[i]);
                    if (v2.Active) v2.EndEarlier();
                }

                foreach (var c in ___ec.AllExistentCells()) { c.permanentLight = true; ___ec.GenerateLight(c, Color.red, 1, true); c.SetPower(true); }

                var gate = __instance.transform.Find("Gate");
                if (gate != null && gateTextures != null && gateTextures.Length >= 3)
                {
                    gate.transform.Find("Gate (1)").GetComponent<MeshRenderer>().material.mainTexture = gateTextures[0];
                    gate.transform.Find("Gate (0)").GetComponent<MeshRenderer>().material.mainTexture = gateTextures[1];
                    gate.transform.Find("Gate (2)").GetComponent<MeshRenderer>().material.mainTexture = gateTextures[2];
                }

                Singleton<MusicManager>.Instance.QueueFile(chaos2, true);
                if (!Singleton<PlayerFileManager>.Instance.reduceFlashing) { ___ec.standardDarkLevel = new Color(0.2f, 0f, 0f); ___ec.FlickerLights(true); }
                for (int i = 0; i < Singleton<MusicManager>.Instance.MidiPlayer.Channels.Length; i++) Singleton<MusicManager>.Instance.MidiPlayer.MPTK_ChannelEnableSet(i, false);

                Baldi baldiToFollow = null;
                for (int i = 0; i < ___ec.Npcs.Count; i++)
                {
                    var npc = ___ec.Npcs[i];
                    try
                    {
                        if (npc is Baldi bald) { ___ec.StartCoroutine(GameExtensions.InfiniteAnger(bald, 0.6f)); if (npc.Character == Character.Baldi) baldiToFollow = bald; continue; }
                        npc.Despawn(); i--;
                    }
                    catch { Object.Destroy(npc.gameObject); ___ec.Npcs.RemoveAt(i--); }
                }
                ___ec.StartCoroutine(SpawnFires(___ec));
                if (baldiToFollow) ___ec.StartCoroutine(DangerousAngryBaldiAnimation(___ec, baldiToFollow));
            }
        }

        static IEnumerator BreakTheTimer(EnvironmentController ec)
        {
            while (ec != null)
            {
                float newTime = (ec.timeLimit > 10f) ? 5f : 9999f;

                ec.SetTimeLimit(newTime);

                ec.timeOut = false;

                yield return new WaitForSeconds(1f);
            }
        }

        static IEnumerator SpawnFires(EnvironmentController ec)
        {
            float cooldown = fireCooldown, maxCooldown = fireCooldown;
            var cs = ec.AllTilesNoGarbage(false, true);
            foreach (var el in ec.Elevators) { cs.Remove(el.Door.aTile); }
            while (cs.Count != 0)
            {
                cooldown -= Time.deltaTime;
                if (cooldown <= 0f)
                {
                    var c = Random.Range(0, cs.Count);
                    maxCooldown = Mathf.Max(0.03f, maxCooldown - 0.05f);
                    cooldown += maxCooldown;
                    AddFire(cs[c], ec);
                    cs.RemoveAt(c);
                }
                yield return null;
            }
        }

        static IEnumerator DangerousAngryBaldiAnimation(EnvironmentController ec, Baldi baldi)
        {
            const float distanceFromBaldi = 14.5f, shakeIntensity = 0.35f, shakeSpeed = 20f, maxRoll = 4f, fovEnd = 100f, fovInitialStart = 65f, framerate = 24.85f;
            var baldiEntity = baldi.Entity; baldiEntity.SetVisible(false);
            var animatedBaldi = Object.Instantiate(placeholderBaldi); animatedBaldi.sprite = angryBaldiAnimation[0]; animatedBaldi.transform.position = baldiEntity.transform.position;
            TimeScaleModifier timeScaleMod = new(0f, 0f, 0f); ec.AddTimeScale(timeScaleMod);
            float elevatorDelay = 1.5f; bool camFovThing = false;
            for (int i = 0; i < Singleton<CoreGameManager>.Instance.setPlayers; i++) Singleton<CoreGameManager>.Instance.GetCamera(i).GetCustomCam().ReverseSlideFOVAnimation(new ValueModifier(), 35f, 8f);
            ValueModifier mod = new();
            while (elevatorDelay > 0f || mod.addend >= fovInitialStart) { elevatorDelay -= Time.deltaTime; if (!camFovThing && elevatorDelay < 0.5f) { camFovThing = true; for (int i = 0; i < Singleton<CoreGameManager>.Instance.setPlayers; i++) Singleton<CoreGameManager>.Instance.GetCamera(i).GetCustomCam().SlideFOVAnimation(mod, fovInitialStart, 10f, framerate); } yield return null; }
            Singleton<CoreGameManager>.Instance.audMan.PlaySingle(angryBal);
            var cam = new GameObject("BaldiAngryCamView").AddComponent<Camera>(); cam.gameObject.AddComponent<CullAffector>();
            float fovStart = cam.fieldOfView; Vector3 basePosition = animatedBaldi.transform.position + (animatedBaldi.transform.forward * distanceFromBaldi); Vector3 startCamPos = animatedBaldi.transform.position + (animatedBaldi.transform.forward * 0.5f);
            var cell = ec.CellFromPosition(basePosition); if (cell.Null || cell.HasWallInDirection(Directions.DirFromVector3(animatedBaldi.transform.forward, 45f).GetOpposite())) { basePosition = animatedBaldi.transform.position - (animatedBaldi.transform.forward * distanceFromBaldi); startCamPos = animatedBaldi.transform.position - (animatedBaldi.transform.forward * 0.5f); }
            cam.transform.position = startCamPos; cam.transform.LookAt(animatedBaldi.transform);
            float frame = 0f, finalFrameIndex = angryBaldiAnimation.Length + 1.5f, baseShakeSeed = Random.Range(0f, 100f);
            while (true)
            {
                if (Time.timeScale == 0f) { yield return null; continue; }
                float progress = frame / finalFrameIndex, intensityMultiplier = Mathf.Clamp01(progress * 2f);
                cam.transform.position = Vector3.Lerp(startCamPos, basePosition, EaseInOutQuad(progress)) + ((cam.transform.right * ((Mathf.PerlinNoise(baseShakeSeed + (Time.time * shakeSpeed), 0) * 2) - 1)) + (cam.transform.up * ((Mathf.PerlinNoise(0, baseShakeSeed + (Time.time * shakeSpeed)) * 2) - 1))) * shakeIntensity * intensityMultiplier;
                cam.fieldOfView = Mathf.Lerp(fovStart, fovEnd, progress * progress); cam.transform.LookAt(animatedBaldi.transform); cam.transform.Rotate(0, 0, Mathf.Sin(Time.time * 40f) * maxRoll * intensityMultiplier, Space.Self);
                if (Mathf.FloorToInt(frame % 10) == 0) cam.transform.position += cam.transform.forward * 0.4f * intensityMultiplier;
                frame += Time.deltaTime * framerate; if (frame >= angryBaldiAnimation.Length) break;
                animatedBaldi.sprite = angryBaldiAnimation[Mathf.FloorToInt(frame)]; yield return null;
            }
            float punchTimer = 0f; while (punchTimer < 0.2f) { punchTimer += Time.deltaTime; cam.transform.position += cam.transform.forward * 75f * Time.deltaTime; cam.fieldOfView += Time.deltaTime * 120f; yield return null; }
            ec.RemoveTimeScale(timeScaleMod); baldiEntity.SetVisible(true); Object.Destroy(animatedBaldi.gameObject); Object.Destroy(cam.gameObject);
            for (int i = 0; i < Singleton<CoreGameManager>.Instance.setPlayers; i++) Singleton<CoreGameManager>.Instance.GetCamera(i).GetCustomCam().ResetSlideFOVAnimation(mod, 10f, framerate);
            static float EaseInOutQuad(float t) => t < 0.5f ? 2 * t * t : 1 - (Mathf.Pow((-2 * t) + 2, 2) / 2);
        }

        private static void AddFire(Cell cell, EnvironmentController ec, float smoothness = 5f)
        {
            var obj = Object.Instantiate(fire, cell.FloorWorldPosition, fire.transform.rotation);
            obj.transform.position += new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            obj.SetActive(true);
            cell.AddRenderer(obj.GetComponent<SpriteRenderer>());
            var f = obj.GetComponent<SchoolFire>();
            f.Initialize(ec);
            f.StartAnimation(f.transform.localScale, smoothness);
            f.transform.localScale = Vector3.zero;
        }

        const float fireCooldown = 3f;
        internal static LoopingSoundObject chaos0, chaos1, chaos2;
        internal static SoundObject angryBal, bal_bangDoor, bal_explosionOutside;
        internal static GameObject fire;
        internal static Texture2D[] gateTextures = new Texture2D[3];
        internal static SpriteRenderer placeholderBaldi;
        internal static Sprite cardboardBaldi;
        internal static Sprite[] angryBaldiAnimation;
    }

    static class Easing
    {
        private const float s = 1.70158f;
        public static float EaseInBack(float t) => t * t * ((s + 1) * t - s);
        public static float EaseOutBack(float t) => 1 + ((--t) * t * ((s + 1) * t + s));
        public static float EaseInOutQuad(float t) => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
        public static float EaseOutCubic(float t) => (--t) * t * t + 1;
        public static float EaseInCubic(float t) => t * t * t;
        public static float EaseInOutCubic(float t) => t < 0.5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
        public static float EaseOutBackWeak(float t) => Mathf.Lerp(EaseInCubic(t), EaseOutBack(t), t);
    }
}
