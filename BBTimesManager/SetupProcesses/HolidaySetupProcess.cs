using System.Collections.Generic;
using System.IO;
using System.Linq;
using BBTimes.CustomComponents;
using BBTimes.CustomContent.Misc;
using BBTimes.CustomContent.RoomFunctions;
using BBTimes.Extensions;
using BBTimes.Plugin;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using PixelInternalAPI.Classes;
using PixelInternalAPI.Extensions;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using UnityEngine;

namespace BBTimes.Manager;

internal static partial class BBTimesManager
{
    public static void SetupChristmasHoliday()
    {
        // --- Setup Christmas Baldi prefab and audio ---
        var baldiSPrites = TextureExtensions.LoadSpriteSheet(6, 1, 30f, ChristmasPath, GetAssetName("christmasBaldi.png"));
        var chBaldi = ObjectCreationExtensions.CreateSpriteBillboard(baldiSPrites[0])
            .AddSpriteHolder(out var chBaldiRenderer, 4f, LayerStorage.iClickableLayer); // Baldo offset should be exactly 5f + hisDefaultoffset
        chBaldi.gameObject.AddBoxCollider(Vector3.up * 5f, new(2.5f, 10f, 2.5f), true);
        chBaldi.name = "Times_ChristmasBaldi";
        chBaldiRenderer.name = "Times_ChristmasBaldi_Renderer";

        chBaldi.gameObject.ConvertToPrefab(true); // He won't be in the editor, he's made specifically for christmas mode

        var christmasBaldi = chBaldi.gameObject.AddComponent<ChristmasBaldi>();

        // --- Assign audio and present references ---
        christmasBaldi.audMan = christmasBaldi.gameObject.CreatePropagatedAudioManager(95f, 175f);
        christmasBaldi.present = man.Get<ItemObject>("Item_Present");
        christmasBaldi.audBell = man.Get<SoundObject>("audRing");
        christmasBaldi.audIntro = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(Path.Combine(ChristmasPath, "BAL_PresentIntro.wav")), "Vfx_BAL_Pitstop_PresentIntro_1", SoundType.Voice, Color.green);
        christmasBaldi.audIntro.additionalKeys = [
            new() { key = "Vfx_BAL_Pitstop_PresentIntro_2", time = 2.417f },
                    new() { key = "Vfx_BAL_Pitstop_PresentIntro_3", time = 5.492f },
                    new() { key = "Vfx_BAL_Wow", time = 9.544f },
                    new() { key = "Vfx_BAL_Pitstop_PresentIntro_4", time = 11.029f },
                    new() { key = "Vfx_BAL_Pitstop_PresentIntro_5", time = 14.059f }
            ];

        christmasBaldi.audBuyItem = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(Path.Combine(ChristmasPath, "BAL_buypresent.wav")), "Vfx_BAL_Pitstop_MerryChristmas", SoundType.Voice, Color.green);

        christmasBaldi.audNoYtps = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(Path.Combine(ChristmasPath, "BAL_needYtpsForPresent.wav")), "Vfx_BAL_Pitstop_Nopresent_1", SoundType.Voice, Color.green);
        christmasBaldi.audNoYtps.additionalKeys = [
            new() { key = "Vfx_BAL_Pitstop_Nopresent_2", time = 0.818f }
            ];

        christmasBaldi.audGenerous = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(Path.Combine(ChristmasPath, "BAL_Pitstop_Generous.wav")), "Vfx_BAL_Pitstop_Generous_1", SoundType.Voice, Color.green);
        christmasBaldi.audGenerous.additionalKeys = [
            new() { key = "Vfx_BAL_Pitstop_Generous_2", time = 2.597f }
            ];

        christmasBaldi.audCollectingPresent = [
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(Path.Combine(ChristmasPath, "BAL_Pitstop_Thanks1.wav")), "Vfx_BAL_Pitstop_Thanks1", SoundType.Voice, Color.green),
                    ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(Path.Combine(ChristmasPath, "BAL_Pitstop_Thanks2.wav")), "Vfx_BAL_Pitstop_Thanks2", SoundType.Voice, Color.green)
            ];

        // --- Add sprite volume animator ---
        var volumeAnimator = christmasBaldi.gameObject.AddComponent<SpriteVolumeAnimator>();
        volumeAnimator.audMan = christmasBaldi.audMan;
        volumeAnimator.renderer = chBaldiRenderer;
        volumeAnimator.volumeMultipler = 1.2f;
        volumeAnimator.sprites = baldiSPrites;

        // --- Add Christmas Baldi to pitstop asset ---
        var pitstopAsset = GenericExtensions.FindResourceObjectByName<LevelAsset>("Pitstop"); // Find shop pitstop here
                                                                                              // Load RBPL file
        using (BinaryReader reader = new(File.OpenRead(Path.Combine(ChristmasPath, "OutsideRoom.rbpl"))))
        {
            var christmasRoom = LevelImporter.CreateRoomAsset(BaldiRoomAsset.Read(reader));
            christmasRoom.name = "ChristmasBaldisRoom";
            christmasRoom.keepTextures = true;

            pitstopAsset.roomAssetPlacements.Add(new()
            {
                room = christmasRoom,
                position = new(37, 8),
                direction = Direction.East,
                doorSpawnId = 0
            });
            pitstopAsset.doors.Add(new(
                0, // Room id is 0 because how the heck am I supposed to know which index a roomAssetPlacement will be at?
                GenericExtensions.FindResourceObjectByName<SwingDoor>("Door_Swinging"),
                new(36, 8),
                Direction.East
                ));
        }
        pitstopAsset.tbos.Add(new()
        {
            prefab = christmasBaldi,
            position = new(40, 8),
            direction = Direction.West
        });

    }

    public static void SetupMarch31Holiday()
    {
        HashSet<GameObject> processedPrefabs = []; // Track processed prefabs
        HashSet<Sprite> contrastedSprites = [];
        HashSet<Material> processedMaterials = [];
        HashSet<Texture2D> contrastedTextures = [];

        Texture2D ToLowContrast(Texture2D source, float contrastFactor = 0.75f) // < 1 = less contrast, > 1 = more contrast
        {
            if (source == null)
                return null;

            if (contrastedTextures.Contains(source))
                return source;

            if (!source.isReadable) // Make sure it is readable
                source = source.MakeReadableTexture();

            Color[] pixels = source.GetPixels();
            float midpoint = 0.5f; // keeps midtones stable

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];

                if (c == Color.black) continue;

                // Adjust contrast per channel
                c.r = Mathf.Clamp01((c.r - midpoint) * contrastFactor + midpoint);
                c.g = Mathf.Clamp01((c.g - midpoint) * contrastFactor + midpoint);
                c.b = Mathf.Clamp01((c.b - midpoint) * contrastFactor + midpoint);

                pixels[i] = c;
            }

            source.SetPixels(pixels);
            source.Apply();

            contrastedTextures.Add(source);

            return source;
        }

        void UpdateTexturesFromMaterialArray(Material[] mats)
        {
            if (mats == null) return;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (!processedMaterials.Contains(mat) && mat.shader.FindPropertyIndex("_MainTex") != -1 && mat.mainTexture is Texture2D tex2D)
                {
                    processedMaterials.Add(mat);
                    mat.mainTexture = ToLowContrast(tex2D);
                }
            }
        }

        foreach (var poster in Resources.FindObjectsOfTypeAll<PosterObject>())
            poster.baseTexture = ToLowContrast(poster.baseTexture);
        foreach (var window in Resources.FindObjectsOfTypeAll<WindowObject>())
            foreach (var mat in window.windowPre.windows)
                UpdateTexturesFromMaterialArray(mat.materials);
        foreach (var door in Resources.FindObjectsOfTypeAll<Door>())
            foreach (var mat in door.GetComponentsInChildren<MeshRenderer>())
                UpdateTexturesFromMaterialArray(mat.materials);

        foreach (var levelObject in Resources.FindObjectsOfTypeAll<LevelObject>())
        {
            // Hallway contrast changes
            LowContrastWeights(levelObject.hallWallTexs);
            LowContrastWeights(levelObject.hallFloorTexs);
            LowContrastWeights(levelObject.hallCeilingTexs);

            LowContrastTransformWeights(levelObject.hallLights);

            // Room changes
            foreach (var rg in levelObject.roomGroup)
            {
                LowContrastWeights(rg.ceilingTexture);
                LowContrastWeights(rg.floorTexture);
                LowContrastWeights(rg.wallTexture);
                LowContrastRoomWeights(rg.potentialRooms);
                LowContrastTransformWeights(rg.light);
            }
            LowContrastRoomWeights(levelObject.potentialSpecialRooms);

            void LowContrastWeights(WeightedTexture2D[] weightedTextures)
            {
                if (weightedTextures != null)
                    for (int i = 0; i < weightedTextures.Length; i++)
                        weightedTextures[i].selection = ToLowContrast(weightedTextures[i].selection);
            }

            void LowContrastTransformWeights(WeightedTransform[] weightedTransforms)
            {
                if (weightedTransforms != null)
                    for (int i = 0; i < weightedTransforms.Length; i++)
                        LowContrastTransform(weightedTransforms[i].selection);
            }

            void LowContrastTransform(Transform transform)
            {
                if (!transform || processedPrefabs.Contains(transform.gameObject)) return;
                processedPrefabs.Add(transform.gameObject);
                foreach (var mat in transform.GetComponentsInChildren<MeshRenderer>())
                    UpdateTexturesFromMaterialArray(mat.materials);
            }

            void LowContrastTextures(Texture2D[] weightedTextures)
            {
                if (weightedTextures != null)
                    for (int i = 0; i < weightedTextures.Length; i++)
                        weightedTextures[i] = ToLowContrast(weightedTextures[i]);
            }

            void LowContrastRoomWeights(WeightedRoomAsset[] weightedAssets)
            {
                if (weightedAssets == null) return;

                for (int i = 0; i < weightedAssets.Length; i++)
                {
                    var asset = weightedAssets[i];
                    if (asset == null || asset.selection == null)
                        continue;

                    var sel = asset.selection;

                    sel.ceilTex = ToLowContrast(sel.ceilTex);
                    sel.wallTex = ToLowContrast(sel.wallTex);
                    sel.florTex = ToLowContrast(sel.florTex);

                    if (sel.roomFunctionContainer && sel.roomFunctionContainer.TryGetComponent<HighCeilingRoomFunction>(out var highCeil))
                    {
                        highCeil.customCeiling = ToLowContrast(highCeil.customCeiling);
                        LowContrastTextures(highCeil.customWallProximityToCeil);
                    }

                    LowContrastTransform(sel.lightPre);

                    List<Transform> allTransforms = new();

                    if (sel.basicObjects != null)
                        allTransforms.AddRange(sel.basicObjects.Where(o => o != null && o.prefab != null).Select(o => o.prefab));

                    if (sel.basicSwaps != null)
                        allTransforms.AddRange(sel.basicSwaps.Where(o => o != null && o.prefabToSwap != null).Select(o => o.prefabToSwap));

                    if (sel.hasActivity && sel.activity?.prefab != null)
                        allTransforms.Add(sel.activity.prefab.transform);

                    foreach (var obj in allTransforms)
                    {
                        if (!obj || processedPrefabs.Contains(obj.gameObject))
                            continue;

                        processedPrefabs.Add(obj.gameObject);

                        foreach (var mesh in obj.GetComponentsInChildren<MeshRenderer>())
                            UpdateTexturesFromMaterialArray(mesh.materials);

                        foreach (var sprRend in obj.GetComponentsInChildren<SpriteRenderer>())
                        {
                            if (sprRend.sprite == null || contrastedSprites.Contains(sprRend.sprite))
                                continue;

                            var originalSprite = sprRend.sprite;
                            var rect = originalSprite.rect;

                            var pivot = new Vector2(
                                originalSprite.pivot.x / rect.width,
                                originalSprite.pivot.y / rect.height
                            );

                            if (!originalSprite.texture.isReadable)
                            {
                                sprRend.sprite = Sprite.Create(
                                    ToLowContrast(originalSprite.texture),
                                    rect,
                                    pivot,
                                    originalSprite.pixelsPerUnit,
                                    0,
                                    SpriteMeshType.FullRect
                                );
                            }
                            else
                            {
                                ToLowContrast(originalSprite.texture);
                            }

                            contrastedSprites.Add(sprRend.sprite);
                        }
                    }
                }
            }

            // Baldi Changes

            var tutorBaldi_Hi = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(Path.Combine(March31Path, "BAL_Hi.wav")), "Vfx_BAL_March31_OhHi", SoundType.Voice, Color.green);
            SoundObject[] tutorBaldi_Countdown = new SoundObject[10];
            for (int i = tutorBaldi_Countdown.Length - 1; i >= 0; i--)
            {
                int idx = i + 1;
                tutorBaldi_Countdown[i] = ObjectCreators.CreateSoundObject(
                    AssetLoader.AudioClipFromFile(Path.Combine(March31Path, $"BAL_Math_{idx}_Classic.wav")),
                    $"Vfx_{(idx == 10 ? 0 : idx)}",
                    SoundType.Voice,
                    Color.green);
            }
            // Baldi new audio setup
            foreach (var tutor in GenericExtensions.FindResourceObjects<HappyBaldi>())
            {
                tutor.audIntro = tutorBaldi_Hi;
                tutor.audCountdown = tutorBaldi_Countdown;
            }
            SoundObject[] praiseSounds = new SoundObject[5];
            for (int i = 0; i < praiseSounds.Length; i++)
            {
                praiseSounds[i] = ObjectCreators.CreateSoundObject(
                    AssetLoader.AudioClipFromFile(Path.Combine(March31Path, $"BAL_Praise{i + 1}_Classic.wav")),
                    $"Vfx_BAL_Praise{i + 1}",
                    SoundType.Voice,
                    Color.green);
            }
            foreach (var baldi in GenericExtensions.FindResourceObjects<Baldi>())
            {
                WeightedSoundObject[] weightedPraiseSounds = new WeightedSoundObject[praiseSounds.Length];
                for (int i = 0; i < weightedPraiseSounds.Length && i < baldi.correctSounds.Length; i++)
                {
                    weightedPraiseSounds[i] = baldi.correctSounds[i];
                    weightedPraiseSounds[i].selection = praiseSounds[i];
                }
            }

            // Decor_ExitSign changes
            var classicExit = AssetLoader.TextureFromFile(Path.Combine(March31Path, "ExitSign.png"));
            var classicExitMap = AssetLoader.TextureFromFile(Path.Combine(March31Path, "ExitSign_Lightmap.png"));
            var classicExitSprite = AssetLoader.SpriteFromTexture2D(classicExit, new(0.5f, 1f), 50f);

            foreach (var decor in GenericExtensions.FindResourceObjects<SpriteRenderer>())
            {
                if (decor.name != "Decor_ExitSign") continue;
                decor.sharedMaterial.mainTexture = classicExit;
                decor.sharedMaterial.SetTexture(Storage.SPRITESTANDARD_LIGHTGUIDE, classicExitMap);
                decor.sprite = classicExitSprite;
            }
        }
    }
}
