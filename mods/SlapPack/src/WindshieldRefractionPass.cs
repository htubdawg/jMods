using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HalfGripSlapPack
{
	/// <summary>Copies opaque objects and sky, then draws water before the existing transparent pass.</summary>
	internal sealed class WindshieldRefractionPass : ScriptableRenderPass
	{
		private readonly WindshieldRefraction owner;
		private static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), BaseMapST = Shader.PropertyToID("_BaseMap_ST");
		private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

		/// <summary>Resources for the built-in point blitter.</summary>
		private sealed class CopyData { internal TextureHandle Source, Destination; internal WindshieldRefraction Owner; }

		/// <summary>Explicit scene/depth dependencies for the water draw.</summary>
		private sealed class DrawData { internal TextureHandle Scene; internal WindshieldRefraction Owner; internal Matrix4x4 Model; }

		/// <summary>Requires sampleable camera color without requesting an extra opaque-color copy.</summary>
		internal WindshieldRefractionPass(WindshieldRefraction owner)
		{
			this.owner = owner;
			renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
			requiresIntermediateTexture = true;
		}

		/// <summary>Records a color downsample and a dependent water draw; never renders a second camera.</summary>
		public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
		{
			if (!owner || !owner.CanDraw) return;
			try
			{
				UniversalCameraData camera = frameData.Get<UniversalCameraData>();
				if (camera.camera != owner.TargetCamera) return;
				owner.Recorded();
				UniversalResourceData resources = frameData.Get<UniversalResourceData>();
				if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid() || !resources.activeDepthTexture.IsValid())
					throw new NotSupportedException("URP did not provide sampleable camera color and depth.");
				RenderTextureDescriptor descriptor = camera.cameraTargetDescriptor;
				float aspect = (float)descriptor.width / Math.Max(1, descriptor.height);
				descriptor.width = Math.Min(owner.CopyWidth, descriptor.width);
				descriptor.height = WindshieldRefractionMath.CopyHeight(descriptor.width, aspect);
				if (!owner.UpdateSurface(camera.GetProjectionMatrix() * camera.GetViewMatrix() * owner.ModelMatrix, camera.camera.nearClipPlane, descriptor.width, descriptor.height))
				{ owner.SkipFrame(); return; }
				descriptor.depthStencilFormat = GraphicsFormat.None; descriptor.depthBufferBits = 0;
				descriptor.msaaSamples = 1; descriptor.bindMS = false;
				descriptor.useMipMap = false; descriptor.autoGenerateMips = false;
				TextureHandle scene = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "SlapPack same-frame scene color", false, FilterMode.Point);
				using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass<CopyData>("SlapPack scene-color downsample", out CopyData data))
				{
					data.Source = resources.activeColorTexture; data.Destination = scene; data.Owner = owner;
					builder.UseTexture(data.Source, AccessFlags.Read);
					builder.SetRenderAttachment(scene, 0, AccessFlags.WriteAll);
					builder.AllowPassCulling(false);
					builder.SetRenderFunc((CopyData d, RasterGraphContext context) =>
					{
						try
						{
							RTHandle source = d.Source;
							Vector2 scale = source.useScaling ? (Vector2)source.rtHandleProperties.rtHandleScale : Vector2.one;
							bool flip = context.GetTextureUVOrigin(d.Source) != context.GetTextureUVOrigin(d.Destination);
							WindshieldRefractionMath.BlitScale(scale.x, scale.y, flip, out float sx, out float sy, out float oy);
							Blitter.BlitTexture(context.cmd, source, new Vector4(sx, sy, 0, oy), 0, false);
							if (d.Owner) d.Owner.Copied();
						}
						catch (Exception error) { if (d.Owner) d.Owner.Fail(error.Message); }
					});
				}
				using (IRasterRenderGraphBuilder builder = graph.AddRasterRenderPass<DrawData>("SlapPack same-frame water refraction", out DrawData data))
				{
					data.Scene = scene; data.Owner = owner; data.Model = owner.ModelMatrix;
					builder.UseTexture(scene, AccessFlags.Read);
					builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
					builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
					builder.AllowPassCulling(false);
					builder.SetRenderFunc((DrawData d, RasterGraphContext context) =>
					{
						if (!d.Owner || !d.Owner.CanDraw) return;
						try
						{
							MaterialPropertyBlock properties = context.renderGraphPool.GetTempMaterialPropertyBlock();
							properties.SetTexture(BaseMap, d.Owner.DebugTint ? Texture2D.whiteTexture : (Texture)d.Scene);
							properties.SetColor(BaseColor, d.Owner.DebugTint ? new Color(1, 0.15f, 1, 1) : Color.white);
							bool top = context.GetTextureUVOrigin(d.Scene) == TextureUVOrigin.TopLeft;
							properties.SetVector(BaseMapST, top ? new Vector4(1, -1, 0, 1) : new Vector4(1, 1, 0, 0));
							context.cmd.DrawMesh(d.Owner.Surface, d.Model, d.Owner.SurfaceMaterial, 0, 0, properties);
							d.Owner.Drawn();
						}
						catch (Exception error) { d.Owner.Fail(error.Message); }
					});
				}
			}
			catch (Exception error) { owner.Fail(error.Message); }
		}
	}
}
