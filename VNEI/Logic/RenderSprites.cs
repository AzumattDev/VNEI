﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VNEI.Logic {
    public class RenderSprites : MonoBehaviour {
        public static RenderSprites instance;
        private Camera renderer;
        private Light light;

        private const int Layer = 3;
        private static readonly Vector3 SpawnPoint = new Vector3(1000f, 1000f, 1000f);

        private void Awake() {
            instance = this;
        }

        public void StartRender() {
            StartCoroutine(RenderAll());
        }

        private void SetupRendering() {
            Log.LogInfo("Setup renderer camera");
            renderer = new GameObject("Render Camera", typeof(Camera)).GetComponent<Camera>();
            renderer.backgroundColor = new Color(0, 0, 0, 0);
            renderer.clearFlags = CameraClearFlags.SolidColor;
            renderer.transform.position = SpawnPoint;
            renderer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            renderer.fieldOfView = 0.5f;
            renderer.farClipPlane = 100000;
            renderer.cullingMask = 1 << Layer;

            light = new GameObject("Render Light", typeof(Light)).GetComponent<Light>();
            light.transform.position = SpawnPoint;
            light.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            light.type = LightType.Directional;
            light.cullingMask = 1 << Layer;
        }

        private void ClearRendering() {
            Destroy(renderer.gameObject);
            Destroy(light.gameObject);
            renderer.targetTexture.Release();
            Log.LogInfo("Finished rendering");
        }

        IEnumerator RenderAll() {
            Queue<RenderObject> queue = new Queue<RenderObject>();

            while (Indexing.ToRenderSprite.Count > 0) {
                string prefabName = Indexing.ToRenderSprite.Dequeue();

                if (Indexing.GetItem(prefabName)?.isActive != true) {
                    continue;
                }

                if (queue.Any(i => i.name == prefabName)) {
                    continue;
                }

                RenderObject renderObject = SpawnSafe(ZNetScene.instance.GetPrefab(prefabName), out bool hasMesh);

                if (!hasMesh) {
                    continue;
                }

                queue.Enqueue(renderObject);
            }

            // wait for destroyed components really be destroyed
            yield return null;

            SetupRendering();

            while (queue.Count > 0) {
                RenderObject currentSpawn = queue.Dequeue();
                RenderSpriteFromPrefab(currentSpawn);
            }

            ClearRendering();
        }

        private void RenderSpriteFromPrefab(RenderObject spawn) {
            RenderTexture oldRenderTexture = RenderTexture.active;
            renderer.targetTexture = RenderTexture.GetTemporary(128, 128, 32);
            RenderTexture.active = renderer.targetTexture;

            SetLayerRecursive(spawn.gameObject.transform, Layer);
            spawn.gameObject.SetActive(true);

            float maxMeshSize = spawn.MaxSizeXY() + 0.1f;
            float distance = maxMeshSize / Mathf.Tan(renderer.fieldOfView * Mathf.Deg2Rad);
            renderer.transform.position = SpawnPoint + new Vector3(0, 0, distance);

            renderer.Render();
            Log.LogDebug($"Rendered {spawn.name}");

            spawn.gameObject.SetActive(false);
            Destroy(spawn.gameObject);

            RenderTexture targetTexture = renderer.targetTexture;
            Texture2D previewImage = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGBA32, false);
            previewImage.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            previewImage.Apply();

            RenderTexture.active = oldRenderTexture;

            Sprite sprite = Sprite.Create(previewImage, new Rect(0, 0, previewImage.width, previewImage.height), new Vector2(0.5f, 0.5f));
            Indexing.GetItem(spawn.name).SetIcon(sprite);
        }

        private static void SetLayerRecursive(Transform transform, int layer) {
            for (int i = 0; i < transform.childCount; i++) {
                SetLayerRecursive(transform.GetChild(i), layer);
            }

            transform.gameObject.layer = layer;
        }

        private static bool IsVisual(Component component) {
            return component is Renderer || component is MeshFilter;
        }

        private static RenderObject SpawnSafe(GameObject prefab, out bool hasMesh) {
            hasMesh = prefab.GetComponentsInChildren<Component>(false).Any(IsVisual);

            if (!hasMesh) {
                return null;
            }

            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);

            GameObject spawn = Instantiate(prefab, new Vector3(0, 0, 0), Quaternion.identity);
            spawn.name = prefab.name;
            spawn.transform.rotation = Quaternion.Euler(0, -30f, 0);

            prefab.SetActive(wasActive);

            Vector3 min = new Vector3(100, 100, 100);
            Vector3 max = new Vector3(-100, -100, -100);

            foreach (Renderer meshRenderer in spawn.GetComponentsInChildren<Renderer>()) {
                min = Vector3.Min(min, meshRenderer.bounds.min);
                max = Vector3.Max(max, meshRenderer.bounds.max);
            }

            spawn.transform.position = SpawnPoint - (min + max) / 2f;
            Vector3 size = new Vector3(Mathf.Abs(min.x) + Mathf.Abs(max.x),
                                       Mathf.Abs(min.y) + Mathf.Abs(max.y),
                                       Mathf.Abs(min.z) + Mathf.Abs(max.z));

            // needs to be destroyed first as Character depend on it
            foreach (CharacterDrop characterDrop in spawn.GetComponentsInChildren<CharacterDrop>()) {
                Destroy(characterDrop);
            }

            // needs to be destroyed first as Rigidbody depend on it
            foreach (Joint joint in spawn.GetComponentsInChildren<Joint>()) {
                Destroy(joint);
            }

            // destroy all other components
            foreach (Component component in spawn.GetComponentsInChildren<Component>(true)) {
                if (component is Transform || IsVisual(component)) {
                    continue;
                }

                Destroy(component);
            }

            // just in case it doesn't gets deleted properly later
            TimedDestruction timedDestruction = spawn.AddComponent<TimedDestruction>();
            timedDestruction.Trigger(1f);

            return new RenderObject(prefab.name, spawn, size);
        }

        private class RenderObject {
            public string name;
            public GameObject gameObject;
            public Vector3 size;

            public RenderObject(string name, GameObject gameObject, Vector3 size) {
                this.name = name;
                this.gameObject = gameObject;
                this.size = size;
            }

            public float MaxSizeXY() {
                return Mathf.Max(size.x, size.y);
            }
        }
    }
}
