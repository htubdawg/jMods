using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The minimap on the screen, in a corner, and lives for the whole game. Each frame it redraws its <see cref="PixelMap"/> from the
	/// <see cref="Race"/>, turned to the player's heading with every car on it.
	/// </summary>
	internal sealed class MiniMapHud : MonoBehaviour
	{
		private RectTransform root;
		private RawImage view;

		private PixelMap pixels;
		private readonly LapCache lap = new LapCache();
		private bool failed;
		/// <summary>Whether K has the minimap showing: for this game only, so an enabled minimap is always there when the game starts.</summary>
		private bool shown = true;

		internal void OnSceneLoaded(Scene loaded)
		{
			failed = false;
		}

		private void Awake()
		{
			try
			{
				BuildUi();
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Minimap: could not make its HUD, it stays off: " + e);
				enabled = false;
			}
		}

		private void OnDestroy() => pixels?.Destroy();

		private void Update()
		{
			if (failed)
			{
				return;
			}
			try
			{
				Mod.WatchConfig();
				if (Mod.Toggle != null && Mod.Toggle.Down)
				{
					shown = !shown;
				}
				bool show = Mod.Enabled.Value && shown && !InteriorMath.Paused;
				if (show)
				{
					Race.Tick();
					show = Race.Ready;
				}
				if (root.gameObject.activeSelf != show)
				{
					root.gameObject.SetActive(show);
				}
				if (show)
				{
					Draw();
				}
			}
			catch (Exception e)
			{
				// One warning per scene, not one per frame.
				failed = true;
				root.gameObject.SetActive(false);
				Mod.Log.LogWarning("Minimap: stopped for this scene after an error: " + e);
			}
		}

		/// <summary>The minimap's width in its own pixels: the set size cut into pixels of the set pixel size, kept even so it has a middle.</summary>
		private static int Resolution() => Mathf.Clamp(Mathf.RoundToInt(Mod.Size.Value / (float)Mod.PixelSize.Value / 2f) * 2, 40, 300);

		private void Draw()
		{
			int size = Resolution();
			if (pixels == null || pixels.Size != size)
			{
				pixels?.Destroy();
				pixels = new PixelMap(size, true);
				view.texture = pixels.Texture;
			}
			if (Race.OpenWorld)
			{
				pixels.DrawLocal(OpenWorldLink.Tiles, Race.Palette, Race.Heading, Race.MetresPerPixel(pixels), Race.Player, Race.Cars);
			}
			else
			{
				pixels.Draw(lap.Get(size, pixels.Room), Race.Palette, Race.Heading, Race.Player, Race.Cars);
			}
			pixels.Upload();
			Place();
		}

		/// <summary>Puts the minimap in its corner, a whole number of screen pixels per map pixel (at 1080p); the settings can change while the game runs.</summary>
		private void Place()
		{
			float size = pixels.Size * Mod.PixelSize.Value;
			float margin = Mod.Margin.Value;
			string corner = Mod.Corner.Value ?? "";
			float x = corner.EndsWith("Right") ? 1f : 0f;
			float y = corner.StartsWith("Bottom") ? 0f : 1f;
			Vector2 anchor = new Vector2(x, y);
			root.anchorMin = anchor;
			root.anchorMax = anchor;
			root.pivot = anchor;
			root.anchoredPosition = new Vector2(x > 0.5f ? -margin : margin, y > 0.5f ? -margin : margin);
			root.sizeDelta = new Vector2(size, size);
			view.color = new Color(1f, 1f, 1f, Mod.Opacity.Value);
		}

		private void BuildUi()
		{
			GameObject canvasObject = new GameObject("MiniMapCanvas");
			canvasObject.transform.SetParent(transform, false);
			Canvas canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 50;
			canvas.pixelPerfect = true;
			CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 1f;

			view = new GameObject("MiniMap", typeof(RectTransform)).AddComponent<RawImage>();
			view.raycastTarget = false;
			root = view.rectTransform;
			root.SetParent(canvasObject.transform, false);
			root.gameObject.SetActive(false);
		}
	}
}
