using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HalfGripSlapPack
{
	/// <summary>Writes the held gun's actual silhouette into foreground depth before refraction and glass tint.</summary>
	internal sealed class HeldGunWeatherOcclusionPass : ScriptableRenderPass
	{
		internal readonly struct Draw
		{
			internal readonly Renderer Renderer;
			internal readonly Material Material;
			internal readonly int Submesh, Pass;
			internal Draw(Renderer renderer, Material material, int submesh, int pass)
			{ Renderer = renderer; Material = material; Submesh = submesh; Pass = pass; }
		}

		private sealed class PassData
		{
			internal HeldGunWeatherOcclusionPass Owner;
			internal Draw[] Draws;
			internal Matrix4x4 View, Projection, Foreground;
		}

		private Camera camera;
		private Draw[] draws;
		internal long Records { get; private set; }
		internal long Submissions { get; private set; }

		internal HeldGunWeatherOcclusionPass()
		{
			renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox - 1);
		}

		internal void Prepare(Camera camera, Draw[] draws) { this.camera = camera; this.draws = draws; }

		/// <summary>Changes only clip depth for the mask; restores the command matrices before subsequent draws.</summary>
		public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
		{
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
			if (cameraData.camera != camera || draws == null || draws.Length == 0) return;
			UniversalResourceData resources = frameData.Get<UniversalResourceData>();
			if (!resources.activeDepthTexture.IsValid()) return;
			Records++;
			using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass<PassData>("SlapPack held 3D gun weather occlusion", out PassData data))
			{
				data.Owner = this; data.Draws = draws; data.View = cameraData.GetViewMatrix(); data.Projection = cameraData.GetProjectionMatrix();
				data.Foreground = data.Projection;
				// SetViewProjectionMatrices takes the same CPU projection URP uses.
				// Leave clip X/Y/W intact and compress depth about the near plane.
				// Near clipping remains intact; backend depth conversion stays Unity's job.
				Vector4 near = data.Projection * new Vector4(0, 0, -camera.nearClipPlane, 1);
				for (int column = 0; column < 4; column++)
					data.Foreground[2, column] = HeldGunDepth.Compress(data.Projection[2, column], data.Projection[3, column], near.z / near.w);
				builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
				builder.AllowGlobalStateModification(true);
				builder.AllowPassCulling(false);
				builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
				{
					try
					{
						context.cmd.SetViewProjectionMatrices(d.View, d.Foreground);
						foreach (Draw draw in d.Draws)
							if (draw.Renderer && draw.Renderer.enabled && draw.Renderer.gameObject.activeInHierarchy && draw.Material)
							{
								context.cmd.DrawRenderer(draw.Renderer, draw.Material, draw.Submesh, draw.Pass);
								d.Owner.Submissions++;
							}
					}
					finally { context.cmd.SetViewProjectionMatrices(d.View, d.Projection); }
				});
			}
		}
	}
}
