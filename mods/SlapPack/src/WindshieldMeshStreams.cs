using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
	/// <summary>Separates changing UV/color data from unchanged positions/normals without changing their formats.</summary>
	internal sealed class WindshieldMeshStreams
	{
		/// <summary>The static stream keeps the original full-precision position and normal values.</summary>
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private struct StaticVertex
		{
			internal Vector3 Position, Normal;
			internal StaticVertex(Vector3 position, Vector3 normal) { Position = position; Normal = normal; }
		}

		private readonly bool split;
		private readonly int uvStream, colorStream;
		internal readonly Mesh Mesh;
		private const MeshUpdateFlags Updates = MeshUpdateFlags.DontRecalculateBounds;

		private WindshieldMeshStreams(Mesh mesh, bool split, int uvStream = 1, int colorStream = 2)
		{
			Mesh = mesh; this.split = split; this.uvStream = uvStream; this.colorStream = colorStream;
		}

		/// <summary>Builds independent dynamic streams, retaining the unmodified original mesh if setup is unsupported.</summary>
		internal static WindshieldMeshStreams Create(Mesh original, Vector3[] positions, Vector2[] uv, Color32[] colors, List<Object> owned)
		{
			if (!owned.Contains(original)) throw new InvalidOperationException("Only the owned generated refraction mesh may be optimized.");
			// Initialize the legacy channels before probing their layout. The legacy
			// mesh remains available throughout setup, so failure cannot leave half a layout.
			original.uv = uv; original.colors32 = colors;
			Mesh candidate = null;
			try
			{
				int previousUVStride = original.GetVertexBufferStride(original.GetVertexAttributeStream(VertexAttribute.TexCoord0));
				int previousColorStride = original.GetVertexBufferStride(original.GetVertexAttributeStream(VertexAttribute.Color));
				if (previousUVStride == 8 && previousColorStride == 4)
					return new WindshieldMeshStreams(original, true, original.GetVertexAttributeStream(VertexAttribute.TexCoord0), original.GetVertexAttributeStream(VertexAttribute.Color));
				Vector3[] normals = original.normals;
				if (normals.Length != positions.Length || original.subMeshCount != 1 || Marshal.SizeOf<StaticVertex>() != 24)
					throw new NotSupportedException("Unexpected source layout for the refraction mesh.");
				var fixedVertices = new StaticVertex[positions.Length];
				for (int i = 0; i < fixedVertices.Length; i++) fixedVertices[i] = new StaticVertex(positions[i], normals[i]);
				candidate = new Mesh { name = original.name, indexFormat = original.indexFormat }; candidate.MarkDynamic();
				candidate.SetVertexBufferParams(positions.Length,
					new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
					new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
					new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1),
					new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2));
				if (candidate.GetVertexBufferStride(0) != 24 || candidate.GetVertexBufferStride(1) != 8 || candidate.GetVertexBufferStride(2) != 4
					|| candidate.GetVertexAttributeOffset(VertexAttribute.Normal) != 12)
					throw new NotSupportedException("The driver did not retain the requested full-precision stream layout.");
				candidate.SetVertexBufferData(fixedVertices, 0, 0, fixedVertices.Length, 0, Updates);
				candidate.SetVertexBufferData(uv, 0, 0, uv.Length, 1, Updates);
				candidate.SetVertexBufferData(colors, 0, 0, colors.Length, 2, Updates);
				candidate.subMeshCount = 1;
				candidate.SetIndices(original.triangles, MeshTopology.Triangles, 0, false);
				candidate.bounds = original.bounds;
				if (candidate.vertexCount != original.vertexCount || candidate.GetIndexCount(0) != original.GetIndexCount(0))
					throw new InvalidOperationException("The optimized mesh did not preserve its vertex/index count.");
				var result = new WindshieldMeshStreams(candidate, true);
				ModLog.Info("Windshield dynamic mesh streams: UV stride " + previousUVStride + " -> 8 bytes/vertex, color stride " + previousColorStride + " -> 4; positions/normals remain Float32.");
				owned.Add(candidate); owned.Remove(original); Object.Destroy(original);
				return result;
			}
			catch (Exception error)
			{
				if (candidate) Object.Destroy(candidate);
				ModLog.Info("Windshield mesh stream optimization unavailable; retaining the original upload path: " + error.Message);
				return new WindshieldMeshStreams(original, false);
			}
		}

		/// <summary>Uploads only UVs when using split streams, without touching geometry or bounds.</summary>
		internal void UploadUV(Vector2[] uv)
		{
			if (split) Mesh.SetVertexBufferData(uv, 0, 0, uv.Length, uvStream, Updates);
			else Mesh.uv = uv;
		}

		/// <summary>Uploads the original packed RGBA bytes without reuploading static geometry.</summary>
		internal void UploadColors(Color32[] colors)
		{
			if (split) Mesh.SetVertexBufferData(colors, 0, 0, colors.Length, colorStream, Updates);
			else Mesh.colors32 = colors;
		}
	}
}
